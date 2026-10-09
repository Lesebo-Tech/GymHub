using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace SwiftyS26.Models
{
    public static class InspectionHelper
    {
        // Same mapping used by RepairHelper/MaintenanceHelper — duplicated
        // deliberately rather than shared, so inspection auto-assignment
        // doesn't silently change behavior if the other helpers evolve.
        private static readonly Dictionary<string, string[]> CategorySpecMap =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Cardio", new[] { "Cardio Equipment" } },
                { "Strength Machines", new[] { "Strength Equipment" } },
                { "Free Weights", new[] { "Strength Equipment" } },
                { "Functional Training", new[] { "General Mechanical" } },
                { "Other", new[] { "General Mechanical", "Electrical/Electronic Systems" } }
            };

        private static Technician GetRecommendedTechnician(
            Equipment equipment,
            List<Technician> technicians,
            Dictionary<int, int> currentInspectionLoads)
        {
            if (technicians == null || !technicians.Any())
                return null;

            string[] preferredSpecs = (equipment?.Category != null && CategorySpecMap.ContainsKey(equipment.Category))
                ? CategorySpecMap[equipment.Category]
                : new string[0];

            var scored = technicians
                .Where(t => t.IsActive)
                .Select(t => new
                {
                    Technician = t,
                    IsMatch = preferredSpecs.Contains(t.Specialization),
                    CurrentLoad = currentInspectionLoads.ContainsKey(t.TechnicianId) ? currentInspectionLoads[t.TechnicianId] : 0
                })
                .OrderByDescending(x => x.IsMatch)
                .ThenBy(x => x.CurrentLoad)
                .ToList();

            return scored.FirstOrDefault()?.Technician;
        }

        // ============================================================
        // AUTO-ASSIGN DUE INSPECTION — fully automatic, mirrors
        // MaintenanceHelper.AutoAssignDueMaintenance exactly.
        //
        // Scope: ROUTINE, schedule-driven inspections only. Post-repair
        // and vendor-repair verification assignment stays manual — that
        // path carries a judgment rule (can't be the technician who did
        // the repair) that deserves a deliberate admin pick, not an
        // algorithm silently excluding someone.
        //
        // Called lazily whenever a relevant page loads (admin's Equipment
        // Management, or a technician's own MyInspections), same trigger
        // pattern as maintenance. Safe to call repeatedly — equipment
        // that already has PendingInspectionTechnicianId set is skipped.
        // ============================================================
        public static void AutoAssignDueInspection(GymHubDB db)
        {
            var dueUnassigned = db.Equipment
                .Where(e => !e.IsDeleted
                         && e.Status != "Retired"
                         && e.NextInspectionDueDate != null
                         && e.NextInspectionDueDate <= DateTime.Today
                         && e.PendingInspectionTechnicianId == null)
                .ToList();

            if (!dueUnassigned.Any())
                return;

            var activeTechnicians = db.Technicians.Where(t => t.IsActive).ToList();
            if (!activeTechnicians.Any())
                return; // stays unassigned until a technician exists — same as maintenance's fallback

            // Running in-memory workload count, spread across this batch
            // rather than piling everything onto whoever scored best first.
            var loads = db.Equipment
                .Where(e => e.PendingInspectionTechnicianId != null)
                .GroupBy(e => e.PendingInspectionTechnicianId.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var equipment in dueUnassigned)
            {
                var recommended = GetRecommendedTechnician(equipment, activeTechnicians, loads);
                if (recommended == null)
                    continue;

                equipment.PendingInspectionTechnicianId = recommended.TechnicianId;
                equipment.PendingInspectionAssignedDate = DateTime.Now;
                db.Entry(equipment).State = EntityState.Modified;

                loads[recommended.TechnicianId] = loads.ContainsKey(recommended.TechnicianId)
                    ? loads[recommended.TechnicianId] + 1
                    : 1;

                try
                {
                    new NotificationService(db).Notify(
                        "Technician", recommended.TechnicianId,
                        "Inspection Assigned To You",
                        $"You've been automatically assigned to inspect \"{equipment.Name}\" ({equipment.EquipmentCode}).",
                        "Info",
                        "/Technician/MyInspections");
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }
            }

            db.SaveChanges();
        }
    }
}
