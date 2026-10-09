using SwiftyS26.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace SwiftyS26.Controllers
{
    public class MaintenanceController : Controller
    {
        private GymHubDB db = new GymHubDB();

        private bool IsAdminLoggedIn() => Session["LoggedInAdmin"] != null;
        private bool IsTechnicianLoggedIn() => Session["LoggedInTechnician"] != null;

        private Technician GetLoggedInTechnician()
        {
            if (!IsTechnicianLoggedIn()) return null;
            int id = (int)Session["LoggedInTechnician"];
            return db.Technicians.Find(id);
        }

        // ============================================================
        // PERFORM MAINTENANCE — POST
        //
        // Callable by admin (unrestricted, same as PerformInspection) or
        // by the technician the maintenance was auto-assigned to (must
        // submit as themselves, and only for equipment actually assigned
        // to them — same enforcement pattern as PerformInspection).
        //
        // Completed: just advances the schedule.
        // IssueFound: takes equipment OutOfService and auto-creates a
        // Repair (source "Maintenance"), which then flows through the
        // exact same repair pipeline already built (assign/escalate/
        // vendor/verify) — no new repair-handling logic needed here.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PerformMaintenance(int equipmentId, int? technicianId, string result, string tasksPerformed, string partsReplaced, decimal? cost)
        {
            bool isAdmin = IsAdminLoggedIn();
            bool isTechnician = !isAdmin && IsTechnicianLoggedIn();

            if (!isAdmin && !isTechnician)
                return RedirectToAction("Login", "Admin");

            string redirectAction = isTechnician ? "MyMaintenance" : "Index";
            string redirectController = isTechnician ? "Technician" : "Equipment";

            Technician actingTechnician = null;
            if (isTechnician)
            {
                actingTechnician = GetLoggedInTechnician();
                if (actingTechnician == null)
                    return RedirectToAction("Login", "Admin");

                if (!technicianId.HasValue || technicianId.Value != actingTechnician.TechnicianId)
                {
                    TempData["Error"] = "You can only submit maintenance results under your own name.";
                    return RedirectToAction(redirectAction, redirectController);
                }
            }

            var equipment = db.Equipment.Find(equipmentId);
            if (equipment == null)
            {
                TempData["Error"] = "Equipment not found.";
                return RedirectToAction(redirectAction, redirectController);
            }

            if (result != "Completed" && result != "IssueFound")
            {
                TempData["Error"] = "Result must be Completed or Issue Found.";
                return RedirectToAction(redirectAction, redirectController);
            }

            if (isTechnician && equipment.PendingMaintenanceTechnicianId != actingTechnician.TechnicianId)
            {
                TempData["Error"] = "This maintenance isn't assigned to you.";
                return RedirectToAction(redirectAction, redirectController);
            }

            var maintenance = new Maintenance
            {
                EquipmentId = equipmentId,
                TechnicianId = technicianId,
                MaintenanceDate = DateTime.Now,
                Result = result,
                TasksPerformed = tasksPerformed,
                PartsReplaced = partsReplaced,
                Cost = cost
            };
            db.Maintenances.Add(maintenance);

            // The maintenance visit happened either way — advance the schedule
            equipment.LastMaintenanceDate = DateTime.Today;
            equipment.NextMaintenanceDueDate = DateTime.Today.AddDays(equipment.MaintenanceIntervalDays);

            // Clear the assignment — next due cycle gets its own fresh auto-assign
            equipment.PendingMaintenanceTechnicianId = null;
            equipment.PendingMaintenanceAssignedDate = null;

            Repair newRepair = null;
            if (result == "IssueFound")
            {
                equipment.Status = "OutOfService";

                newRepair = new Repair
                {
                    EquipmentId = equipmentId,
                    FaultDescription = string.IsNullOrEmpty(tasksPerformed) ? "Issue found during scheduled maintenance." : tasksPerformed,
                    RepairStatus = "Pending",
                    ReportedDate = DateTime.Now,
                    ReportedBySource = "Maintenance",
                    Category = "Other",
                    Severity = "Critical",
                    ReferenceNumber = RepairHelper.GenerateReferenceNumber(db)
                };
                db.Repairs.Add(newRepair);
            }

            db.Entry(equipment).State = EntityState.Modified;
            db.SaveChanges(); // saves maintenance + equipment (+ repair) together; IDs now exist

            if (newRepair != null)
            {
                newRepair.MaintenanceId = maintenance.MaintenanceId;
                db.Entry(newRepair).State = EntityState.Modified;
                db.SaveChanges();

                // Close the loop for a technician who just reported an issue —
                // tell them what their submission actually triggered.
                if (isTechnician)
                {
                    try
                    {
                        new NotificationService(db).Notify(
                            "Technician", actingTechnician.TechnicianId,
                            "Repair Logged From Your Maintenance Report",
                            $"Your issue report for \"{equipment.Name}\" has been logged as {newRepair.ReferenceNumber} and is awaiting assignment.",
                            "Info",
                            "/Technician/MyRepairs");
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }
                }

                try
                {
                    new NotificationService(db).NotifyAdmin(
                        "New Repair From Maintenance Issue",
                        $"An issue was found during maintenance on \"{equipment.Name}\" — repair {newRepair.ReferenceNumber} logged and awaiting assignment.",
                        "Alert",
                        "/Equipment/Index");
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }
            }

            TempData["Success"] = result == "Completed"
                ? $"Maintenance recorded for {equipment.Name} — completed, no issues."
                : $"Maintenance recorded for {equipment.Name} — issue found, repair logged.";
            return RedirectToAction(redirectAction, redirectController);
        }

        // ============================================================
        // LOG MAINTENANCE — POST, ad-hoc / unscheduled maintenance
        //
        // Covers work done outside the auto-assigned due cycle (e.g. a
        // technician notices something while working nearby and services
        // it on the spot). Mirrors RepairController.LogFault's role for
        // faults — this is that same "outside the normal flow" entry
        // point, but for maintenance. Not tied to any PendingMaintenance
        // assignment check, since by definition nothing assigned this.
        // Still advances the schedule the same as a due-triggered visit.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogMaintenance(int equipmentId, int? technicianId, string result, string tasksPerformed, string partsReplaced, decimal? cost)
        {
            bool isAdmin = IsAdminLoggedIn();
            bool isTechnician = !isAdmin && IsTechnicianLoggedIn();

            if (!isAdmin && !isTechnician)
                return RedirectToAction("Login", "Admin");

            string redirectAction = isTechnician ? "MyMaintenance" : "Index";
            string redirectController = isTechnician ? "Technician" : "Equipment";

            Technician actingTechnician = null;
            if (isTechnician)
            {
                actingTechnician = GetLoggedInTechnician();
                if (actingTechnician == null)
                    return RedirectToAction("Login", "Admin");

                if (!technicianId.HasValue || technicianId.Value != actingTechnician.TechnicianId)
                {
                    TempData["Error"] = "You can only log maintenance under your own name.";
                    return RedirectToAction(redirectAction, redirectController);
                }
            }

            var equipment = db.Equipment.Find(equipmentId);
            if (equipment == null || equipment.IsDeleted)
            {
                TempData["Error"] = "Equipment not found.";
                return RedirectToAction(redirectAction, redirectController);
            }

            if (result != "Completed" && result != "IssueFound")
            {
                TempData["Error"] = "Result must be Completed or Issue Found.";
                return RedirectToAction(redirectAction, redirectController);
            }

            var maintenance = new Maintenance
            {
                EquipmentId = equipmentId,
                TechnicianId = technicianId,
                MaintenanceDate = DateTime.Now,
                Result = result,
                TasksPerformed = tasksPerformed,
                PartsReplaced = partsReplaced,
                Cost = cost
            };
            db.Maintenances.Add(maintenance);

            // Same schedule bump as the due-triggered path — the work
            // happened regardless of whether it was scheduled
            equipment.LastMaintenanceDate = DateTime.Today;
            equipment.NextMaintenanceDueDate = DateTime.Today.AddDays(equipment.MaintenanceIntervalDays);

            Repair newRepair = null;
            if (result == "IssueFound")
            {
                equipment.Status = "OutOfService";

                newRepair = new Repair
                {
                    EquipmentId = equipmentId,
                    FaultDescription = string.IsNullOrEmpty(tasksPerformed) ? "Issue found during unscheduled maintenance." : tasksPerformed,
                    RepairStatus = "Pending",
                    ReportedDate = DateTime.Now,
                    ReportedBySource = "Maintenance",
                    Category = "Other",
                    Severity = "Critical",
                    ReferenceNumber = RepairHelper.GenerateReferenceNumber(db)
                };
                db.Repairs.Add(newRepair);
            }

            db.Entry(equipment).State = EntityState.Modified;
            db.SaveChanges();

            if (newRepair != null)
            {
                newRepair.MaintenanceId = maintenance.MaintenanceId;
                db.Entry(newRepair).State = EntityState.Modified;
                db.SaveChanges();

                if (isTechnician)
                {
                    try
                    {
                        new NotificationService(db).Notify(
                            "Technician", actingTechnician.TechnicianId,
                            "Repair Logged From Your Maintenance Report",
                            $"Your issue report for \"{equipment.Name}\" has been logged as {newRepair.ReferenceNumber} and is awaiting assignment.",
                            "Info",
                            "/Technician/MyRepairs");
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }
                }

                try
                {
                    new NotificationService(db).NotifyAdmin(
                        "New Repair From Maintenance Issue",
                        $"An issue was found during ad-hoc maintenance on \"{equipment.Name}\" — repair {newRepair.ReferenceNumber} logged and awaiting assignment.",
                        "Alert",
                        "/Equipment/Index");
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }
            }

            TempData["Success"] = $"Ad-hoc maintenance logged for {equipment.Name}.";
            return RedirectToAction(redirectAction, redirectController);
        }

        // ============================================================
        // REASSIGN MAINTENANCE — POST, admin override
        //
        // Auto-assignment is the default path, but the admin can still
        // swap who's assigned before it's performed — same override
        // capability we already gave repairs.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReassignMaintenance(int equipmentId, int technicianId)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            var equipment = db.Equipment.Find(equipmentId);
            if (equipment == null)
            {
                TempData["Error"] = "Equipment not found.";
                return RedirectToAction("Index", "Equipment");
            }

            var technician = db.Technicians.Find(technicianId);
            if (technician == null || !technician.IsActive)
            {
                TempData["Error"] = "Technician not found or inactive.";
                return RedirectToAction("Index", "Equipment");
            }

            equipment.PendingMaintenanceTechnicianId = technicianId;
            equipment.PendingMaintenanceAssignedDate = DateTime.Now;
            db.Entry(equipment).State = EntityState.Modified;
            db.SaveChanges();

            try
            {
                new NotificationService(db).Notify(
                    "Technician", technicianId,
                    "Maintenance Reassigned To You",
                    $"You've been assigned scheduled maintenance for \"{equipment.Name}\" ({equipment.EquipmentCode}).",
                    "Info",
                    "/Technician/MyMaintenance");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }

            TempData["Success"] = $"Maintenance reassigned to {technician.FullName}.";
            return RedirectToAction("Index", "Equipment");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}