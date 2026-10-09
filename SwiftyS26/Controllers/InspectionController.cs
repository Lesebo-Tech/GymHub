using SwiftyS26.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace SwiftyS26.Controllers
{
    public class InspectionController : Controller
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
        // ASSIGN INSPECTION (ROUTINE) — POST
        //
        // Tasks a technician with an equipment's upcoming/overdue routine
        // inspection, WITHOUT recording any outcome yet. Splits "who's
        // doing this" from "what happened" — the same distinction we
        // already made on the repair side.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssignInspection(int equipmentId, int technicianId)
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

            equipment.PendingInspectionTechnicianId = technicianId;
            equipment.PendingInspectionAssignedDate = DateTime.Now;
            db.Entry(equipment).State = EntityState.Modified;
            db.SaveChanges();

            try
            {
                new NotificationService(db).Notify(
                    "Technician", technicianId,
                    "Inspection Assigned To You",
                    $"You've been assigned to inspect \"{equipment.Name}\" ({equipment.EquipmentCode}).",
                    "Info",
                    "/Technician/MyRepairs");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }

            TempData["Success"] = $"{technician.FullName} assigned to inspect {equipment.Name}.";
            return RedirectToAction("Index", "Equipment");
        }

        // ============================================================
        // ASSIGN VERIFICATION (POST-REPAIR / VENDOR) — POST
        //
        // Same split, applied to a repair sitting in AwaitingInspection.
        // Enforces the different-technician rule here too — no point
        // letting the admin assign the repairing technician as their own
        // verifier only to have it rejected later at Perform time.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssignVerification(int repairId, int technicianId)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            var repair = db.Repairs.Include(r => r.Equipment).FirstOrDefault(r => r.RepairId == repairId);
            if (repair == null)
            {
                TempData["Error"] = "Repair not found.";
                return RedirectToAction("Index", "Equipment");
            }

            if (repair.RepairStatus != "AwaitingInspection")
            {
                TempData["Error"] = "This repair is not currently awaiting inspection.";
                return RedirectToAction("Index", "Equipment");
            }

            var technician = db.Technicians.Find(technicianId);
            if (technician == null || !technician.IsActive)
            {
                TempData["Error"] = "Technician not found or inactive.";
                return RedirectToAction("Index", "Equipment");
            }

            // ── Same rule as PerformInspection: a technician-repaired item
            // (VendorId null) can't be verified by the technician who fixed
            // it. A vendor repair (VendorId set) has no such restriction,
            // even if TechnicianId is a stale value from before escalation. ──
            bool isVendorRepair = repair.VendorId != null;
            if (!isVendorRepair && repair.TechnicianId.HasValue && repair.TechnicianId.Value == technicianId)
            {
                TempData["Error"] = "The technician who performed this repair cannot also be assigned to verify it.";
                return RedirectToAction("Index", "Equipment");
            }

            repair.VerificationTechnicianId = technicianId;
            repair.VerificationAssignedDate = DateTime.Now;
            db.Entry(repair).State = EntityState.Modified;
            db.SaveChanges();

            try
            {
                new NotificationService(db).Notify(
                    "Technician", technicianId,
                    "Repair Verification Assigned To You",
                    $"You've been assigned to verify a completed repair on \"{repair.Equipment?.Name}\".",
                    "Info",
                    "/Technician/MyRepairs");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }

            TempData["Success"] = $"{technician.FullName} assigned to verify the repair on {repair.Equipment?.Name}.";
            return RedirectToAction("Index", "Equipment");
        }

        // ============================================================
        // PERFORM INSPECTION — POST
        //
        // Now callable by BOTH admin and technician, not admin-only.
        //
        // When a technician submits this, two extra rules apply on top
        // of everything below:
        //   1. They can only submit results under their OWN TechnicianId
        //      (can't record someone else's name against an inspection).
        //   2. The inspection/verification must actually be assigned TO
        //      them (PendingInspectionTechnicianId / VerificationTechnicianId
        //      must match) — they can't just perform any due inspection
        //      that happens to exist, only ones handed to them.
        // Admins remain unrestricted, matching existing behavior.
        //
        // Handles TWO distinct flows depending on whether repairId is supplied:
        //
        //  ROUTINE  (repairId == null): the existing scheduled-inspection flow.
        //           Fail auto-creates a new Repair, exactly as before.
        //
        //  POST-REPAIR (repairId != null): verifies a completed repair
        //           (Repair.RepairStatus == "AwaitingInspection"). The
        //           technician who performed the repair may NOT also verify
        //           it — enforced here even if the UI dropdown filtering
        //           was bypassed. Pass -> Repair Resolved, equipment
        //           Available. Fail -> Repair Reopened, equipment stays
        //           OutOfService, NO new Repair is created (avoids
        //           duplicating the fault record).
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PerformInspection(int equipmentId, int? technicianId, string result, string findings, string actionTaken, int? repairId)
        {
            bool isAdmin = IsAdminLoggedIn();
            bool isTechnician = !isAdmin && IsTechnicianLoggedIn();

            if (!isAdmin && !isTechnician)
                return RedirectToAction("Login", "Admin");

            // Where errors/success redirect to depends on who's submitting —
            // admins land back on Equipment Management, technicians on their
            // own inspections list.
            string redirectAction = isTechnician ? "MyInspections" : "Index";
            string redirectController = isTechnician ? "Technician" : "Equipment";

            Technician actingTechnician = null;
            if (isTechnician)
            {
                actingTechnician = GetLoggedInTechnician();
                if (actingTechnician == null)
                    return RedirectToAction("Login", "Admin");

                // A technician can only ever submit results as themselves
                if (!technicianId.HasValue || technicianId.Value != actingTechnician.TechnicianId)
                {
                    TempData["Error"] = "You can only submit inspection results under your own name.";
                    return RedirectToAction(redirectAction, redirectController);
                }
            }

            var equipment = db.Equipment.Find(equipmentId);
            if (equipment == null)
            {
                TempData["Error"] = "Equipment not found.";
                return RedirectToAction(redirectAction, redirectController);
            }

            if (result != "Pass" && result != "Fail")
            {
                TempData["Error"] = "Inspection result must be Pass or Fail.";
                return RedirectToAction(redirectAction, redirectController);
            }

            // ── If this is a post-repair verification, load and validate the repair ──
            Repair repairBeingVerified = null;
            if (repairId.HasValue)
            {
                repairBeingVerified = db.Repairs.Find(repairId.Value);

                if (repairBeingVerified == null)
                {
                    TempData["Error"] = "The repair this inspection is meant to verify could not be found.";
                    return RedirectToAction(redirectAction, redirectController);
                }

                if (repairBeingVerified.RepairStatus != "AwaitingInspection")
                {
                    TempData["Error"] = "This repair is not currently awaiting inspection.";
                    return RedirectToAction(redirectAction, redirectController);
                }

                // ── SERVER-SIDE ENFORCEMENT: the technician who performed the
                // repair cannot also perform its verification inspection.
                // This backstops the UI dropdown filtering — never trust the
                // client alone for a rule like this. ──
                if (technicianId.HasValue
                    && repairBeingVerified.TechnicianId.HasValue
                    && technicianId.Value == repairBeingVerified.TechnicianId.Value)
                {
                    TempData["Error"] = "The technician who performed this repair cannot also perform the verification inspection. Please select a different technician.";
                    return RedirectToAction(redirectAction, redirectController);
                }

                // ── Technician-only: this verification must actually be
                // assigned to them, not just any AwaitingInspection repair ──
                if (isTechnician && repairBeingVerified.VerificationTechnicianId != actingTechnician.TechnicianId)
                {
                    TempData["Error"] = "This verification isn't assigned to you.";
                    return RedirectToAction(redirectAction, redirectController);
                }
            }
            else if (isTechnician)
            {
                // ── Technician-only: this routine inspection must actually
                // be assigned to them ──
                if (equipment.PendingInspectionTechnicianId != actingTechnician.TechnicianId)
                {
                    TempData["Error"] = "This inspection isn't assigned to you.";
                    return RedirectToAction(redirectAction, redirectController);
                }
            }

            var inspection = new Inspection
            {
                EquipmentId = equipmentId,
                TechnicianId = technicianId,
                InspectionDate = DateTime.Now,
                Result = result,
                Findings = findings,
                ActionTaken = actionTaken,
                RepairId = repairId,
                InspectionType = repairId.HasValue ? "PostRepair" : "Routine"
            };
            db.Inspections.Add(inspection);

            Repair newRepair = null;

            if (repairBeingVerified != null)
            {
                // ══════════════════════════════════════════════════════
                // POST-REPAIR BRANCH
                // ══════════════════════════════════════════════════════
                if (result == "Pass")
                {
                    // ── CHANGED: a passed inspection no longer finalizes the
                    // repair on its own. It stops at AwaitingApproval — an
                    // admin has to explicitly sign off before the equipment
                    // is trusted back into service. Equipment status is
                    // deliberately left untouched here (still OutOfService)
                    // until that approval happens. ──
                    repairBeingVerified.RepairStatus = "AwaitingApproval";

                    try
                    {
                        new NotificationService(db).NotifyAdmin(
                            "Inspection Passed — Awaiting Your Approval",
                            $"\"{equipment.Name}\" passed its post-repair inspection. Review and approve before it returns to service.",
                            "Info",
                            "/Equipment/Index");
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }

                    // Member notification deliberately deferred until the
                    // admin actually approves — telling them it's fixed
                    // before that would be premature.
                }
                else // Fail — verification failed, do NOT create a new Repair
                {
                    repairBeingVerified.RepairStatus = "Reopened";
                    equipment.Status = "OutOfService";

                    // Keep the technician's assignment history accurate — a
                    // "Completed" repair that failed post-repair verification
                    // isn't really complete. Without this, RepairHistory would
                    // permanently show a job as Completed even though it was
                    // rejected on inspection.
                    var originalAssignment = db.RepairAssignments
                        .Where(a => a.RepairId == repairBeingVerified.RepairId && a.AssignmentStatus == "Completed")
                        .OrderByDescending(a => a.CompletedDate)
                        .FirstOrDefault();
                    if (originalAssignment != null)
                    {
                        originalAssignment.AssignmentStatus = "Reopened";
                        db.Entry(originalAssignment).State = EntityState.Modified;
                    }

                    try
                    {
                        new NotificationService(db).NotifyAdmin(
                            "Post-Repair Inspection Failed",
                            $"\"{equipment.Name}\" was verified after repair and the issue is still present — repair reopened and needs another look.",
                            "Alert",
                            "/Equipment/Index");
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }
                }

                // A physical inspection did take place, so the routine schedule still advances
                equipment.LastInspectionDate = DateTime.Today;
                equipment.NextInspectionDueDate = DateTime.Today.AddDays(equipment.InspectionIntervalDays);

                // ── Clear the verification assignment — this attempt is
                // resolved either way. If Reopened, a fresh MarkComplete
                // cycle will require a brand-new AssignVerification. ──
                repairBeingVerified.VerificationTechnicianId = null;
                repairBeingVerified.VerificationAssignedDate = null;

                db.Entry(repairBeingVerified).State = EntityState.Modified;
            }
            else
            {
                // ══════════════════════════════════════════════════════
                // ROUTINE BRANCH — unchanged behavior
                // ══════════════════════════════════════════════════════
                equipment.LastInspectionDate = DateTime.Today;
                equipment.NextInspectionDueDate = DateTime.Today.AddDays(equipment.InspectionIntervalDays);

                // ── Clear the routine assignment — the next due date will
                // need its own fresh AssignInspection when it comes around ──
                equipment.PendingInspectionTechnicianId = null;
                equipment.PendingInspectionAssignedDate = null;

                if (result == "Pass")
                {
                    if (equipment.Status == "OutOfService")
                        equipment.Status = "Available";
                }
                else // Fail
                {
                    equipment.Status = "OutOfService";

                    newRepair = new Repair
                    {
                        EquipmentId = equipmentId,
                        FaultDescription = string.IsNullOrEmpty(findings) ? "Failed inspection — no findings noted." : findings,
                        RepairStatus = "Pending",
                        ReportedDate = DateTime.Now,
                        ReportedBySource = "Inspection",
                        Category = "Other",
                        Severity = "Critical",
                        ReferenceNumber = RepairHelper.GenerateReferenceNumber(db)
                    };
                    db.Repairs.Add(newRepair);
                }
            }

            db.Entry(equipment).State = EntityState.Modified;
            db.SaveChanges(); // saves inspection + equipment (+ repair) together; IDs now exist

            if (newRepair != null)
            {
                newRepair.InspectionId = inspection.InspectionId;
                db.Entry(newRepair).State = EntityState.Modified;
                db.SaveChanges();

                try
                {
                    new NotificationService(db).NotifyAdmin(
                        "New Repair From Failed Inspection",
                        $"\"{equipment.Name}\" failed routine inspection — repair {newRepair.ReferenceNumber} logged and awaiting assignment.",
                        "Alert",
                        "/Equipment/Index");
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }
            }

            string successMsg = repairBeingVerified != null
                ? $"Post-repair inspection recorded for {equipment.Name} — " +
                  (result == "Pass"
                      ? "verified and back in service."
                      : "issue still present, repair reopened for reassignment.")
                : $"Inspection recorded for {equipment.Name} ({equipment.EquipmentCode}) — Result: {result}.";

            TempData["Success"] = successMsg;
            return RedirectToAction(redirectAction, redirectController);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}