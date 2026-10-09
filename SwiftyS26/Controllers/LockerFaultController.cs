using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using SwiftyS26.Models;
using System.Data.Entity;


namespace SwiftyS26.Controllers
{
    public class LockerFaultController : Controller
    {
        private GymHubDB db = new GymHubDB();

        private const int AdminId = 1; // matches AdminController's single-admin constant

        private bool IsAdminLoggedIn() => Session["LoggedInAdmin"] != null;
        private bool IsMemberLoggedIn() => Session["LoggedInMember"] != null;
        private bool IsTechnicianLoggedIn() => Session["LoggedInTechnician"] != null;

        private Technician GetLoggedInTechnician()
        {
            if (!IsTechnicianLoggedIn()) return null;
            int id = (int)Session["LoggedInTechnician"];
            return db.Technicians.Find(id);
        }

        // ============================================================
        // REPORT FAULT — POST (member or admin)
        //
        // Deliberately does NOT take the locker out of service. A member's
        // report is a SIGNAL; only admin triage changes system state. This
        // is what stops any member disabling any locker with a text box.
        //
        // Duplicate handling: if an open fault already exists for this
        // locker, this report attaches to it as an additional reporter
        // rather than creating a second fault. Three people reporting one
        // jammed door is one problem with three witnesses.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Report(int lockerId, string faultType, string severity,
            string description, HttpPostedFileBase photoFile)
        {
            bool isAdmin = IsAdminLoggedIn();
            bool isMember = !isAdmin && IsMemberLoggedIn();

            if (!isAdmin && !isMember)
                return RedirectToAction("Login", "Forms");

            string redirectAction = isMember ? "Index" : "ManageLockers";
            string redirectController = isMember ? "Locker" : "Admin";

            // ── Validation ────────────────────────────────────────────
            if (!LockerFaultHelper.IsValidFaultType(faultType))
            {
                TempData["Error"] = "Please select a valid fault type.";
                return RedirectToAction(redirectAction, redirectController);
            }

            if (!LockerFaultHelper.IsValidSeverity(severity))
            {
                TempData["Error"] = "Please select how serious the problem is.";
                return RedirectToAction(redirectAction, redirectController);
            }

            if (string.IsNullOrWhiteSpace(description) || description.Trim().Length < 10)
            {
                TempData["Error"] = "Please describe the problem (at least 10 characters).";
                return RedirectToAction(redirectAction, redirectController);
            }

            var locker = db.Lockers.Find(lockerId);
            if (locker == null)
            {
                TempData["Error"] = "Locker not found.";
                return RedirectToAction(redirectAction, redirectController);
            }

            int? memberId = isMember ? (int?)Session["LoggedInMember"] : null;

            // ── Is this reporter currently holding the locker? ────────
            // Strongest available signal that belongings are trapped.
            bool wasHolding = false;
            if (memberId.HasValue)
            {
                wasHolding = db.LockerReservations.Any(r =>
                    r.LockerId == lockerId && r.IsActive && r.MemberId == memberId.Value);
            }

            // ── Photo upload — same pattern as equipment fault reports ─
            string photoPath = null;
            if (photoFile != null && photoFile.ContentLength > 0)
            {
                var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var ext = System.IO.Path.GetExtension(photoFile.FileName).ToLower();

                if (!allowedExt.Contains(ext))
                {
                    TempData["Error"] = "Only JPG, PNG or WEBP images are allowed.";
                    return RedirectToAction(redirectAction, redirectController);
                }

                if (photoFile.ContentLength > 5 * 1024 * 1024)
                {
                    TempData["Error"] = "Photo must be smaller than 5MB.";
                    return RedirectToAction(redirectAction, redirectController);
                }

                try
                {
                    string folder = Server.MapPath("~/Uploads/LockerFaults");
                    if (!System.IO.Directory.Exists(folder))
                        System.IO.Directory.CreateDirectory(folder);

                    string safeName = $"{Guid.NewGuid()}{ext}";
                    photoFile.SaveAs(System.IO.Path.Combine(folder, safeName));
                    photoPath = $"/Uploads/LockerFaults/{safeName}";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UPLOAD ERROR] {ex.Message}");
                    TempData["Error"] = "Problem uploading the photo. Please try again.";
                    return RedirectToAction(redirectAction, redirectController);
                }
            }

            // ── Deduplicate: attach to any existing open fault ────────
            var existingFault = db.LockerFaults
                .Where(f => f.LockerId == lockerId
                         && LockerFaultHelper.OpenStatuses.Contains(f.Status))
                .OrderByDescending(f => f.ReportedDate)
                .FirstOrDefault();

            bool isNewFault = existingFault == null;
            LockerFault fault;

            if (isNewFault)
            {
                fault = new LockerFault
                {
                    LockerId = lockerId,
                    ReferenceNumber = LockerFaultHelper.GenerateReferenceNumber(db),
                    FaultType = faultType,
                    ReportedSeverity = severity,
                    Status = "Reported",
                    ReportedDate = DateTime.Now,
                    BelongingsInside = wasHolding
                };
                db.LockerFaults.Add(fault);

                // Flag the locker as reported-faulty. This is VISIBLE to
                // admin but does not block members — triage decides that.
                if (locker.OperationalStatus == "InService")
                {
                    locker.OperationalStatus = "ReportedFaulty";
                    db.Entry(locker).State = EntityState.Modified;
                }
            }
            else
            {
                fault = existingFault;

                // A later reporter may reveal that belongings ARE trapped
                // even if the first reporter didn't hold the locker.
                if (wasHolding && !fault.BelongingsInside)
                {
                    fault.BelongingsInside = true;
                    db.Entry(fault).State = EntityState.Modified;
                }

                // Escalate the reported severity if this reporter says it's
                // worse — but never downgrade it.
                if (severity == "Critical" && fault.ReportedSeverity != "Critical")
                {
                    fault.ReportedSeverity = "Critical";
                    db.Entry(fault).State = EntityState.Modified;
                }
                else if (severity == "Major" && fault.ReportedSeverity == "Minor")
                {
                    fault.ReportedSeverity = "Major";
                    db.Entry(fault).State = EntityState.Modified;
                }
            }

            db.SaveChanges(); // fault.LockerFaultId now exists

            // Prevent one member spamming the same fault repeatedly
            if (!isNewFault && memberId.HasValue &&
                db.LockerFaultReports.Any(r => r.LockerFaultId == fault.LockerFaultId && r.MemberId == memberId.Value))
            {
                TempData["Success"] = $"You've already reported this locker — it's logged as {fault.ReferenceNumber} and being looked at.";
                return RedirectToAction(redirectAction, redirectController);
            }

            var report = new LockerFaultReport
            {
                LockerFaultId = fault.LockerFaultId,
                MemberId = memberId,
                ReportedBySource = isMember ? "Member" : "Admin",
                Description = description.Trim(),
                ReportedSeverity = severity,
                PhotoPath = photoPath,
                WasHoldingLocker = wasHolding,
                ReportedDate = DateTime.Now
            };
            db.LockerFaultReports.Add(report);
            db.SaveChanges();

            // ── Notify admin — urgency mirrors the reported severity ──
            if (isMember)
            {
                try
                {
                    int reportCount = db.LockerFaultReports.Count(r => r.LockerFaultId == fault.LockerFaultId);
                    string prefix = severity == "Critical" ? "CRITICAL: " : "";
                    string dupe = isNewFault ? "" : $" ({reportCount} reports on this fault)";
                    string trapped = fault.BelongingsInside ? " Member belongings may be inside." : "";

                    new NotificationService(db).NotifyAdmin(
                        isNewFault ? "Locker Fault Reported" : "Additional Locker Fault Report",
                        $"{prefix}Locker {locker.LockerNumber} — {fault.FaultTypeDisplay}. Ref {fault.ReferenceNumber}.{trapped}{dupe} Awaiting triage.",
                        LockerFaultHelper.NotificationTypeForSeverity(severity),
                        "/Admin/ManageLockers");
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }

                // Confirm to the reporting member
                try
                {
                    new NotificationService(db).Notify(
                        "Member", memberId.Value,
                        "Locker Fault Reported",
                        $"Thanks — locker {locker.LockerNumber} has been logged as {fault.ReferenceNumber}. We'll let you know once it's fixed." +
                            (fault.BelongingsInside ? " If your belongings are inside, please speak to reception." : ""),
                        "Info",
                        "/Locker/Index");
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }
            }

            TempData["Success"] = isNewFault
                ? $"Thanks — locker {locker.LockerNumber} has been reported. Reference: {fault.ReferenceNumber}."
                : $"Thanks — your report was added to existing fault {fault.ReferenceNumber}.";

            return RedirectToAction(redirectAction, redirectController);
        }

        // ============================================================
        // TRIAGE — POST (admin only)
        //
        // The authoritative decision point. Admin sets the REAL severity
        // (which may differ from what the member claimed) and that decision
        // — not the member's — determines whether the locker comes out of
        // service.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Triage(int lockerFaultId, string severity, string triageNotes, bool belongingsInside = false)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            if (!LockerFaultHelper.IsValidSeverity(severity))
            {
                TempData["Error"] = "Please select a valid severity.";
                return RedirectToAction("ManageLockers", "Admin");
            }

            var fault = db.LockerFaults
                .Include(f => f.Locker)
                .FirstOrDefault(f => f.LockerFaultId == lockerFaultId);

            if (fault == null)
            {
                TempData["Error"] = "Fault not found.";
                return RedirectToAction("ManageLockers", "Admin");
            }

            if (fault.Status != "Reported")
            {
                TempData["Error"] = "This fault has already been triaged.";
                return RedirectToAction("ManageLockers", "Admin");
            }

            fault.Severity = severity;
            fault.Status = "Triaged";
            fault.TriagedDate = DateTime.Now;
            fault.TriagedByAdminId = AdminId;
            fault.TriageNotes = triageNotes;
            fault.BelongingsInside = belongingsInside;

            // Admin's severity drives operational state — Minor faults leave
            // the locker in service rather than costing the gym a unit.
            var locker = fault.Locker;
            locker.OperationalStatus = LockerFaultHelper.OperationalStatusForSeverity(severity);

            db.Entry(fault).State = EntityState.Modified;
            db.Entry(locker).State = EntityState.Modified;
            db.SaveChanges();

            // ── Tell the holder their locker is now out of service ────
            if (locker.OperationalStatus == "OutOfService")
            {
                var holder = db.LockerReservations
                    .FirstOrDefault(r => r.LockerId == locker.LockerId && r.IsActive);

                if (holder != null)
                {
                    try
                    {
                        new NotificationService(db).Notify(
                            "Member", holder.MemberId,
                            "Your Locker Is Out of Service",
                            $"Locker {locker.LockerNumber} has been taken out of service for repair (ref {fault.ReferenceNumber}). " +
                                (fault.BelongingsInside
                                    ? "Please speak to reception about retrieving your belongings."
                                    : "Please collect your belongings and speak to reception."),
                            "Warning",
                            "/Locker/Index");
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }
                }
            }

            TempData["Success"] = severity == "Minor"
                ? $"Fault {fault.ReferenceNumber} triaged as Minor — locker {locker.LockerNumber} stays in service."
                : $"Fault {fault.ReferenceNumber} triaged as {severity} — locker {locker.LockerNumber} is now out of service.";

            return RedirectToAction("ManageLockers", "Admin");
        }

        // ============================================================
        // ASSIGN TECHNICIAN — POST (admin only)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Assign(int lockerFaultId, int technicianId)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            var fault = db.LockerFaults
                .Include(f => f.Locker)
                .FirstOrDefault(f => f.LockerFaultId == lockerFaultId);

            if (fault == null)
            {
                TempData["Error"] = "Fault not found.";
                return RedirectToAction("ManageLockers", "Admin");
            }

            if (fault.Status != "Triaged" && fault.Status != "Assigned")
            {
                TempData["Error"] = "This fault must be triaged before it can be assigned.";
                return RedirectToAction("ManageLockers", "Admin");
            }

            var technician = db.Technicians.Find(technicianId);
            if (technician == null || !technician.IsActive)
            {
                TempData["Error"] = "Technician not found or inactive.";
                return RedirectToAction("ManageLockers", "Admin");
            }

            bool isReassignment = fault.TechnicianId.HasValue && fault.TechnicianId != technicianId;

            fault.TechnicianId = technicianId;
            fault.AssignedDate = DateTime.Now;
            fault.Status = "Assigned";

            db.Entry(fault).State = EntityState.Modified;
            db.SaveChanges();

            try
            {
                new NotificationService(db).Notify(
                    "Technician", technicianId,
                    isReassignment ? "Locker Fault Reassigned To You" : "Locker Fault Assigned To You",
                    $"{fault.EffectiveSeverity} — locker {fault.Locker?.LockerNumber}: {fault.FaultTypeDisplay}. Ref {fault.ReferenceNumber}.",
                    LockerFaultHelper.NotificationTypeForSeverity(fault.EffectiveSeverity),
                    "/Technician/MyRepairs");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }

            TempData["Success"] = $"Fault {fault.ReferenceNumber} assigned to {technician.FullName}.";
            return RedirectToAction("ManageLockers", "Admin");
        }

        // ============================================================
        // START WORK — POST (technician, own faults only)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Start(int lockerFaultId)
        {
            if (!IsTechnicianLoggedIn())
                return RedirectToAction("Login", "Admin");

            var technician = GetLoggedInTechnician();
            if (technician == null)
                return RedirectToAction("Login", "Admin");

            var fault = db.LockerFaults
                .Include(f => f.Locker)
                .FirstOrDefault(f => f.LockerFaultId == lockerFaultId && f.TechnicianId == technician.TechnicianId);

            if (fault == null)
            {
                TempData["Error"] = "That fault isn't assigned to you.";
                return RedirectToAction("MyRepairs", "Technician");
            }

            if (fault.Status != "Assigned")
            {
                TempData["Error"] = "This fault can't be started from its current status.";
                return RedirectToAction("MyRepairs", "Technician");
            }

            fault.Status = "InProgress";
            fault.StartedDate = DateTime.Now;

            var locker = fault.Locker;
            locker.OperationalStatus = "UnderRepair";

            db.Entry(fault).State = EntityState.Modified;
            db.Entry(locker).State = EntityState.Modified;
            db.SaveChanges();

            TempData["Success"] = $"Work started on locker {locker.LockerNumber}.";
            return RedirectToAction("MyRepairs", "Technician");
        }

        // ============================================================
        // RESOLVE — POST (technician or admin)
        //
        // On success the locker returns to service and EVERY reporter is
        // notified — not just whoever reported it first. That's the payoff
        // of modelling reports as their own rows.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Resolve(int lockerFaultId, string outcome, string resolutionNotes,
            string partsUsed, decimal? repairCost)
        {
            bool isAdmin = IsAdminLoggedIn();
            bool isTechnician = !isAdmin && IsTechnicianLoggedIn();

            if (!isAdmin && !isTechnician)
                return RedirectToAction("Login", "Admin");

            string redirectAction = isTechnician ? "MyRepairs" : "ManageLockers";
            string redirectController = isTechnician ? "Technician" : "Admin";

            if (outcome != "Resolved" && outcome != "Unrepairable")
            {
                TempData["Error"] = "Outcome must be Resolved or Unrepairable.";
                return RedirectToAction(redirectAction, redirectController);
            }

            if (string.IsNullOrWhiteSpace(resolutionNotes))
            {
                TempData["Error"] = "Please record what was done.";
                return RedirectToAction(redirectAction, redirectController);
            }

            var query = db.LockerFaults.Include(f => f.Locker).AsQueryable();

            LockerFault fault;
            if (isTechnician)
            {
                var technician = GetLoggedInTechnician();
                if (technician == null) return RedirectToAction("Login", "Admin");
                fault = query.FirstOrDefault(f => f.LockerFaultId == lockerFaultId && f.TechnicianId == technician.TechnicianId);

                if (fault == null)
                {
                    TempData["Error"] = "That fault isn't assigned to you.";
                    return RedirectToAction(redirectAction, redirectController);
                }
            }
            else
            {
                fault = query.FirstOrDefault(f => f.LockerFaultId == lockerFaultId);
                if (fault == null)
                {
                    TempData["Error"] = "Fault not found.";
                    return RedirectToAction(redirectAction, redirectController);
                }
            }

            if (!fault.IsOpen)
            {
                TempData["Error"] = "This fault is already closed.";
                return RedirectToAction(redirectAction, redirectController);
            }

            fault.Status = outcome;
            fault.ResolutionNotes = resolutionNotes.Trim();
            fault.PartsUsed = partsUsed;
            fault.RepairCost = repairCost;
            fault.ResolvedDate = DateTime.Now;

            var locker = fault.Locker;

            if (outcome == "Resolved")
            {
                locker.OperationalStatus = "InService";
            }
            else
            {
                // Beyond repair — retire it and free any reservation, since
                // the unit is never coming back.
                locker.OperationalStatus = "Retired";

                var holder = db.LockerReservations
                    .FirstOrDefault(r => r.LockerId == locker.LockerId && r.IsActive);
                if (holder != null)
                {
                    holder.IsActive = false;
                    holder.EndedAt = DateTime.Now;
                    holder.AccessCodeHash = null;
                    holder.WasForceReleased = true;
                    holder.ForceReleaseReason = $"Locker retired — unrepairable (ref {fault.ReferenceNumber})";
                    db.Entry(holder).State = EntityState.Modified;
                    locker.IsOccupied = false;
                }
            }

            db.Entry(fault).State = EntityState.Modified;
            db.Entry(locker).State = EntityState.Modified;
            db.SaveChanges();

            // ── Notify every member who reported this fault ───────────
            var reporterIds = db.LockerFaultReports
                .Where(r => r.LockerFaultId == fault.LockerFaultId && r.MemberId != null)
                .Select(r => r.MemberId.Value)
                .Distinct()
                .ToList();

            foreach (var mid in reporterIds)
            {
                try
                {
                    new NotificationService(db).Notify(
                        "Member", mid,
                        outcome == "Resolved" ? "Locker Fixed" : "Locker Retired",
                        outcome == "Resolved"
                            ? $"Locker {locker.LockerNumber} has been repaired and is back in service. Thanks for reporting it."
                            : $"Locker {locker.LockerNumber} could not be repaired and has been retired. Thanks for reporting it.",
                        outcome == "Resolved" ? "Success" : "Info",
                        "/Locker/Index");
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }
            }

            if (isTechnician)
            {
                try
                {
                    new NotificationService(db).NotifyAdmin(
                        outcome == "Resolved" ? "Locker Fault Resolved" : "Locker Marked Unrepairable",
                        $"Locker {locker.LockerNumber} ({fault.ReferenceNumber}) — {outcome.ToLower()} by {fault.Technician?.FullName ?? "technician"}.",
                        outcome == "Resolved" ? "Success" : "Warning",
                        "/Admin/ManageLockers");
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }
            }

            TempData["Success"] = outcome == "Resolved"
                ? $"Locker {locker.LockerNumber} is back in service."
                : $"Locker {locker.LockerNumber} marked unrepairable and retired.";

            return RedirectToAction(redirectAction, redirectController);
        }

        // ============================================================
        // OVERRIDE OPEN — POST (admin only)
        //
        // Staff opening a locker a member still holds. This is NOT the same
        // as Force Release: it opens the locker for belongings retrieval
        // WITHOUT ending the member's reservation or destroying their PIN.
        //
        // Constrained deliberately:
        //   • only via a fault flagged BelongingsInside
        //   • mandatory reason + acknowledgement
        //   • immutable audit row, separate from the fault
        //   • member ALWAYS notified, unconditionally
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult OverrideOpen(int lockerFaultId, string reason, string acknowledgement)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "A reason is required to open a member's locker.";
                return RedirectToAction("ManageLockers", "Admin");
            }

            if (string.IsNullOrWhiteSpace(acknowledgement))
            {
                TempData["Error"] = "Please confirm how the member was informed.";
                return RedirectToAction("ManageLockers", "Admin");
            }

            var fault = db.LockerFaults
                .Include(f => f.Locker)
                .FirstOrDefault(f => f.LockerFaultId == lockerFaultId);

            if (fault == null)
            {
                TempData["Error"] = "Fault not found.";
                return RedirectToAction("ManageLockers", "Admin");
            }

            // Gate: override is only legitimate when belongings are trapped
            if (!fault.BelongingsInside)
            {
                TempData["Error"] = "Override is only available for faults where member belongings are inside.";
                return RedirectToAction("ManageLockers", "Admin");
            }

            var reservation = db.LockerReservations
                .Include(r => r.Member)
                .FirstOrDefault(r => r.LockerId == fault.LockerId && r.IsActive);

            if (reservation == null)
            {
                TempData["Error"] = "This locker has no active reservation — no override needed.";
                return RedirectToAction("ManageLockers", "Admin");
            }

            var auditRecord = new LockerAccessOverride
            {
                LockerId = fault.LockerId,
                ReservationId = reservation.ReservationId,
                MemberId = reservation.MemberId,
                LockerFaultId = fault.LockerFaultId,
                PerformedByAdminId = AdminId,
                Reason = reason.Trim(),
                Acknowledgement = acknowledgement.Trim(),
                PerformedAt = DateTime.Now,
                IpAddress = Request.UserHostAddress,
                MemberNotified = false
            };
            db.LockerAccessOverrides.Add(auditRecord);
            db.SaveChanges();

            // The member is told unconditionally — there is no silent access.
            bool notified = false;
            try
            {
                new NotificationService(db).Notify(
                    "Member", reservation.MemberId,
                    "Your Locker Was Opened by Staff",
                    $"Locker {fault.Locker?.LockerNumber} was opened by gym staff on {auditRecord.PerformedAt:dd MMM yyyy HH:mm}. " +
                        $"Reason: {auditRecord.Reason}. Your reservation is still active — please speak to reception.",
                    "Alert",
                    "/Locker/Index");
                notified = true;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }

            if (notified)
            {
                auditRecord.MemberNotified = true;
                db.Entry(auditRecord).State = EntityState.Modified;
                db.SaveChanges();
            }

            TempData["Success"] = $"Locker {fault.Locker?.LockerNumber} opened. {reservation.Member?.FullName ?? "The member"} has been notified and the action logged.";
            return RedirectToAction("ManageLockers", "Admin");
        }

        // ============================================================
        // AUDIT LOG — GET (admin only)
        // Standalone view of every override ever performed.
        // ============================================================
        public ActionResult AccessAudit()
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            ViewBag.PageTitle = "LOCKER ACCESS AUDIT";
            ViewBag.ActiveNav = "lockers";

            var overrides = db.LockerAccessOverrides
                .Include(o => o.Locker)
                .Include(o => o.Member)
                .Include(o => o.LockerFault)
                .OrderByDescending(o => o.PerformedAt)
                .Take(200)
                .ToList();

            return View(overrides);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}