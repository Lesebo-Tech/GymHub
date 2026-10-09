using SwiftyS26.Models;
using System;
using System.Data.Entity;
using System.Web.Mvc;
using System.Linq;

namespace SwiftyS26.Controllers
{
    public class TechnicianController : Controller
    {
        private GymHubDB db = new GymHubDB();

        private bool IsTechnicianLoggedIn() => Session["LoggedInTechnician"] != null;

        private Technician GetLoggedInTechnician()
        {
            if (!IsTechnicianLoggedIn()) return null;
            int id = (int)Session["LoggedInTechnician"];
            return db.Technicians.Find(id);
        }

        // ============================================================
        // DASHBOARD
        // ============================================================
        public ActionResult Dashboard()
        {
            if (!IsTechnicianLoggedIn())
                return RedirectToAction("Login", "Admin");

            var technician = GetLoggedInTechnician();
            if (technician == null)
                return RedirectToAction("Login", "Admin");

            if (technician.MustChangePassword)
                return RedirectToAction("ChangePassword");

            var myAssignments = db.RepairAssignments
                .Include(a => a.Repair)
                .Where(a => a.TechnicianId == technician.TechnicianId)
                .ToList();

            var startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

            ViewBag.TechnicianName = technician.FullName;
            ViewBag.TechnicianCode = technician.TechnicianCode;
            ViewBag.TechnicianRole = technician.Role;
            ViewBag.Specialization = technician.Specialization;
            ViewBag.CertificationLevel = technician.CertificationLevel;
            ViewBag.EmploymentType = technician.EmploymentType;

            ViewBag.AwaitingResponseCount = myAssignments.Count(a => a.AssignmentStatus == "Assigned");
            ViewBag.InProgressCount = myAssignments.Count(a => a.Repair.RepairStatus == "InProgress" || a.Repair.RepairStatus == "Accepted");
            ViewBag.AwaitingInspectionCount = myAssignments.Count(a => a.Repair.RepairStatus == "AwaitingInspection");
            ViewBag.CompletedThisMonthCount = myAssignments.Count(a =>
                a.AssignmentStatus == "Completed" && a.CompletedDate.HasValue && a.CompletedDate.Value >= startOfMonth);

            // A repair that came back Reopened after failing post-repair verification
            // needs the technician's attention just as much as a fresh assignment —
            // surface it as its own count so it isn't silently lost.
            ViewBag.ReopenedCount = myAssignments.Count(a => a.AssignmentStatus == "Reopened");

            ViewBag.RecentAssignments = myAssignments
                .Where(a => a.AssignmentStatus == "Assigned" || a.AssignmentStatus == "Accepted" || a.AssignmentStatus == "Reopened")
                .OrderByDescending(a => a.AssignedDate)
                .Take(5)
                .ToList();

            return View(technician);
        }

        // ============================================================
        // CHANGE PASSWORD — GET
        // ============================================================
        public ActionResult ChangePassword()
        {
            if (!IsTechnicianLoggedIn())
                return RedirectToAction("Login", "Admin");

            return View(new ChangePasswordViewModel());
        }

        // ============================================================
        // CHANGE PASSWORD — POST
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(ChangePasswordViewModel model)
        {
            if (!IsTechnicianLoggedIn())
                return RedirectToAction("Login", "Admin");

            if (!ModelState.IsValid)
                return View(model);

            var technician = GetLoggedInTechnician();
            if (technician == null)
                return RedirectToAction("Login", "Admin");

            string hashedCurrent = HashPassword(model.CurrentPassword);
            if (technician.PasswordHash != hashedCurrent)
            {
                ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                return View(model);
            }

            string hashedNew = HashPassword(model.NewPassword);
            if (hashedNew == technician.PasswordHash)
            {
                ModelState.AddModelError("NewPassword", "New password must be different from your current password.");
                return View(model);
            }

            technician.PasswordHash = hashedNew;
            technician.MustChangePassword = false;
            db.SaveChanges();

            TempData["Success"] = "Password changed successfully. Welcome to GymHub!";
            return RedirectToAction("Dashboard");
        }

        // ============================================================
        // LOGOUT
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            Session.Remove("LoggedInTechnician");
            Session.Remove("LoggedInTechnicianName");
            Session.Remove("LoggedInTechnicianCode");
            Session.Remove("LoggedInTechnicianRole");
            return RedirectToAction("Index", "Home");
        }

        // ============================================================
        // HELPER
        // ============================================================
        private string HashPassword(string password)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                var sb = new System.Text.StringBuilder();
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }


        // ============================================================
        // MY REPAIRS — dashboard list of assigned repairs
        //
        // Includes "Reopened" assignments alongside "Assigned"/"Accepted" —
        // a repair that failed its post-repair verification needs to come
        // back onto the technician's active worklist, not vanish. The view
        // is responsible for rendering a Reopened card differently (e.g. a
        // "resume work" action straight into InProgress) from a brand-new
        // Assigned card (which still needs Accept/Decline).
        // ============================================================
        public ActionResult MyRepairs()
        {
            if (!IsTechnicianLoggedIn())
                return RedirectToAction("Login", "Admin");

            var technician = GetLoggedInTechnician();
            if (technician == null)
                return RedirectToAction("Login", "Admin");

            var myAssignments = db.RepairAssignments
                .Include(a => a.Repair)
                .Include(a => a.Repair.Equipment)
                .Where(a => a.TechnicianId == technician.TechnicianId
                         && (a.AssignmentStatus == "Assigned"
                          || a.AssignmentStatus == "Accepted"
                          || a.AssignmentStatus == "Reopened"))
                .OrderByDescending(a => a.AssignedDate)
                .ToList();

            // ── Locker faults assigned to this technician ─────────────
            // Folded into this page rather than a separate one, since the
            // actions (start/resolve) are nearly identical to repairs and
            // a 4th technician sidebar page wasn't worth the clutter.
            ViewBag.MyLockerFaults = db.LockerFaults
                .Include(f => f.Locker)
                .Where(f => f.TechnicianId == technician.TechnicianId
                         && (f.Status == "Assigned" || f.Status == "InProgress"))
                .OrderByDescending(f => f.AssignedDate)
                .ToList();

            ViewBag.TechnicianName = technician.FullName;
            return View(myAssignments);
        }

        // ============================================================
        // ACCEPT ASSIGNMENT — POST
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AcceptAssignment(int repairAssignmentId)
        {
            if (!IsTechnicianLoggedIn())
                return RedirectToAction("Login", "Admin");

            var technician = GetLoggedInTechnician();
            var assignment = db.RepairAssignments
                .Include(a => a.Repair)
                .FirstOrDefault(a => a.RepairAssignmentId == repairAssignmentId && a.TechnicianId == technician.TechnicianId);

            if (assignment == null)
            {
                TempData["Error"] = "Assignment not found.";
                return RedirectToAction("MyRepairs");
            }
            if (assignment.AssignmentStatus != "Assigned")
            {
                TempData["Error"] = "This assignment has already been actioned.";
                return RedirectToAction("MyRepairs");
            }

            assignment.AssignmentStatus = "Accepted";
            assignment.AcceptedDate = DateTime.Now;
            assignment.Repair.RepairStatus = "Accepted";

            db.Entry(assignment).State = EntityState.Modified;
            db.Entry(assignment.Repair).State = EntityState.Modified;
            db.SaveChanges();

            TempData["Success"] = $"Repair accepted — {assignment.Repair.Equipment?.Name}.";
            return RedirectToAction("MyRepairs");
        }

        // ============================================================
        // DECLINE ASSIGNMENT — POST
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeclineAssignment(int repairAssignmentId, string declineReason)
        {
            if (!IsTechnicianLoggedIn())
                return RedirectToAction("Login", "Admin");

            if (string.IsNullOrWhiteSpace(declineReason))
            {
                TempData["Error"] = "Please provide a reason for declining.";
                return RedirectToAction("MyRepairs");
            }

            var technician = GetLoggedInTechnician();
            var assignment = db.RepairAssignments
                .Include(a => a.Repair)
                .FirstOrDefault(a => a.RepairAssignmentId == repairAssignmentId && a.TechnicianId == technician.TechnicianId);

            if (assignment == null)
            {
                TempData["Error"] = "Assignment not found.";
                return RedirectToAction("MyRepairs");
            }
            if (assignment.AssignmentStatus != "Assigned")
            {
                TempData["Error"] = "This assignment has already been actioned.";
                return RedirectToAction("MyRepairs");
            }

            assignment.AssignmentStatus = "Declined";
            assignment.DeclineReason = declineReason.Trim();

            // Send the repair back to the unassigned queue for the admin to reassign
            assignment.Repair.RepairStatus = "Pending";
            assignment.Repair.TechnicianId = null;

            db.Entry(assignment).State = EntityState.Modified;
            db.Entry(assignment.Repair).State = EntityState.Modified;
            db.SaveChanges();

            try
            {
                new NotificationService(db).NotifyAdmin(
                    "Repair Assignment Declined",
                    $"{technician.FullName} declined the repair for \"{assignment.Repair.Equipment?.Name}\" — reason: {assignment.DeclineReason}. Needs reassignment.",
                    "Warning",
                    "/Equipment/Index");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }

            TempData["Success"] = "Assignment declined. The admin will reassign this repair.";
            return RedirectToAction("MyRepairs");
        }

        // ============================================================
        // START REPAIR — POST
        //
        // Accepts a repair moving in from either "Accepted" (normal first
        // start) or "Reopened" (resuming after a failed post-repair
        // verification) — both are valid entry points into InProgress.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult StartRepair(int repairAssignmentId)
        {
            if (!IsTechnicianLoggedIn())
                return RedirectToAction("Login", "Admin");

            var technician = GetLoggedInTechnician();
            var assignment = db.RepairAssignments
                .Include(a => a.Repair)
                .FirstOrDefault(a => a.RepairAssignmentId == repairAssignmentId && a.TechnicianId == technician.TechnicianId);

            if (assignment == null)
            {
                TempData["Error"] = "Assignment not found.";
                return RedirectToAction("MyRepairs");
            }
            if (assignment.AssignmentStatus != "Accepted" && assignment.AssignmentStatus != "Reopened")
            {
                TempData["Error"] = "You must accept this assignment before starting work.";
                return RedirectToAction("MyRepairs");
            }

            assignment.StartedDate = DateTime.Now;
            assignment.AssignmentStatus = "Accepted"; // normalize back into the active-work state
            assignment.Repair.RepairStatus = "InProgress";

            db.Entry(assignment).State = EntityState.Modified;
            db.Entry(assignment.Repair).State = EntityState.Modified;
            db.SaveChanges();

            TempData["Success"] = $"Repair started — {assignment.Repair.Equipment?.Name}.";
            return RedirectToAction("MyRepairs");
        }

        // ============================================================
        // COMPLETE REPAIR — POST
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CompleteRepair(int repairAssignmentId, string diagnosisNotes, string partsUsed, decimal? repairCost)
        {
            if (!IsTechnicianLoggedIn())
                return RedirectToAction("Login", "Admin");

            var technician = GetLoggedInTechnician();
            var assignment = db.RepairAssignments
                .Include(a => a.Repair)
                .FirstOrDefault(a => a.RepairAssignmentId == repairAssignmentId && a.TechnicianId == technician.TechnicianId);

            if (assignment == null)
            {
                TempData["Error"] = "Assignment not found.";
                return RedirectToAction("MyRepairs");
            }
            if (assignment.Repair.RepairStatus != "InProgress")
            {
                TempData["Error"] = "This repair must be In Progress before it can be completed.";
                return RedirectToAction("MyRepairs");
            }

            assignment.CompletedDate = DateTime.Now;
            assignment.AssignmentStatus = "Completed";

            assignment.Repair.DiagnosisNotes = diagnosisNotes;
            assignment.Repair.PartsUsed = partsUsed;
            assignment.Repair.RepairCost = repairCost;
            assignment.Repair.RepairStatus = "AwaitingInspection"; // gated by post-repair inspection before it's truly Resolved

            db.Entry(assignment).State = EntityState.Modified;
            db.Entry(assignment.Repair).State = EntityState.Modified;
            db.SaveChanges();

            try
            {
                new NotificationService(db).NotifyAdmin(
                    "Repair Awaiting Inspection Assignment",
                    $"{technician.FullName} marked the repair on \"{assignment.Repair.Equipment?.Name}\" complete — assign a verifier from the Inspections Due tab.",
                    "Info",
                    "/Equipment/Index");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }

            TempData["Success"] = $"Repair marked complete — awaiting post-repair inspection.";
            return RedirectToAction("MyRepairs");
        }
        // ============================================================
        // ESCALATE REPAIR — POST, technician can't fix it in-house
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EscalateRepair(int repairAssignmentId, string escalationReason)
        {
            if (!IsTechnicianLoggedIn())
                return RedirectToAction("Login", "Admin");

            if (string.IsNullOrWhiteSpace(escalationReason))
            {
                TempData["Error"] = "Please explain why this repair needs to be escalated.";
                return RedirectToAction("MyRepairs");
            }

            var technician = GetLoggedInTechnician();
            var assignment = db.RepairAssignments
                .Include(a => a.Repair.Equipment)
                .FirstOrDefault(a => a.RepairAssignmentId == repairAssignmentId && a.TechnicianId == technician.TechnicianId);

            if (assignment == null)
            {
                TempData["Error"] = "Assignment not found.";
                return RedirectToAction("MyRepairs");
            }
            if (assignment.Repair.RepairStatus != "InProgress")
            {
                TempData["Error"] = "Only repairs currently in progress can be escalated.";
                return RedirectToAction("MyRepairs");
            }

            assignment.AssignmentStatus = "Escalated";
            assignment.EscalationReason = escalationReason.Trim();
            assignment.Repair.RepairStatus = "Escalated";

            db.Entry(assignment).State = EntityState.Modified;
            db.Entry(assignment.Repair).State = EntityState.Modified;
            db.SaveChanges();

            try
            {
                new NotificationService(db).NotifyAdmin(
                    "Repair Escalated — Vendor Needed",
                    $"{technician.FullName} escalated the repair on \"{assignment.Repair.Equipment?.Name}\" — reason: {assignment.EscalationReason}. Assign a vendor from the Fault Repair tab.",
                    "Warning",
                    "/Equipment/Index");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }

            TempData["Success"] = $"Repair escalated — admin will review and arrange a vendor if needed.";
            return RedirectToAction("MyRepairs");
        }
        // ============================================================
        // REPAIR HISTORY — resolved/unrepairable/superseded assignments
        //
        // Note: a "Reopened" assignment is NOT included here — it belongs
        // in MyRepairs as active work, not History, since the technician
        // still needs to act on it. It only reaches History once it's
        // completed again and passes post-repair verification (or is
        // escalated/superseded from that second attempt).
        // ============================================================
        public ActionResult RepairHistory()
        {
            if (!IsTechnicianLoggedIn())
                return RedirectToAction("Login", "Admin");

            var technician = GetLoggedInTechnician();
            if (technician == null)
                return RedirectToAction("Login", "Admin");

            var history = db.RepairAssignments
                .Include(a => a.Repair.Equipment)
                .Where(a => a.TechnicianId == technician.TechnicianId
                         && (a.AssignmentStatus == "Completed"
                          || a.AssignmentStatus == "Declined"
                          || a.AssignmentStatus == "Escalated"
                          || a.AssignmentStatus == "Superseded"))
                .OrderByDescending(a => a.CompletedDate ?? a.AssignedDate)
                .ToList();

            ViewBag.TechnicianName = technician.FullName;
            return View(history);
        }

        // ============================================================
        // MY INSPECTIONS — routine equipment inspections AND post-repair/
        // vendor verifications currently assigned to this technician.
        // The actual Pass/Fail submission posts to
        // InspectionController.PerformInspection, which now allows
        // technicians as well as admins, restricted to only what's
        // assigned to them specifically.
        // ============================================================
        public ActionResult MyInspections()
        {
            if (!IsTechnicianLoggedIn())
                return RedirectToAction("Login", "Admin");

            var technician = GetLoggedInTechnician();
            if (technician == null)
                return RedirectToAction("Login", "Admin");

            // Same reasoning as MyMaintenance — a technician logging in
            // before any admin today should still get freshly-due
            // inspections auto-assigned to them, not just whoever's on
            // the equipment page most often.
            InspectionHelper.AutoAssignDueInspection(db);

            var vm = new MyInspectionsViewModel
            {
                RoutineAssigned = db.Equipment
                    .Where(e => !e.IsDeleted && e.PendingInspectionTechnicianId == technician.TechnicianId)
                    .OrderBy(e => e.NextInspectionDueDate)
                    .ToList(),

                VerificationAssigned = db.Repairs
                    .Include(r => r.Equipment)
                    .Where(r => !r.Equipment.IsDeleted
                             && r.VerificationTechnicianId == technician.TechnicianId
                             && r.RepairStatus == "AwaitingInspection")
                    .OrderByDescending(r => r.VerificationAssignedDate)
                    .ToList()
            };

            ViewBag.TechnicianName = technician.FullName;
            ViewBag.TechnicianId = technician.TechnicianId;
            return View(vm);
        }

        // ============================================================
        // MY MAINTENANCE — equipment auto-assigned to this technician
        // for scheduled maintenance. Calls AutoAssignDueMaintenance first
        // so a technician logging in before any admin today still gets
        // freshly-due equipment assigned to them, not just whoever's on
        // the equipment page most often.
        // ============================================================
        public ActionResult MyMaintenance()
        {
            if (!IsTechnicianLoggedIn())
                return RedirectToAction("Login", "Admin");

            var technician = GetLoggedInTechnician();
            if (technician == null)
                return RedirectToAction("Login", "Admin");

            //MaintenanceHelper.AutoAssignDueMaintenance(db);

            var myMaintenance = db.Equipment
                .Where(e => !e.IsDeleted && e.PendingMaintenanceTechnicianId == technician.TechnicianId)
                .OrderBy(e => e.NextMaintenanceDueDate)
                .ToList();

            // For the ad-hoc "Log Maintenance" dropdown — any active equipment,
            // not just what's currently assigned, since unscheduled work by
            // definition isn't tied to an assignment
            ViewBag.AllActiveEquipment = db.Equipment
                .Where(e => !e.IsDeleted && e.Status != "Retired")
                .OrderBy(e => e.Name)
                .ToList();

            ViewBag.TechnicianName = technician.FullName;
            ViewBag.TechnicianId = technician.TechnicianId;
            return View(myMaintenance);
        }

        // ============================================================
        // MY MAINTENANCE HISTORY — mirrors RepairHistory's role, but for
        // completed maintenance visits (both due-triggered and ad-hoc).
        // ============================================================
        public ActionResult MyMaintenanceHistory()
        {
            if (!IsTechnicianLoggedIn())
                return RedirectToAction("Login", "Admin");

            var technician = GetLoggedInTechnician();
            if (technician == null)
                return RedirectToAction("Login", "Admin");

            var history = db.Maintenances
                .Include(m => m.Equipment)
                .Where(m => m.TechnicianId == technician.TechnicianId && !m.Equipment.IsDeleted)
                .OrderByDescending(m => m.MaintenanceDate)
                .ToList();

            ViewBag.TechnicianName = technician.FullName;
            return View(history);
        }

        // ============================================================
        // SCAN QR — GET, renders the camera scanner page
        // ============================================================
        public ActionResult ScanQr()
        {
            if (!IsTechnicianLoggedIn())
                return RedirectToAction("Login", "Admin");

            var technician = GetLoggedInTechnician();
            if (technician == null)
                return RedirectToAction("Login", "Admin");

            ViewBag.TechnicianName = technician.FullName;
            return View();
        }

        // ============================================================
        // RESOLVE QR — POST (AJAX), called after the camera decodes a
        // QR code. Reuses the same pipe-delimited format
        // EquipmentController.VerifyEquipmentQr already parses:
        //   "GYMHUB EQUIPMENT | Code: EQ-0001 | Name: Treadmill | Area: Cardio Zone"
        //
        // Resolves straight to whatever's assigned to THIS technician
        // for that equipment — repair, routine inspection, verification,
        // or maintenance — checked in that priority order. If nothing is
        // assigned to them, says so rather than guessing.
        // ============================================================
        [HttpPost]
        public JsonResult ResolveQr(string qrData)
        {
            if (!IsTechnicianLoggedIn())
                return Json(new { success = false, message = "Session expired. Please log in again." });

            var technician = GetLoggedInTechnician();
            if (technician == null)
                return Json(new { success = false, message = "Session expired. Please log in again." });

            if (string.IsNullOrWhiteSpace(qrData))
                return Json(new { success = false, message = "No QR data received." });

            string code = ExtractQrField(qrData, "Code:");
            if (string.IsNullOrWhiteSpace(code))
                return Json(new { success = false, message = "Unrecognised QR code. Please scan a valid GymHub equipment code." });

            var equipment = db.Equipment.FirstOrDefault(e => e.EquipmentCode == code && !e.IsDeleted);
            if (equipment == null)
                return Json(new { success = false, message = $"No equipment found for code {code}." });

            // 1. An active repair assigned to this technician on this equipment?
            bool hasRepair = db.RepairAssignments
                .Any(a => a.TechnicianId == technician.TechnicianId
                       && a.Repair.EquipmentId == equipment.EquipmentId
                       && (a.AssignmentStatus == "Assigned" || a.AssignmentStatus == "Accepted" || a.AssignmentStatus == "Reopened"));
            if (hasRepair)
                return Json(new
                {
                    success = true,
                    equipmentName = equipment.Name,
                    equipmentCode = equipment.EquipmentCode,
                    matchType = "Repair",
                    redirectUrl = Url.Action("MyRepairs", "Technician")
                });

            // 2. A routine inspection auto-assigned to this technician on this equipment?
            if (equipment.PendingInspectionTechnicianId == technician.TechnicianId)
                return Json(new
                {
                    success = true,
                    equipmentName = equipment.Name,
                    equipmentCode = equipment.EquipmentCode,
                    matchType = "Inspection",
                    redirectUrl = Url.Action("MyInspections", "Technician")
                });

            // 3. A post-repair/vendor verification assigned to this technician on this equipment?
            bool hasVerification = db.Repairs
                .Any(r => r.EquipmentId == equipment.EquipmentId
                       && r.VerificationTechnicianId == technician.TechnicianId
                       && r.RepairStatus == "AwaitingInspection");
            if (hasVerification)
                return Json(new
                {
                    success = true,
                    equipmentName = equipment.Name,
                    equipmentCode = equipment.EquipmentCode,
                    matchType = "Verification",
                    redirectUrl = Url.Action("MyInspections", "Technician")
                });

            // 4. Maintenance auto-assigned to this technician on this equipment?
            if (equipment.PendingMaintenanceTechnicianId == technician.TechnicianId)
                return Json(new
                {
                    success = true,
                    equipmentName = equipment.Name,
                    equipmentCode = equipment.EquipmentCode,
                    matchType = "Maintenance",
                    redirectUrl = Url.Action("MyMaintenance", "Technician")
                });

            // Nothing assigned to this technician for this equipment
            return Json(new
            {
                success = false,
                equipmentName = equipment.Name,
                equipmentCode = equipment.EquipmentCode,
                message = $"\"{equipment.Name}\" ({equipment.EquipmentCode}) has no task currently assigned to you."
            });
        }

        // ── HELPER — pulls a "Key: value" segment out of the pipe-delimited QR string ──
        private string ExtractQrField(string qrData, string key)
        {
            var parts = qrData.Split('|');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.StartsWith(key))
                    return trimmed.Substring(key.Length).Trim();
            }
            return null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}