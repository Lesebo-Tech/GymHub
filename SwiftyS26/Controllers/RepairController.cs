using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using SwiftyS26.Models;
using System.Data.Entity;

namespace SwiftyS26.Controllers
{
    public class RepairController : Controller
    {
        private GymHubDB db = new GymHubDB();

        private bool IsAdminLoggedIn() => Session["LoggedInAdmin"] != null;

        // ============================================================
        // LOG FAULT — POST, creates a Repair record for OutOfService equipment
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogFault(int equipmentId, int? inspectionId, string faultDescription, string category = "Other")
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            var equipment = db.Equipment.Find(equipmentId);
            if (equipment == null)
            {
                TempData["Error"] = "Equipment not found.";
                return RedirectToAction("Index", "Equipment");
            }

            var repair = new Repair
            {
                EquipmentId = equipmentId,
                InspectionId = inspectionId,
                FaultDescription = faultDescription,
                RepairStatus = "Pending",
                ReportedDate = DateTime.Now,
                ReportedBySource = "Admin",
                Category = FaultCategoryHelper.IsValidCategory(category) ? category : "Other",
                Severity = "Critical", // admin-logged faults force OutOfService, so treat as Critical
                ReferenceNumber = RepairHelper.GenerateReferenceNumber(db)
            };
            db.Repairs.Add(repair);

            // Equipment should already be OutOfService from a failed inspection,
            // but this covers faults reported outside that flow too
            equipment.Status = "OutOfService";
            db.Entry(equipment).State = EntityState.Modified;

            db.SaveChanges();

            TempData["Success"] = $"Fault logged for {equipment.Name} ({equipment.EquipmentCode}) — Ref: {repair.ReferenceNumber}.";
            return RedirectToAction("Index", "Equipment");
        }

        // ============================================================
        // MARK COMPLETE — POST
        //
        // Admin-side equivalent of the technician's own "Complete Repair"
        // action — used when the person doing the actual fix (a vendor, or
        // a technician the admin is closing out on their behalf) reports
        // the work is done. This NEVER sets Resolved directly — it always
        // routes through AwaitingInspection, same gate every repair has to
        // pass through regardless of who performed it.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkComplete(int repairId, string diagnosisNotes, string partsUsed, decimal? repairCost)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            var repair = db.Repairs
                .Include(r => r.Equipment)
                .FirstOrDefault(r => r.RepairId == repairId);

            if (repair == null)
            {
                TempData["Error"] = "Repair record not found.";
                return RedirectToAction("Index", "Equipment");
            }

            var validFromStatuses = new[] { "Assigned", "Accepted", "InProgress", "Reopened" };
            if (!validFromStatuses.Contains(repair.RepairStatus))
            {
                TempData["Error"] = $"This repair is currently \"{repair.RepairStatus}\" and can't be marked complete from there.";
                return RedirectToAction("Index", "Equipment");
            }

            if (repair.TechnicianId == null && repair.VendorId == null)
            {
                TempData["Error"] = "Assign a technician or vendor before marking this repair complete.";
                return RedirectToAction("Index", "Equipment");
            }

            repair.DiagnosisNotes = diagnosisNotes;
            repair.PartsUsed = partsUsed;
            repair.RepairCost = repairCost;
            repair.RepairStatus = "AwaitingInspection";

            var equipment = repair.Equipment;
            equipment.Status = "OutOfService"; // still down until a post-repair inspection passes it

            // ── Keep the RepairAssignment record in sync (mirrors what
            // CompleteRepair does on the technician side) ──
            RepairHelper.SyncAssignmentOnStatusChange(db, repairId, "AwaitingInspection");

            db.Entry(repair).State = EntityState.Modified;
            db.Entry(equipment).State = EntityState.Modified;
            db.SaveChanges();

            TempData["Success"] = $"{equipment.Name} marked complete — awaiting post-repair inspection.";
            return RedirectToAction("Index", "Equipment");
        }

        // ============================================================
        // MARK UNREPAIRABLE — POST
        //
        // Closes a repair out as beyond repair — no inspection gate needed
        // here, since there's nothing to verify (the equipment is being
        // retired, not returned to service).
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkUnrepairable(int repairId, string reason)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Please explain why this equipment can't be repaired.";
                return RedirectToAction("Index", "Equipment");
            }

            var repair = db.Repairs
                .Include(r => r.Equipment)
                .Include(r => r.ReportedByMember)
                .FirstOrDefault(r => r.RepairId == repairId);

            if (repair == null)
            {
                TempData["Error"] = "Repair record not found.";
                return RedirectToAction("Index", "Equipment");
            }

            var closedStatuses = new[] { "Resolved", "Unrepairable" };
            if (closedStatuses.Contains(repair.RepairStatus))
            {
                TempData["Error"] = "This repair has already been closed out.";
                return RedirectToAction("Index", "Equipment");
            }

            repair.DiagnosisNotes = reason.Trim();
            repair.RepairStatus = "Unrepairable";
            repair.ResolvedDate = DateTime.Now;

            var equipment = repair.Equipment;
            equipment.Status = "Retired";
            equipment.RetiredAt = DateTime.Now;
            equipment.RetirementReason = "Unrepairable: " + reason.Trim();

            RepairHelper.SyncAssignmentOnStatusChange(db, repairId, "Unrepairable");

            db.Entry(repair).State = EntityState.Modified;
            db.Entry(equipment).State = EntityState.Modified;
            db.SaveChanges();

            if (repair.ReportedBySource == "Member" && repair.ReportedByMemberId.HasValue)
            {
                try
                {
                    new NotificationService(db).Notify(
                        "Member", repair.ReportedByMemberId.Value,
                        "Fault Report Update",
                        $"\"{equipment.Name}\" could not be repaired and has been retired. Thanks for flagging it.",
                        "Info",
                        "/Dashboard/Equipment");
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }
            }

            TempData["Success"] = $"{equipment.Name} marked unrepairable and retired.";
            return RedirectToAction("Index", "Equipment");
        }

        // ============================================================
        // ASSIGN TECHNICIAN — POST, creates a RepairAssignment record
        // and moves the repair from Pending into the assignment flow
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssignTechnician(int repairId, int technicianId, string priority, string dueDate, string assignmentNotes)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            var repair = db.Repairs.Include(r => r.Equipment).FirstOrDefault(r => r.RepairId == repairId);
            if (repair == null)
            {
                TempData["Error"] = "Repair not found.";
                return RedirectToAction("Index", "Equipment");
            }

            if (repair.RepairStatus != "Pending")
            {
                TempData["Error"] = "This repair is not awaiting assignment.";
                return RedirectToAction("Index", "Equipment");
            }

            var technician = db.Technicians.Find(technicianId);
            if (technician == null || !technician.IsActive)
            {
                TempData["Error"] = "Technician not found or inactive.";
                return RedirectToAction("Index", "Equipment");
            }

            DateTime? due = null;
            if (DateTime.TryParse(dueDate, out DateTime parsedDue))
                due = parsedDue;

            var assignment = new RepairAssignment
            {
                RepairId = repairId,
                AssigneeType = "Technician",
                TechnicianId = technicianId,
                AssignedByAdminId = 1, // matches AdminId constant in AdminController's single-admin setup
                AssignedDate = DateTime.Now,
                DueDate = due,
                Priority = priority,
                AssignmentNotes = assignmentNotes,
                AssignmentStatus = "Assigned"
            };
            db.RepairAssignments.Add(assignment);

            repair.RepairStatus = "Assigned";
            repair.TechnicianId = technicianId;
            db.Entry(repair).State = EntityState.Modified;

            db.SaveChanges();

            // ── Notify the technician of the new job (wrapped in try/catch — never blocks the assignment itself) ──
            try
            {
                string equipName = repair.Equipment?.Name ?? "equipment";
                new NotificationService(db).Notify(
                    "Technician", technicianId,
                    "New Repair Assigned",
                    $"You've been assigned a {priority?.ToLower() ?? "medium"} priority repair for \"{equipName}\"" +
                        (due.HasValue ? $", due {due.Value:dd MMM yyyy}." : "."),
                    "Info",
                    "/Technician/Assignments");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }

            TempData["Success"] = $"Repair assigned to {technician.FullName}.";
            return RedirectToAction("Index", "Equipment");
        }

        // ============================================================
        // REASSIGN TECHNICIAN — POST
        //
        // Covers the gap left by removing the old free-form UpdateRepair:
        // an admin can no longer just change who's assigned by editing a
        // dropdown. This is the deliberate, purpose-built replacement —
        // only usable before real work has started (Assigned/Accepted),
        // since once a repair is InProgress, EscalateRepair is the correct
        // path for "someone else needs to take this over."
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReassignTechnician(int repairId, int technicianId, string priority, string dueDate, string assignmentNotes)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            var repair = db.Repairs.Include(r => r.Equipment).FirstOrDefault(r => r.RepairId == repairId);
            if (repair == null)
            {
                TempData["Error"] = "Repair not found.";
                return RedirectToAction("Index", "Equipment");
            }

            var reassignableStatuses = new[] { "Assigned", "Accepted" };
            if (!reassignableStatuses.Contains(repair.RepairStatus))
            {
                TempData["Error"] = "This repair can only be reassigned before work has started. Once In Progress, use Escalate instead.";
                return RedirectToAction("Index", "Equipment");
            }

            var technician = db.Technicians.Find(technicianId);
            if (technician == null || !technician.IsActive)
            {
                TempData["Error"] = "Technician not found or inactive.";
                return RedirectToAction("Index", "Equipment");
            }

            // Mark whatever assignment currently exists as superseded —
            // same pattern used when a repair escalates to a vendor
            var priorAssignment = db.RepairAssignments
                .Where(a => a.RepairId == repairId)
                .OrderByDescending(a => a.AssignedDate)
                .FirstOrDefault();
            if (priorAssignment != null && (priorAssignment.AssignmentStatus == "Assigned" || priorAssignment.AssignmentStatus == "Accepted"))
            {
                priorAssignment.AssignmentStatus = "Superseded";
                db.Entry(priorAssignment).State = EntityState.Modified;
            }

            DateTime? due = null;
            if (DateTime.TryParse(dueDate, out DateTime parsedDue))
                due = parsedDue;

            var newAssignment = new RepairAssignment
            {
                RepairId = repairId,
                AssigneeType = "Technician",
                TechnicianId = technicianId,
                AssignedByAdminId = 1,
                AssignedDate = DateTime.Now,
                DueDate = due,
                Priority = priority,
                AssignmentNotes = assignmentNotes,
                AssignmentStatus = "Assigned"
            };
            db.RepairAssignments.Add(newAssignment);

            // Reset to "Assigned" even if it was already "Accepted" — the new
            // technician hasn't accepted anything yet, they're starting fresh
            repair.RepairStatus = "Assigned";
            repair.TechnicianId = technicianId;
            db.Entry(repair).State = EntityState.Modified;

            db.SaveChanges();

            try
            {
                string equipName = repair.Equipment?.Name ?? "equipment";
                new NotificationService(db).Notify(
                    "Technician", technicianId,
                    "Repair Reassigned To You",
                    $"You've been assigned a {priority?.ToLower() ?? "medium"} priority repair for \"{equipName}\"" +
                        (due.HasValue ? $", due {due.Value:dd MMM yyyy}." : "."),
                    "Info",
                    "/Technician/Assignments");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }

            TempData["Success"] = $"Repair reassigned to {technician.FullName}.";
            return RedirectToAction("Index", "Equipment");
        }
        // ============================================================
        // ASSIGN VENDOR — POST, admin approves escalation and assigns
        // an external vendor in one combined step
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssignVendor(int repairId, int vendorId, string priority, string dueDate, string assignmentNotes)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            var repair = db.Repairs.Include(r => r.Equipment).FirstOrDefault(r => r.RepairId == repairId);
            if (repair == null)
            {
                TempData["Error"] = "Repair not found.";
                return RedirectToAction("Index", "Equipment");
            }
            if (repair.RepairStatus != "Escalated")
            {
                TempData["Error"] = "This repair has not been escalated for vendor review.";
                return RedirectToAction("Index", "Equipment");
            }

            var vendor = db.Vendors.Find(vendorId);
            if (vendor == null || !vendor.IsActive)
            {
                TempData["Error"] = "Vendor not found or inactive.";
                return RedirectToAction("Index", "Equipment");
            }

            // Mark the technician's escalated assignment as superseded — the vendor takes over now
            var priorAssignment = db.RepairAssignments
                .Where(a => a.RepairId == repairId)
                .OrderByDescending(a => a.AssignedDate)
                .FirstOrDefault();
            if (priorAssignment != null && priorAssignment.AssignmentStatus == "Escalated")
            {
                priorAssignment.AssignmentStatus = "Superseded";
                db.Entry(priorAssignment).State = EntityState.Modified;
            }

            DateTime? due = null;
            if (DateTime.TryParse(dueDate, out DateTime parsedDue))
                due = parsedDue;

            var assignment = new RepairAssignment
            {
                RepairId = repairId,
                AssigneeType = "Vendor",
                VendorId = vendorId,
                AssignedByAdminId = 1,
                AssignedDate = DateTime.Now,
                DueDate = due,
                Priority = priority,
                AssignmentNotes = assignmentNotes,
                AssignmentStatus = "Assigned"
            };
            db.RepairAssignments.Add(assignment);

            repair.RepairStatus = "Assigned"; // reuses the same status — Update Repair now handles vendor completion
            repair.VendorId = vendorId;
            db.Entry(repair).State = EntityState.Modified;
            db.SaveChanges();

            TempData["Success"] = $"Escalation approved — assigned to vendor {vendor.CompanyName}.";
            return RedirectToAction("Index", "Equipment");
        }

        // ============================================================
        // APPROVE COMPLETION — POST (admin only)
        //
        // The final sign-off gate. A passed inspection alone no longer
        // returns equipment to service — this is the explicit human
        // checkpoint that does. Only reachable from AwaitingApproval.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ApproveCompletion(int repairId)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            var repair = db.Repairs
                .Include(r => r.Equipment)
                .Include(r => r.ReportedByMember)
                .FirstOrDefault(r => r.RepairId == repairId);

            if (repair == null)
            {
                TempData["Error"] = "Repair not found.";
                return RedirectToAction("Index", "Equipment");
            }

            if (repair.RepairStatus != "AwaitingApproval")
            {
                TempData["Error"] = "This repair is not awaiting approval.";
                return RedirectToAction("Index", "Equipment");
            }

            repair.RepairStatus = "Resolved";
            repair.ResolvedDate = DateTime.Now;

            var equipment = repair.Equipment;
            equipment.Status = "Available";
            equipment.LastMaintenanceDate = DateTime.Today;

            db.Entry(repair).State = EntityState.Modified;
            db.Entry(equipment).State = EntityState.Modified;
            db.SaveChanges();

            // Member finds out only now — once an admin has actually signed off
            if (repair.ReportedBySource == "Member" && repair.ReportedByMemberId.HasValue)
            {
                try
                {
                    new NotificationService(db).Notify(
                        "Member", repair.ReportedByMemberId.Value,
                        "Fault Report Update",
                        $"\"{equipment.Name}\" has been repaired, verified, and is back in service. Thanks for the report!",
                        "Success",
                        "/Dashboard/Equipment");
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }
            }

            TempData["Success"] = $"Approved — {equipment.Name} is back in service.";
            return RedirectToAction("Index", "Equipment");
        }

        // ============================================================
        // REJECT COMPLETION — POST (admin only)
        //
        // Admin disagrees with a passed inspection. Same outcome as if
        // the inspection itself had failed — routes back to the same
        // technician as Reopened — but with the admin's own reason
        // captured separately from the technician's diagnosis notes.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectCompletion(int repairId, string reason)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Please explain why this completion is being rejected.";
                return RedirectToAction("Index", "Equipment");
            }

            var repair = db.Repairs
                .Include(r => r.Equipment)
                .FirstOrDefault(r => r.RepairId == repairId);

            if (repair == null)
            {
                TempData["Error"] = "Repair not found.";
                return RedirectToAction("Index", "Equipment");
            }

            if (repair.RepairStatus != "AwaitingApproval")
            {
                TempData["Error"] = "This repair is not awaiting approval.";
                return RedirectToAction("Index", "Equipment");
            }

            repair.RepairStatus = "Reopened";
            repair.ApprovalRejectionReason = reason.Trim();
            // equipment.Status is already OutOfService — untouched, correctly so

            // ── Same assignment-history correction the Fail path already
            // does — a "Completed" job that gets rejected isn't really
            // complete, so the technician's record should reflect that. ──
            var originalAssignment = db.RepairAssignments
                .Where(a => a.RepairId == repairId && a.AssignmentStatus == "Completed")
                .OrderByDescending(a => a.CompletedDate)
                .FirstOrDefault();
            if (originalAssignment != null)
            {
                originalAssignment.AssignmentStatus = "Reopened";
                db.Entry(originalAssignment).State = EntityState.Modified;

                try
                {
                    if (originalAssignment.TechnicianId.HasValue)
                    {
                        new NotificationService(db).Notify(
                            "Technician", originalAssignment.TechnicianId.Value,
                            "Repair Completion Rejected",
                            $"Your completed repair on \"{repair.Equipment?.Name}\" was rejected by admin — reason: {reason.Trim()}. Please take another look.",
                            "Warning",
                            "/Technician/MyRepairs");
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }
            }

            db.Entry(repair).State = EntityState.Modified;
            db.SaveChanges();

            TempData["Success"] = $"Rejected — {repair.Equipment?.Name} sent back for further work.";
            return RedirectToAction("Index", "Equipment");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}