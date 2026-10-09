using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace SwiftyS26.Models
{
    public static class RepairHelper
    {
        public static readonly string[] ValidStatuses = new[]
        {
            "Pending", "Assigned", "Accepted", "InProgress",
            "Escalated", "AwaitingInspection", "AwaitingApproval", "Resolved",
            "Unrepairable", "Reopened"
        };

        public static bool IsValidStatus(string status) =>
            ValidStatuses.Contains(status);

        // Statuses that still count as "open" for the Fault Repair tab —
        // everything except the two terminal outcomes. AwaitingApproval is
        // deliberately included — a passed inspection isn't actually done
        // until an admin signs off, so it must still show as active work.
        public static readonly string[] OpenStatuses = new[]
        {
            "Pending", "Assigned", "Accepted", "InProgress",
            "Escalated", "AwaitingInspection", "AwaitingApproval", "Reopened"
        };

        // Maps Equipment.Category to the Technician.Specialization values
        // that are the best fit for that category
        private static readonly Dictionary<string, string[]> CategorySpecMap =
            new Dictionary<string, string[]>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "Cardio", new[] { "Cardio Equipment" } },
                { "Strength Machines", new[] { "Strength Equipment" } },
                { "Free Weights", new[] { "Strength Equipment" } },
                { "Functional Training", new[] { "General Mechanical" } },
                { "Other", new[] { "General Mechanical", "Electrical/Electronic Systems" } }
            };

        // Recommends the best-fit active technician: specialization match first,
        // then lowest current active-assignment workload. Mirrors the scoring
        // pattern used for member-trainer matching in FindTrainer.
        public static Technician GetRecommendedTechnician(
            Equipment equipment,
            List<Technician> technicians,
            List<RepairAssignment> activeAssignments)
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
                    CurrentLoad = activeAssignments.Count(a => a.TechnicianId == t.TechnicianId)
                })
                .OrderByDescending(x => x.IsMatch)
                .ThenBy(x => x.CurrentLoad)
                .ToList();

            return scored.FirstOrDefault()?.Technician;
        }

        // ============================================================
        // Single source of truth for reference numbers, used by
        // ReportFault (member), LogFault (admin), and PerformInspection
        // (auto-created on Fail) so every repair gets one consistently.
        // ============================================================
        public static string GenerateReferenceNumber(GymHubDB db)
        {
            var rng = new Random();
            string refNumber;
            int attempts = 0;
            do
            {
                refNumber = "RF-" + DateTime.Now.Year + "-" + rng.Next(1000, 9999);
                attempts++;
            }
            while (attempts < 15 && db.Repairs.Any(r => r.ReferenceNumber == refNumber));

            return refNumber;
        }

        // ============================================================
        // Keeps the RepairAssignment record in sync whenever a Repair's
        // status changes via an action that doesn't already manipulate
        // its own loaded assignment object directly (i.e. the admin-side
        // MarkComplete / MarkUnrepairable / AssignVendor-adjacent flows).
        //
        // NOTE: the technician-side actions in TechnicianController
        // (StartRepair, CompleteRepair, EscalateRepair) do NOT call this —
        // they already have the assignment loaded and set its status
        // directly. This helper exists specifically for admin actions that
        // only have the Repair loaded, not a specific RepairAssignment.
        // ============================================================
        public static void SyncAssignmentOnStatusChange(GymHubDB db, int repairId, string newRepairStatus)
        {
            var assignment = db.RepairAssignments
                .Where(a => a.RepairId == repairId)
                .OrderByDescending(a => a.AssignedDate)
                .FirstOrDefault();

            if (assignment == null) return; // no assignment exists yet (e.g. status set before assignment) — nothing to sync

            switch (newRepairStatus)
            {
                case "InProgress":
                    if (assignment.AssignmentStatus == "Assigned")
                        assignment.AssignmentStatus = "Accepted";
                    if (assignment.AcceptedDate == null)
                        assignment.AcceptedDate = DateTime.Now;
                    if (assignment.StartedDate == null)
                        assignment.StartedDate = DateTime.Now;
                    break;

                // ── FIX: this case was missing entirely. Without it, a repair
                // closed out via the admin's MarkComplete action (used for
                // vendor-driven repairs, and any admin-managed technician
                // repair) never flips its RepairAssignment to "Completed" —
                // it silently stays "Assigned"/"Accepted" forever. That, in
                // turn, breaks InspectionController's Reopened-history sync,
                // which specifically searches for AssignmentStatus ==
                // "Completed" when a post-repair inspection fails. ──
                case "AwaitingInspection":
                    assignment.AssignmentStatus = "Completed";
                    if (assignment.CompletedDate == null)
                        assignment.CompletedDate = DateTime.Now;
                    break;

                case "Resolved":
                case "Unrepairable":
                    assignment.AssignmentStatus = "Completed";
                    if (assignment.CompletedDate == null)
                        assignment.CompletedDate = DateTime.Now;
                    break;

                case "Escalated":
                    assignment.AssignmentStatus = "Escalated";
                    break;

                    // Pending / Assigned / Reopened —
                    // no assignment-side change needed for these.
            }

            db.Entry(assignment).State = EntityState.Modified;
        }
    }
}