using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using BCrypt.Net;
using SwiftyS26.Models;

namespace SwiftyS26.Controllers
{
    public class LockerController : Controller
    {
        private GymHubDB db = new GymHubDB();

        // GET: Locker — lockers matching the member's gender, plus their own history
        public ActionResult Index()
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            int currentMemberId = (int)Session["LoggedInMember"];
            var member = db.Members.Find(currentMemberId);
            if (member == null)
                return RedirectToAction("Login", "Forms");

            // Retired lockers are gone for good — everything else is shown,
            // including out-of-service ones, so members can see WHY a locker
            // they expected to use isn't available rather than it silently
            // vanishing from the grid.
            var lockers = db.Lockers
                .Where(l => l.Gender == member.Gender && l.OperationalStatus != "Retired")
                .OrderBy(l => l.LockerNumber)
                .ToList();

            var myActiveReservation = db.LockerReservations
                .Where(r => r.MemberId == currentMemberId && r.IsActive)
                .FirstOrDefault();

            ViewBag.MyActiveReservationLockerId = myActiveReservation?.LockerId;
            ViewBag.MyActiveReservation = myActiveReservation;

            // ── History tab: this member's past (ended) reservations ──
            ViewBag.MyHistory = db.LockerReservations
                .Include(r => r.Locker)
                .Where(r => r.MemberId == currentMemberId && !r.IsActive)
                .OrderByDescending(r => r.EndedAt)
                .Take(50)
                .ToList();

            // ── Which lockers already have an open fault logged ───────
            // Used by the view to show "already reported" instead of
            // inviting a duplicate report.
            ViewBag.OpenFaultLockerIds = db.LockerFaults
                .Where(f => LockerFaultHelper.OpenStatuses.Contains(f.Status))
                .Select(f => f.LockerId)
                .Distinct()
                .ToList();

            return View(lockers);
        }

        // GET: Locker/Reserve/5
        public ActionResult Reserve(int? id)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            if (!id.HasValue)
            {
                TempData["Error"] = "No locker was selected. Please choose a locker first.";
                return RedirectToAction("Index");
            }

            int lockerId = id.Value;
            int currentMemberId = (int)Session["LoggedInMember"];

            var member = db.Members.Find(currentMemberId);
            if (member == null)
                return RedirectToAction("Login", "Forms");

            var locker = db.Lockers.Find(lockerId);
            if (locker == null)
            {
                TempData["Error"] = "Locker not found.";
                return RedirectToAction("Index");
            }

            if (locker.Gender != member.Gender)
            {
                TempData["Error"] = "This locker is not available for your account.";
                return RedirectToAction("Index");
            }

            // IsReservable covers BOTH axes: free of a reservation AND
            // operationally healthy. A locker can fail either independently.
            if (!locker.IsReservable)
            {
                TempData["Error"] = locker.OperationalStatus != "InService"
                    ? $"Locker {locker.LockerNumber} is currently out of service. Please choose another locker."
                    : "This locker is currently unavailable. Please choose another locker.";
                return RedirectToAction("Index");
            }

            var existingReservation = db.LockerReservations
                .Where(r => r.MemberId == currentMemberId && r.IsActive)
                .FirstOrDefault();

            if (existingReservation != null)
            {
                TempData["Error"] = "You already have an active locker reservation. Please end it before reserving another.";
                return RedirectToAction("Unlock", new { id = existingReservation.LockerId });
            }

            return View(locker);
        }

        // POST: Locker/Reserve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reserve(int? id, string pin, string confirmPin)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            if (!id.HasValue)
            {
                TempData["Error"] = "No locker was selected. Please choose a locker first.";
                return RedirectToAction("Index");
            }

            int lockerId = id.Value;
            int currentMemberId = (int)Session["LoggedInMember"];

            var member = db.Members.Find(currentMemberId);
            if (member == null)
                return RedirectToAction("Login", "Forms");

            var locker = db.Lockers.Find(lockerId);
            if (locker == null)
            {
                TempData["Error"] = "Locker not found.";
                return RedirectToAction("Index");
            }

            if (locker.Gender != member.Gender)
            {
                TempData["Error"] = "This locker is not available for your account.";
                return RedirectToAction("Index");
            }

            // Re-check availability at submit time (race-condition safe).
            // Also catches a locker taken out of service between the GET
            // and the POST — a real window now that faults exist.
            if (!locker.IsReservable)
            {
                TempData["Error"] = locker.OperationalStatus != "InService"
                    ? $"Locker {locker.LockerNumber} is currently out of service. Please choose another locker."
                    : "This locker is currently unavailable. Please choose another locker.";
                return RedirectToAction("Index");
            }

            var existingReservation = db.LockerReservations
                .Where(r => r.MemberId == currentMemberId && r.IsActive)
                .FirstOrDefault();

            if (existingReservation != null)
            {
                TempData["Error"] = "You already have an active locker reservation. Please end it before reserving another.";
                return RedirectToAction("Unlock", new { id = existingReservation.LockerId });
            }

            if (string.IsNullOrWhiteSpace(pin) || pin.Length != 5 || !pin.All(char.IsDigit))
            {
                TempData["Error"] = "Your PIN must be exactly 5 digits.";
                return RedirectToAction("Reserve", new { id = lockerId });
            }

            if (pin != confirmPin)
            {
                TempData["Error"] = "PINs do not match. Please try again.";
                return RedirectToAction("Reserve", new { id = lockerId });
            }

            string hashedPin = BCrypt.Net.BCrypt.HashPassword(pin);

            var reservation = new LockerReservation
            {
                LockerId = locker.LockerId,
                MemberId = currentMemberId,
                AccessCodeHash = hashedPin,
                ReservedAt = DateTime.Now,
                IsActive = true
            };

            locker.IsOccupied = true;
            db.LockerReservations.Add(reservation);
            db.SaveChanges();

            TempData["Success"] = string.Format("Locker {0} reserved successfully!", locker.LockerNumber);
            return RedirectToAction("Index");
        }

        // GET: Locker/Unlock/5
        public ActionResult Unlock(int? id)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            if (!id.HasValue)
            {
                TempData["Error"] = "No locker was selected.";
                return RedirectToAction("Index");
            }

            int lockerId = id.Value;
            int currentMemberId = (int)Session["LoggedInMember"];

            var locker = db.Lockers.Find(lockerId);
            if (locker == null)
            {
                TempData["Error"] = "Locker not found.";
                return RedirectToAction("Index");
            }

            var activeReservation = db.LockerReservations
                .Where(r => r.LockerId == lockerId && r.IsActive)
                .FirstOrDefault();

            if (activeReservation == null || activeReservation.MemberId != currentMemberId)
            {
                TempData["Error"] = "You do not have an active reservation for this locker.";
                return RedirectToAction("Index");
            }

            ViewBag.HasBeenUnlocked = activeReservation.HasBeenUnlocked;
            ViewBag.LockoutSecondsLeft = (activeReservation.LockedUntil.HasValue && activeReservation.LockedUntil > DateTime.Now)
                ? (int)Math.Ceiling((activeReservation.LockedUntil.Value - DateTime.Now).TotalSeconds)
                : 0;

            // ── Fault context for this locker ─────────────────────────
            // Note the member is NOT blocked from unlocking a faulty locker.
            // If their belongings are inside and the PIN pad still works,
            // preventing them from opening it would be actively harmful.
            var openFault = db.LockerFaults
                .Where(f => f.LockerId == lockerId && LockerFaultHelper.OpenStatuses.Contains(f.Status))
                .OrderByDescending(f => f.ReportedDate)
                .FirstOrDefault();

            ViewBag.OpenFault = openFault;
            ViewBag.OperationalStatus = locker.OperationalStatus;

            return View(locker);
        }

        // POST: Locker/Unlock/5 — returns JSON so the door can animate in place
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Unlock(int? id, string pin)
        {
            if (Session["LoggedInMember"] == null)
                return Json(new { success = false, redirect = Url.Action("Login", "Forms") });

            if (!id.HasValue)
                return Json(new { success = false, message = "No locker was selected." });

            int lockerId = id.Value;
            int currentMemberId = (int)Session["LoggedInMember"];

            var activeReservation = db.LockerReservations
                .Include(r => r.Locker)
                .Where(r => r.LockerId == lockerId && r.IsActive)
                .FirstOrDefault();

            if (activeReservation == null || activeReservation.MemberId != currentMemberId)
                return Json(new { success = false, message = "You do not have an active reservation for this locker." });

            // Currently locked out
            if (activeReservation.LockedUntil.HasValue && activeReservation.LockedUntil > DateTime.Now)
            {
                var secondsLeft = (int)Math.Ceiling((activeReservation.LockedUntil.Value - DateTime.Now).TotalSeconds);
                return Json(new
                {
                    success = false,
                    lockedOut = true,
                    secondsLeft,
                    message = $"Too many incorrect attempts. Try again in {Math.Ceiling(secondsLeft / 60.0)} minute(s)."
                });
            }

            bool isCorrect = BCrypt.Net.BCrypt.Verify(pin, activeReservation.AccessCodeHash);

            if (!isCorrect)
            {
                activeReservation.FailedAttempts += 1;

                if (activeReservation.FailedAttempts >= 5)
                {
                    activeReservation.LockedUntil = DateTime.Now.AddMinutes(5);
                    activeReservation.FailedAttempts = 0;
                    activeReservation.LockoutCount += 1;
                    db.SaveChanges();

                    // ── Repeat lockout may signal tampering — tell the admin.
                    // Deliberately NOT on the first lockout, which is usually
                    // just a member who forgot their own PIN. ──
                    if (activeReservation.LockoutCount >= 2)
                    {
                        try
                        {
                            var m = db.Members.Find(activeReservation.MemberId);
                            new NotificationService(db).NotifyAdmin(
                                "Repeated Locker Lockout",
                                $"Locker {activeReservation.Locker?.LockerNumber} has been locked out {activeReservation.LockoutCount} times on the current reservation" +
                                    (m != null ? $" (held by {m.FullName})" : "") + ". This may indicate tampering.",
                                "Alert",
                                "/Admin/ManageLockers");
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }
                    }

                    return Json(new
                    {
                        success = false,
                        lockedOut = true,
                        secondsLeft = 300,
                        message = "Too many incorrect attempts. Locker locked for 5 minutes."
                    });
                }

                db.SaveChanges();

                return Json(new
                {
                    success = false,
                    attemptsRemaining = 5 - activeReservation.FailedAttempts,
                    message = $"Incorrect PIN. {5 - activeReservation.FailedAttempts} attempt(s) remaining."
                });
            }

            // Correct PIN
            activeReservation.FailedAttempts = 0;
            activeReservation.LockedUntil = null;
            activeReservation.HasBeenUnlocked = true;
            db.SaveChanges();

            return Json(new
            {
                success = true,
                message = "Locker unlocked! Don't forget to close it when you're done."
            });
        }

        // POST: Locker/ForgotPin/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPin(int? id)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            if (!id.HasValue)
            {
                TempData["Error"] = "No locker was selected.";
                return RedirectToAction("Index");
            }

            int lockerId = id.Value;
            int currentMemberId = (int)Session["LoggedInMember"];

            var member = db.Members.Find(currentMemberId);
            if (member == null)
                return RedirectToAction("Login", "Forms");

            var locker = db.Lockers.Find(lockerId);
            if (locker == null)
            {
                TempData["Error"] = "Locker not found.";
                return RedirectToAction("Index");
            }

            var activeReservation = db.LockerReservations
                .Where(r => r.LockerId == lockerId && r.IsActive)
                .FirstOrDefault();

            if (activeReservation == null || activeReservation.MemberId != currentMemberId)
            {
                TempData["Error"] = "You do not have an active reservation for this locker.";
                return RedirectToAction("Index");
            }

            Random rng = new Random();
            string newPin = rng.Next(0, 100000).ToString("D5");

            activeReservation.AccessCodeHash = BCrypt.Net.BCrypt.HashPassword(newPin);
            activeReservation.FailedAttempts = 0;
            activeReservation.LockedUntil = null;
            activeReservation.HasBeenUnlocked = false;
            db.SaveChanges();

            // ── In-app notification, so the member still gets a signal
            // even if the email fails ──
            try
            {
                new NotificationService(db).Notify(
                    "Member", currentMemberId,
                    "Locker PIN Reset",
                    $"A new PIN for locker {locker.LockerNumber} has been sent to your email. Your old PIN no longer works.",
                    "Info",
                    Url.Action("Unlock", "Locker", new { id = lockerId }));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }

            try
            {
                EmailService.SendLockerPinReset(member.Email, member.FirstName, locker.LockerNumber, newPin);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[EMAIL ERROR] {0}", ex.Message));
                TempData["Error"] = "PIN was reset, but we couldn't send the email. Please contact support.";
                return RedirectToAction("Unlock", new { id = lockerId });
            }

            TempData["Success"] = "A new PIN has been sent to your email.";
            return RedirectToAction("Unlock", new { id = lockerId });
        }

        // POST: Locker/EndReservation/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EndReservation(int? id)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            if (!id.HasValue)
            {
                TempData["Error"] = "No locker was selected.";
                return RedirectToAction("Index");
            }

            int lockerId = id.Value;
            int currentMemberId = (int)Session["LoggedInMember"];

            var locker = db.Lockers.Find(lockerId);
            if (locker == null)
            {
                TempData["Error"] = "Locker not found.";
                return RedirectToAction("Index");
            }

            var activeReservation = db.LockerReservations
                .Where(r => r.LockerId == lockerId && r.IsActive)
                .FirstOrDefault();

            if (activeReservation == null || activeReservation.MemberId != currentMemberId)
            {
                TempData["Error"] = "You do not have an active reservation for this locker.";
                return RedirectToAction("Index");
            }

            // ── Jammed-locker exception ───────────────────────────────
            // Normally a member must unlock at least once before ending a
            // reservation (proves they actually accessed it). But if the
            // locker is out of service BECAUSE it won't open, that rule
            // traps them in a reservation they physically cannot satisfy.
            // In that case let them release it — staff will handle the
            // belongings via the fault's override path.
            bool lockerIsFaulty = locker.OperationalStatus == "OutOfService"
                               || locker.OperationalStatus == "UnderRepair";

            if (!activeReservation.HasBeenUnlocked && !lockerIsFaulty)
            {
                TempData["Error"] = "Please unlock the locker with your PIN before ending the reservation.";
                return RedirectToAction("Unlock", new { id = lockerId });
            }

            activeReservation.AccessCodeHash = null;
            activeReservation.IsActive = false;
            activeReservation.EndedAt = DateTime.Now;
            activeReservation.FailedAttempts = 0;
            activeReservation.LockedUntil = null;

            locker.IsOccupied = false;
            db.SaveChanges();

            TempData["Success"] = lockerIsFaulty && !activeReservation.HasBeenUnlocked
                ? $"Reservation on locker {locker.LockerNumber} released. Please speak to reception about your belongings."
                : string.Format("Reservation ended. Locker {0} is now available.", locker.LockerNumber);

            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}