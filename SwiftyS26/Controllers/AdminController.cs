using SwiftyS26.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SwiftyS26.Controllers
{
    public class AdminController : Controller
    {
        private GymHubDB db = new GymHubDB();

        private const string AdminEmail = "admin@gymhub.co.za";
        private const string AdminPassword = "Admin@123";
        private const string AdminName = "GymHub Admin";
        private const string AdminRole = "Admin";
        private const int AdminId = 1;

        private bool IsAdminLoggedIn() => Session["LoggedInAdmin"] != null;

        // ============================================================
        // LOGIN
        // ============================================================
        public ActionResult Login()
        {
            if (IsAdminLoggedIn()) return RedirectToAction("Overview");
            return View(new AdminLoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(AdminLoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // ── Check Admin first ──
            if (model.Email.Equals(AdminEmail, StringComparison.OrdinalIgnoreCase)
                && model.Password == AdminPassword)
            {
                Session["LoggedInAdmin"] = AdminId;
                Session["LoggedInAdminName"] = AdminName;
                Session["LoggedInAdminRole"] = AdminRole;
                return RedirectToAction("Overview", "Admin");
            }

            // ── Check Trainer ──
            string hashedInput = HashPassword(model.Password);

            var trainer = db.Trainers.FirstOrDefault(t =>
                t.Email == model.Email &&
                t.PasswordHash == hashedInput &&
                t.IsActive);

            if (trainer != null)
            {
                Session["LoggedInTrainer"] = trainer.TrainerId;
                Session["LoggedInTrainerName"] = trainer.FullName;
                Session["LoggedInTrainerCode"] = trainer.TrainerCode;
                Session["LoggedInTrainerRole"] = trainer.Role;

                if (trainer.MustChangePassword)
                    return RedirectToAction("ChangePassword", "Trainer");

                return RedirectToAction("Dashboard", "Trainer");
            }
            // ── Check Technician ──
            var technician = db.Technicians.FirstOrDefault(t =>
                t.Email == model.Email &&
                t.PasswordHash == hashedInput &&
                t.IsActive);

            if (technician != null)
            {
                Session["LoggedInTechnician"] = technician.TechnicianId;
                Session["LoggedInTechnicianName"] = technician.FullName;
                Session["LoggedInTechnicianCode"] = technician.TechnicianCode;
                Session["LoggedInTechnicianRole"] = technician.Role;

                if (technician.MustChangePassword)
                    return RedirectToAction("ChangePassword", "Technician");

                return RedirectToAction("Dashboard", "Technician");
            }
            var storeStaff = db.StoreStaffs.FirstOrDefault(s =>
                 s.Email == model.Email &&
                 s.PasswordHash == hashedInput &&
                 s.IsActive);

            if (storeStaff != null)
            {
                Session["LoggedInStoreStaff"] = storeStaff.StoreStaffId;
                Session["LoggedInStoreStaffName"] = storeStaff.FullName;
                Session["LoggedInStoreStaffCode"] = storeStaff.StoreStaffCode;
                Session["LoggedInStoreStaffRole"] = storeStaff.Role;

                if (storeStaff.MustChangePassword)
                    return RedirectToAction("ChangePassword", "StoreStaff");

                return RedirectToAction("Dashboard", "StoreStaff");
            }

            // ── Both failed ──
            ViewBag.Error = "Incorrect email or password.";
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            Session.Remove("LoggedInAdmin");
            Session.Remove("LoggedInAdminName");
            Session.Remove("LoggedInAdminRole");
            return RedirectToAction("Login");
        }

        // ============================================================
        // DASHBOARD
        // ============================================================
        public ActionResult Dashboard()
        {
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");

            var members = db.Members.OrderByDescending(m => m.RegisteredAt).ToList();
            var payments = db.Payments.OrderByDescending(p => p.PaymentDate).ToList();
            var trainers = db.Trainers.OrderByDescending(t => t.CreatedAt).ToList();

            var thisMonth = DateTime.Now.Month;
            var thisYear = DateTime.Now.Year;

            // ── Members ──
            ViewBag.TotalMembers = members.Count;
            ViewBag.ActiveMembers = members.Count(m => m.Status == "Active");
            ViewBag.PendingMembers = members.Count(m => m.Status == "Pending");
            ViewBag.SuspendedMembers = members.Count(m => m.Status == "Suspended");
            ViewBag.FrozenMembers = members.Count(m => m.Status == "Frozen");

            // ── Revenue ──
            ViewBag.TotalRevenue = members
                .Where(m => m.Status == "Active" || m.Status == "Frozen")
                .Sum(m => (decimal?)m.MonthlyFee) ?? 0;

            // ── Payments ──
            ViewBag.PaymentsThisMonth = payments.Count(p =>
                p.PaymentDate.Month == thisMonth &&
                p.PaymentDate.Year == thisYear);

            ViewBag.TotalPaymentsAmount = payments
                .Where(p => p.Status == "Paid" || p.Status == "Success")
                .Sum(p => (decimal?)p.Amount) ?? 0;

            ViewBag.TotalPayments = payments.Count;
            ViewBag.PendingPayments = payments.Count(p => p.Status == "Pending");

            // ── Plans ──
            ViewBag.BasicCount = members.Count(m => m.Plan.ToLower() == "basic");
            ViewBag.StandardCount = members.Count(m => m.Plan.ToLower() == "standard");
            ViewBag.PremiumCount = members.Count(m => m.Plan.ToLower() == "premium");

            // ── Trainers ──
            ViewBag.TotalTrainers = trainers.Count;
            ViewBag.ActiveTrainers = trainers.Count(t => t.IsActive);
            ViewBag.FullTimeTrainers = trainers.Count(t => t.EmploymentType == "Full-time");
            ViewBag.PartTimeTrainers = trainers.Count(t => t.EmploymentType == "Part-time");
            ViewBag.ContractTrainers = trainers.Count(t => t.EmploymentType == "Contract");

            // ── Lists ──
            ViewBag.AllMembers = members;
            ViewBag.AllPayments = payments;
            ViewBag.RecentMembers = members.Take(5).ToList();
            ViewBag.RecentTrainers = trainers.Take(5).ToList();
            ViewBag.AdminName = Session["LoggedInAdminName"];

            return View();
        }

        // ============================================================
        // CAFE ADMIN
        // ============================================================
        public ActionResult CafeAdmin()
        {
            ViewBag.PageTitle = "CAFÉ MANAGEMENT";
            ViewBag.ActiveNav = "cafe";
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");
            return View();
        }

        // ============================================================
        // QR SCANNER
        // ============================================================
        public ActionResult Scanner()
        {
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");
            return View();
        }

        [HttpPost]
        public ActionResult VerifyQr(string qrData)
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            if (string.IsNullOrWhiteSpace(qrData))
                return Json(new { success = false, message = "No QR data received." });

            var parsed = MemberHelper.ParseQrContent(qrData);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.MemberNumber))
                return Json(new { success = false, message = "Invalid QR code." });

            var member = db.Members.FirstOrDefault(m => m.MemberNumber == parsed.MemberNumber);
            if (member == null)
                return Json(new { success = false, message = "Member not found.", memberNumber = parsed.MemberNumber });

            if (member.Status == "Cancelled")
                return Json(new { success = false, message = "Access denied — membership is cancelled.", memberName = member.FullName, memberNumber = member.MemberNumber, plan = member.Plan });
            if (member.Status == "Suspended")
                return Json(new { success = false, message = "Access denied — membership is suspended.", memberName = member.FullName, memberNumber = member.MemberNumber, plan = member.Plan });
            if (member.Status == "Frozen")
                return Json(new { success = false, message = "Access denied — membership is currently frozen.", memberName = member.FullName, memberNumber = member.MemberNumber, plan = member.Plan });
            if (member.Status == "Pending Cancellation")
                return Json(new { success = true, message = "Access Granted (Cancellation Pending)", memberName = member.FullName, memberNumber = member.MemberNumber, plan = member.Plan.ToUpper(), status = member.Status });
            if (member.Status != "Active")
                return Json(new { success = false, message = $"Access denied — status is {member.Status}.", memberName = member.FullName, memberNumber = member.MemberNumber, plan = member.Plan });

            return Json(new { success = true, message = "Access Granted!", memberName = member.FullName, memberNumber = member.MemberNumber, plan = member.Plan.ToUpper(), email = member.Email, status = member.Status });
        }

        // ============================================================
        // REFUNDS
        // ============================================================
        public ActionResult GetRefunds()
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            var refunds = db.RefundRequests
                .Include("Member")
                .OrderByDescending(r => r.RequestedAt)
                .ToList();

            var data = refunds.Select(r => new
            {
                refundRequestId = r.RefundRequestId,
                memberName = r.Member != null ? r.Member.FullName : "—",
                memberNumber = r.MemberNumber,
                refundType = r.RefundType,
                refundAmount = r.RefundAmount.ToString("F2"),
                status = r.Status,
                requestedAt = r.RequestedAt.ToString("dd MMM yyyy"),
                processedAt = r.ProcessedAt.HasValue ? r.ProcessedAt.Value.ToString("dd MMM yyyy") : null,
                notes = r.Notes
            }).ToList();

            return Json(new
            {
                success = true,
                refunds = data,
                totalCount = refunds.Count,
                pendingCount = refunds.Count(r => r.Status == "Pending"),
                approvedCount = refunds.Count(r => r.Status == "Approved"),
                rejectedCount = refunds.Count(r => r.Status == "Rejected"),
                totalAmount = refunds.Where(r => r.Status == "Approved")
                                        .Sum(r => r.RefundAmount).ToString("F2")
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult ApproveRefund(int refundRequestId, string adminNotes)
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            var refund = db.RefundRequests
                .Include("Member")
                .FirstOrDefault(r => r.RefundRequestId == refundRequestId);

            if (refund == null)
                return Json(new { success = false, message = "Not found." });
            if (refund.Status != "Pending")
                return Json(new { success = false, message = $"Already {refund.Status}." });

            refund.Status = "Approved";
            refund.ProcessedAt = DateTime.Now;
            refund.Notes = (refund.Notes ?? "") +
                                 $"\n[Approved {DateTime.Now:dd MMM yyyy HH:mm}]" +
                                 (string.IsNullOrWhiteSpace(adminNotes) ? "" : $"\nNotes: {adminNotes}");
            db.SaveChanges();
            if (refund.Member != null)
            {
                new NotificationService(db).Notify(
                    "Member", refund.Member.MemberId,
                    "Refund Approved",
                    $"Your refund of R{refund.RefundAmount:F2} has been approved.",
                    "Success",
                    "/Dashboard/Payments");
            }

            try
            {
                if (refund.Member != null)
                    EmailService.SendRefundApproved(
                        refund.Member.Email, refund.Member.FirstName,
                        refund.MemberNumber, refund.RefundAmount);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EMAIL] {ex.Message}");
            }

            return Json(new
            {
                success = true,
                message = $"Refund of R{refund.RefundAmount:F2} approved.",
                refundAmount = refund.RefundAmount.ToString("F2"),
                memberName = refund.Member?.FullName ?? "—"
            });
        }

        [HttpPost]
        public ActionResult RejectRefund(int refundRequestId, string adminNotes)
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            var refund = db.RefundRequests
                .Include("Member")
                .FirstOrDefault(r => r.RefundRequestId == refundRequestId);

            if (refund == null)
                return Json(new { success = false, message = "Not found." });
            if (refund.Status != "Pending")
                return Json(new { success = false, message = $"Already {refund.Status}." });

            refund.Status = "Rejected";
            refund.ProcessedAt = DateTime.Now;
            refund.Notes = (refund.Notes ?? "") +
                                 $"\n[Rejected {DateTime.Now:dd MMM yyyy HH:mm}]" +
                                 (string.IsNullOrWhiteSpace(adminNotes) ? "" : $"\nReason: {adminNotes}");
            db.SaveChanges();
            if (refund.Member != null)
            {
                new NotificationService(db).Notify(
                    "Member", refund.Member.MemberId,
                    "Refund Declined",
                    $"Your refund request for R{refund.RefundAmount:F2} was declined." +
                        (string.IsNullOrWhiteSpace(adminNotes) ? "" : $" Reason: {adminNotes}"),
                    "Warning",
                    "/Dashboard/Payments");
            }

            try
            {
                if (refund.Member != null)
                    EmailService.SendRefundRejected(
                        refund.Member.Email, refund.Member.FirstName,
                        refund.MemberNumber, refund.RefundAmount,
                        adminNotes ?? "No reason provided.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EMAIL] {ex.Message}");
            }

            return Json(new
            {
                success = true,
                message = "Refund request rejected.",
                memberName = refund.Member?.FullName ?? "—"
            });
        }

        // ============================================================
        // FREEZE MANAGEMENT
        // ============================================================
        public ActionResult GetFreezes()
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            var freezes = db.FreezeRequests
                .Include("Member")
                .OrderByDescending(f => f.RequestedAt)
                .ToList();

            var data = freezes.Select(f => new
            {
                freezeRequestId = f.FreezeRequestId,
                memberName = f.Member != null ? f.Member.FullName : "—",
                memberNumber = f.MemberNumber,
                freezeStart = f.FreezeStart.ToString("dd MMM yyyy"),
                freezeEnd = f.FreezeEnd.ToString("dd MMM yyyy"),
                days = f.FreezeDurationDays,
                reason = f.Reason,
                status = f.Status,
                requestedAt = f.RequestedAt.ToString("dd MMM yyyy"),
                processedAt = f.ProcessedAt.HasValue ? f.ProcessedAt.Value.ToString("dd MMM yyyy") : null,
                adminNotes = f.AdminNotes,
                originalExpiry = f.OriginalExpiry.HasValue ? f.OriginalExpiry.Value.ToString("dd MMM yyyy") : null,
                extendedExpiry = f.ExtendedExpiry.HasValue ? f.ExtendedExpiry.Value.ToString("dd MMM yyyy") : null,
                isCurrentlyFrozen = f.IsCurrentlyFrozen
            }).ToList();

            return Json(new
            {
                success = true,
                freezes = data,
                totalCount = freezes.Count,
                pendingCount = freezes.Count(f => f.Status == "Pending"),
                approvedCount = freezes.Count(f => f.Status == "Approved"),
                rejectedCount = freezes.Count(f => f.Status == "Rejected"),
                completedCount = freezes.Count(f => f.Status == "Completed"),
                activeCount = freezes.Count(f => f.IsCurrentlyFrozen)
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ApproveFreeze(int freezeRequestId, string adminNotes)
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            var freeze = db.FreezeRequests
                .Include("Member")
                .FirstOrDefault(f => f.FreezeRequestId == freezeRequestId);

            if (freeze == null)
                return Json(new { success = false, message = "Freeze request not found." });
            if (freeze.Status != "Pending")
                return Json(new { success = false, message = $"Request is already {freeze.Status}." });

            var member = freeze.Member ?? db.Members.Find(freeze.MemberId);
            if (member == null)
                return Json(new { success = false, message = "Member not found." });

            if (member.Status != "Active")
                return Json(new { success = false, message = $"Member status is {member.Status}. Only Active members can be frozen." });

            bool hasOutstanding = db.Payments.Any(p =>
                p.MemberId == member.MemberId && p.Status == "Pending");
            if (hasOutstanding)
                return Json(new { success = false, message = "Member has outstanding pending payments." });

            DateTime originalExpiry = member.MembershipExpiry ?? DateTime.Today.AddMonths(1);
            int freezeDays = freeze.FreezeDurationDays;
            DateTime extendedExpiry = originalExpiry.AddDays(freezeDays);

            freeze.Status = "Approved";
            freeze.ProcessedAt = DateTime.Now;
            freeze.AdminNotes = adminNotes?.Trim();
            freeze.OriginalExpiry = originalExpiry;
            freeze.ExtendedExpiry = extendedExpiry;

            if (freeze.FreezeStart.Date <= DateTime.Today)
                member.Status = "Frozen";

            member.MembershipExpiry = extendedExpiry;
            db.Entry(member).State = EntityState.Modified;
            db.SaveChanges();
            new NotificationService(db).Notify(
    "Member", member.MemberId,
    "Freeze Approved",
    $"Your freeze from {freeze.FreezeStart:dd MMM} to {freeze.FreezeEnd:dd MMM} was approved. Membership extended to {extendedExpiry:dd MMM yyyy}.",
    "Success",
    "/Dashboard/Freeze");
            try
            {
                EmailService.SendFreezeApproved(
                    member.Email, member.FirstName, member.MemberNumber,
                    freeze.FreezeStart.ToString("dd MMM yyyy"),
                    freeze.FreezeEnd.ToString("dd MMM yyyy"),
                    freezeDays,
                    originalExpiry.ToString("dd MMM yyyy"),
                    extendedExpiry.ToString("dd MMM yyyy"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EMAIL] {ex.Message}");
            }

            return Json(new
            {
                success = true,
                message = $"Freeze approved for {member.FullName}. Extended by {freezeDays} days to {extendedExpiry:dd MMM yyyy}.",
                memberName = member.FullName,
                freezeDays,
                extendedExpiry = extendedExpiry.ToString("dd MMM yyyy"),
                originalExpiry = originalExpiry.ToString("dd MMM yyyy")
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectFreeze(int freezeRequestId, string adminNotes)
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            var freeze = db.FreezeRequests
                .Include("Member")
                .FirstOrDefault(f => f.FreezeRequestId == freezeRequestId);

            if (freeze == null)
                return Json(new { success = false, message = "Freeze request not found." });
            if (freeze.Status != "Pending")
                return Json(new { success = false, message = $"Request is already {freeze.Status}." });

            freeze.Status = "Rejected";
            freeze.ProcessedAt = DateTime.Now;
            freeze.AdminNotes = adminNotes?.Trim();
            db.SaveChanges();

            var member = freeze.Member ?? db.Members.Find(freeze.MemberId);
            if (member != null)
            {
                new NotificationService(db).Notify(
                    "Member", member.MemberId,
                    "Freeze Request Declined",
                    $"Your freeze request for {freeze.FreezeStart:dd MMM}–{freeze.FreezeEnd:dd MMM} was declined." +
                        (string.IsNullOrWhiteSpace(adminNotes) ? "" : $" Reason: {adminNotes}"),
                    "Warning",
                    "/Dashboard/Freeze");
            }
            try
            {
                if (member != null)
                    EmailService.SendFreezeRejected(
                        member.Email, member.FirstName, member.MemberNumber,
                        freeze.FreezeStart.ToString("dd MMM yyyy"),
                        freeze.FreezeEnd.ToString("dd MMM yyyy"),
                        adminNotes ?? "No reason provided.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EMAIL] {ex.Message}");
            }

            return Json(new
            {
                success = true,
                message = $"Freeze request rejected for {member?.FullName ?? freeze.MemberNumber}.",
                memberName = member?.FullName ?? "—"
            });
        }

        // ============================================================
        // ATTENDANCE — CHECK IN
        // ============================================================
        [HttpPost]
        public ActionResult CheckIn(string memberId)
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            if (string.IsNullOrWhiteSpace(memberId))
                return Json(new { success = false, message = "Please enter a Member ID." });

            memberId = memberId.Trim().ToUpper();
            var member = db.Members.FirstOrDefault(m => m.MemberNumber == memberId);

            if (member == null)
                return Json(new { success = false, message = $"No member found with ID <strong>{memberId}</strong>." });
            if (member.Status == "Frozen")
                return Json(new { success = false, message = $"<strong>{member.FullName}</strong>'s membership is currently frozen." });
            if (member.Status != "Active" && member.Status != "Pending Cancellation")
                return Json(new { success = false, message = $"Member <strong>{member.FullName}</strong> has status <strong>{member.Status}</strong> and cannot be checked in." });

            var today = DateTime.Today;
            var already = db.Attendances.FirstOrDefault(a =>
                a.MemberId == member.MemberId &&
                DbFunctions.TruncateTime(a.CheckInTime) == today &&
                a.CheckOutTime == null);

            if (already != null)
                return Json(new
                {
                    success = false,
                    alreadyIn = true,
                    message = $"<strong>{member.FullName}</strong> is already checked in today.",
                    attendanceId = already.AttendanceId,
                    memberName = already.MemberName,
                    memberNumber = already.MemberNumber,
                    plan = already.Plan,
                    checkIn = already.CheckInTime.ToString("HH:mm")
                });

            var record = new Attendance
            {
                MemberId = member.MemberId,
                MemberNumber = member.MemberNumber,
                MemberName = member.FullName,
                Plan = member.Plan,
                CheckInTime = DateTime.Now,
                IpAddress = Request.UserHostAddress
            };
            db.Attendances.Add(record);
            db.SaveChanges();

            // ── Loyalty Points ──
            const int POINTS_PER_VISIT = 10;
            var loyalty = db.LoyaltyAccounts.FirstOrDefault(l => l.MemberId == member.MemberId);
            if (loyalty == null)
            {
                loyalty = new LoyaltyAccount
                {
                    MemberId = member.MemberId,
                    CurrentPoints = 0,
                    TotalPointsEarned = 0,
                    CreatedAt = DateTime.Now
                };
                db.LoyaltyAccounts.Add(loyalty);
                db.SaveChanges();
            }

            loyalty.CurrentPoints += POINTS_PER_VISIT;
            loyalty.TotalPointsEarned += POINTS_PER_VISIT;
            loyalty.LastUpdated = DateTime.Now;

            db.PointsTransactions.Add(new PointsTransaction
            {
                MemberId = member.MemberId,
                TransactionType = "Earned",
                Points = POINTS_PER_VISIT,
                Description = $"Check-in on {DateTime.Now:dd MMM yyyy}",
                CreatedAt = DateTime.Now
            });
            db.SaveChanges();

            // ── Milestone Check ──
            int totalVisits = db.Attendances.Count(a => a.MemberId == member.MemberId);
            int bonusPoints = 0;
            string milestoneMessage = null;

            if (totalVisits == 10) { bonusPoints = 50; milestoneMessage = "🎉 10 visits — bonus 50 points!"; }
            else if (totalVisits == 25) { bonusPoints = 150; milestoneMessage = "🎉 25 visits — bonus 150 points!"; }
            else if (totalVisits == 50) { bonusPoints = 300; milestoneMessage = "🎉 50 visits — bonus 300 points!"; }
            else if (totalVisits == 100) { bonusPoints = 500; milestoneMessage = "🎉 100 visits — bonus 500 points!"; }

            if (bonusPoints > 0)
            {
                loyalty.CurrentPoints += bonusPoints;
                loyalty.TotalPointsEarned += bonusPoints;
                loyalty.LastUpdated = DateTime.Now;

                db.PointsTransactions.Add(new PointsTransaction
                {
                    MemberId = member.MemberId,
                    TransactionType = "Bonus",
                    Points = bonusPoints,
                    Description = $"Milestone bonus — {totalVisits} visits reached",
                    CreatedAt = DateTime.Now
                });
                db.SaveChanges();
            }

            return Json(new
            {
                success = true,
                attendanceId = record.AttendanceId,
                memberName = record.MemberName,
                memberNumber = record.MemberNumber,
                plan = record.Plan,
                checkIn = record.CheckInTime.ToString("HH:mm"),
                email = member.Email,
                pointsEarned = POINTS_PER_VISIT,
                bonusPoints,
                totalPoints = loyalty.CurrentPoints,
                milestoneMessage
            });
        }

        // ============================================================
        // ATTENDANCE — CHECK OUT
        // ============================================================
        [HttpPost]
        public ActionResult CheckOut(int attendanceId)
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            var record = db.Attendances.Find(attendanceId);
            if (record == null)
                return Json(new { success = false, message = "Record not found." });
            if (record.CheckOutTime.HasValue)
                return Json(new
                {
                    success = false,
                    message = $"Already checked out at {record.CheckOutTime.Value:HH:mm}.",
                    checkOut = record.CheckOutTime.Value.ToString("HH:mm"),
                    duration = record.Duration
                });

            record.CheckOutTime = DateTime.Now;
            db.Entry(record).State = EntityState.Modified;
            db.SaveChanges();

            return Json(new
            {
                success = true,
                attendanceId = record.AttendanceId,
                memberName = record.MemberName,
                memberNumber = record.MemberNumber,
                checkIn = record.CheckInTime.ToString("HH:mm"),
                checkOut = record.CheckOutTime.Value.ToString("HH:mm"),
                duration = record.Duration
            });
        }

        // ============================================================
        // ATTENDANCE — TODAY
        // ============================================================
        public ActionResult GetTodayAttendance()
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            var today = DateTime.Today;
            var records = db.Attendances
                .Where(a => DbFunctions.TruncateTime(a.CheckInTime) == today)
                .OrderByDescending(a => a.CheckInTime).ToList();

            var data = records.Select(a => new
            {
                attendanceId = a.AttendanceId,
                memberName = a.MemberName,
                memberNumber = a.MemberNumber,
                plan = a.Plan,
                checkIn = a.CheckInTime.ToString("HH:mm"),
                checkOut = a.CheckOutTime.HasValue ? a.CheckOutTime.Value.ToString("HH:mm") : (string)null,
                duration = a.Duration,
                isCheckedOut = a.IsCheckedOut
            }).ToList();

            return Json(new
            {
                success = true,
                records = data,
                totalToday = records.Count,
                currentlyIn = records.Count(a => !a.IsCheckedOut),
                checkedOut = records.Count(a => a.IsCheckedOut)
            }, JsonRequestBehavior.AllowGet);
        }

        // ============================================================
        // ATTENDANCE — DATE RANGE REPORT
        // ============================================================
        public ActionResult GetAttendanceReport(string from, string to)
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            DateTime fromDate = DateTime.Today.AddDays(-6);
            DateTime toDate = DateTime.Today;

            if (!string.IsNullOrWhiteSpace(from)) DateTime.TryParse(from, out fromDate);
            if (!string.IsNullOrWhiteSpace(to)) DateTime.TryParse(to, out toDate);

            toDate = toDate.Date.AddDays(1).AddTicks(-1);

            var records = db.Attendances
                .Where(a => a.CheckInTime >= fromDate && a.CheckInTime <= toDate)
                .OrderByDescending(a => a.CheckInTime).ToList();

            var data = records.Select(a => new
            {
                attendanceId = a.AttendanceId,
                memberName = a.MemberName,
                memberNumber = a.MemberNumber,
                plan = a.Plan,
                date = a.CheckInTime.ToString("dd MMM yyyy"),
                checkIn = a.CheckInTime.ToString("HH:mm"),
                checkOut = a.CheckOutTime.HasValue ? a.CheckOutTime.Value.ToString("HH:mm") : "—",
                duration = a.Duration,
                isCheckedOut = a.IsCheckedOut
            }).ToList();

            return Json(new
            {
                success = true,
                records = data,
                totalVisits = records.Count,
                fromDate = fromDate.ToString("dd MMM yyyy"),
                toDate = toDate.ToString("dd MMM yyyy")
            }, JsonRequestBehavior.AllowGet);
        }

        // ============================================================
        // TRAINER MANAGEMENT
        // ============================================================
        public ActionResult TrainerManagement()
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login");

            ViewBag.Trainers = db.Trainers
                .OrderByDescending(t => t.CreatedAt)
                .ToList();
            ViewBag.PageTitle = "TRAINER MANAGEMENT";
            ViewBag.ActiveNav = "trainers";

            return View("TrainerManagement", new Trainer());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Trainer trainer)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill in all required fields correctly.";
                return RedirectToAction("TrainerManagement");
            }

            if (db.Trainers.Any(t => t.Email == trainer.Email))
            {
                TempData["Error"] = "A trainer with this email already exists.";
                return RedirectToAction("TrainerManagement");
            }

            int next = db.Trainers.Any()
                ? db.Trainers.Max(t => t.TrainerId) + 1
                : 1;
            trainer.TrainerCode = "TRN-" + next.ToString("D4");

            string tempPassword = GenerateTempPassword();
            trainer.PasswordHash = HashPassword(tempPassword);
            trainer.MustChangePassword = true;
            trainer.IsActive = true;
            trainer.Role = "Trainer";
            trainer.CreatedAt = DateTime.Now;

            db.Trainers.Add(trainer);
            db.SaveChanges();

            try
            {
                EmailService.SendTrainerWelcome(
                    trainer.Email, trainer.FullName,
                    trainer.TrainerCode, tempPassword);
                TempData["Success"] = $"Trainer {trainer.FullName} added. Login details sent to {trainer.Email}.";
            }
            catch
            {
                TempData["Warning"] = $"Trainer {trainer.FullName} added but email could not be sent to {trainer.Email}.";
            }

            return RedirectToAction("TrainerManagement");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Trainer trainer)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login");

            var existing = db.Trainers.Find(trainer.TrainerId);
            if (existing == null)
            {
                TempData["Error"] = "Trainer not found.";
                return RedirectToAction("TrainerManagement");
            }

            existing.FullName = trainer.FullName;
            existing.Email = trainer.Email;
            existing.PhoneNumber = trainer.PhoneNumber;
            existing.Gender = trainer.Gender;
            existing.DateOfBirth = trainer.DateOfBirth;
            existing.Specialization = trainer.Specialization;
            existing.ExperienceLevel = trainer.ExperienceLevel;
            existing.Qualification = trainer.Qualification;
            existing.EmploymentType = trainer.EmploymentType;
            existing.MaxCapacity = trainer.MaxCapacity;

            db.SaveChanges();

            TempData["Success"] = $"{existing.FullName} updated successfully.";
            return RedirectToAction("TrainerManagement");
        }

        public ActionResult Delete(int id)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login");

            var trainer = db.Trainers.Find(id);
            if (trainer == null)
            {
                TempData["Error"] = "Trainer not found.";
                return RedirectToAction("TrainerManagement");
            }

            string name = trainer.FullName;
            db.Trainers.Remove(trainer);
            db.SaveChanges();

            TempData["Success"] = $"Trainer {name} deleted successfully.";
            return RedirectToAction("TrainerManagement");
        }

        // ============================================================
        // UTILITIES
        // ============================================================
        public ActionResult FixSuspended()
        {
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");
            var wrongly = db.Members
                .Where(m => m.Status == "Suspended" &&
                       DbFunctions.DiffDays(m.RegisteredAt, DateTime.Today) < 30)
                .ToList();
            foreach (var m in wrongly) m.Status = "Active";
            db.SaveChanges();
            return Content($"✅ Fixed {wrongly.Count} wrongly suspended members.");
        }

        public ActionResult RegenerateQr(int memberId)
        {
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");
            var member = db.Members.Find(memberId);
            if (member == null) return Content("Member not found.");
            member.QrCodeBase64 = MemberHelper.GenerateQrBase64(
                member.MemberNumber, member.FullName, member.Plan, member.Email);
            db.Entry(member).State = EntityState.Modified;
            db.SaveChanges();
            return Content($"✅ QR regenerated for {member.FullName}");
        }

        public ActionResult RegenerateAllQr()
        {
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");
            var membersWithoutQr = db.Members
                .Where(m => m.QrCodeBase64 == null || m.QrCodeBase64 == "").ToList();
            foreach (var m in membersWithoutQr)
                m.QrCodeBase64 = MemberHelper.GenerateQrBase64(
                    m.MemberNumber, m.FullName, m.Plan, m.Email);
            db.SaveChanges();
            return Content($"✅ Generated QR for {membersWithoutQr.Count} members.");
        }

        // ============================================================
        // HELPERS
        // ============================================================
        private string GenerateTempPassword(int length = 10)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

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

        // GET: /Admin/GetCurrentOccupancy
        public ActionResult GetCurrentOccupancy()
        {
            if (!IsAdminLoggedIn() && Session["LoggedInMember"] == null)
                return Json(new { success = false, message = "Unauthorized" },
                            JsonRequestBehavior.AllowGet);

            const int MAX_CAPACITY = 150; // match your gym's actual limit

            var today = DateTime.Today;

            // REAL data only — members checked in today with no checkout yet
            int currentCount = db.Attendances.Count(a =>
                DbFunctions.TruncateTime(a.CheckInTime) == today &&
                a.CheckOutTime == null);

            if (currentCount == 0)
            {
                return Json(new
                {
                    success = true,
                    currentCount = 0,
                    maxCapacity = MAX_CAPACITY,
                    occupancyPct = 0,
                    freeSpots = MAX_CAPACITY,
                    occupancyStatus = "No data",
                    message = "No live occupancy data available.",
                    lastUpdated = DateTime.Now.ToString("HH:mm")
                }, JsonRequestBehavior.AllowGet);
            }

            int pct = (int)Math.Round((currentCount / (double)MAX_CAPACITY) * 100);
            int freeSpots = Math.Max(0, MAX_CAPACITY - currentCount);

            string status;
            if (pct < 40) status = "Quiet";
            else if (pct < 65) status = "Moderate";
            else if (pct < 85) status = "Busy";
            else status = "Near Full";

            return Json(new
            {
                success = true,
                currentCount,
                maxCapacity = MAX_CAPACITY,
                occupancyPct = pct,
                freeSpots,
                occupancyStatus = status,
                lastUpdated = DateTime.Now.ToString("HH:mm")
            }, JsonRequestBehavior.AllowGet);
        }

        // GET: /Admin/GetFullOccupancyAnalysis
        public ActionResult GetFullOccupancyAnalysis()
        {
            if (!IsAdminLoggedIn() && Session["LoggedInMember"] == null)
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            const int MAX_CAPACITY = 150;
            var today = DateTime.Today;
            var now = DateTime.Now;

            // ── Currently inside ──────────────────────────────────────────
            var currentlyIn = db.Attendances
                .Where(a => DbFunctions.TruncateTime(a.CheckInTime) == today
                         && a.CheckOutTime == null)
                .ToList();

            int currentCount = currentlyIn.Count;
            int occupancyPct = (int)Math.Round((currentCount / (double)MAX_CAPACITY) * 100);

            string occupancyStatus =
                occupancyPct < 40 ? "Quiet" :
                occupancyPct < 65 ? "Moderate" :
                occupancyPct < 85 ? "Busy" : "Near Full";

            // ── Today's totals ────────────────────────────────────────────
            var todayAll = db.Attendances
                .Where(a => DbFunctions.TruncateTime(a.CheckInTime) == today)
                .ToList();

            var checkedOut = todayAll.Where(a => a.CheckOutTime.HasValue).ToList();

            double avgSessionMins = checkedOut.Any()
                ? checkedOut.Average(a =>
                    (a.CheckOutTime.Value - a.CheckInTime).TotalMinutes)
                : 0;

            int longSessions = currentlyIn
                .Count(a => (now - a.CheckInTime).TotalMinutes > 120);

            // ── Yesterday comparison ──────────────────────────────────────
            var yesterday = today.AddDays(-1);
            int yesterdayCount = db.Attendances
                .Count(a => DbFunctions.TruncateTime(a.CheckInTime) == yesterday);

            // ── Hourly breakdown (today, 05h–21h) ────────────────────────
            var hourly = todayAll
                .GroupBy(a => a.CheckInTime.Hour)
                .Select(g => new { hour = g.Key, count = g.Count() })
                .OrderBy(x => x.hour)
                .ToList();

            var hourlyData = Enumerable.Range(5, 17).Select(h => new
            {
                hour = h.ToString("D2"),
                count = hourly.FirstOrDefault(x => x.hour == h)?.count ?? 0
            }).ToList();

            // ── Plan breakdown for members currently inside ───────────────
            var memberIds = currentlyIn.Select(a => a.MemberId).Distinct().ToList();
            var members = db.Members
                .Where(m => memberIds.Contains(m.MemberId))
                .Select(m => new { m.MemberId, m.Plan })
                .ToList();

            var planBreakdown = members
                .GroupBy(m => m.Plan.ToLower())
                .Select(g => new { plan = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToList();

            // ── Peak heatmap — last 7 days by hour slot ───────────────────
            var sevenDaysAgo = today.AddDays(-7);
            var weekAttendance = db.Attendances
                .Where(a => a.CheckInTime >= sevenDaysAgo &&
                            a.CheckInTime < today)
                .ToList();

            // Slot boundaries: 05-09, 09-12, 12-15, 15-18, 18-22
            var slots = new[] {
        new { label = "05–09", from = 5,  to = 9  },
        new { label = "09–12", from = 9,  to = 12 },
        new { label = "12–15", from = 12, to = 15 },
        new { label = "15–18", from = 15, to = 18 },
        new { label = "18–22", from = 18, to = 22 }
    };

            // Days: 0=Mon … 6=Sun
            var heatmap = slots.Select(slot => new
            {
                slot = slot.label,
                days = Enumerable.Range(0, 7).Select(d =>
                {
                    var date = today.AddDays(-(6 - d));
                    return weekAttendance.Count(a =>
                        a.CheckInTime.Date == date.Date &&
                        a.CheckInTime.Hour >= slot.from &&
                        a.CheckInTime.Hour < slot.to);
                }).ToList()
            }).ToList();

            // ── 7-day rolling avg visits per day ─────────────────────────
            double rollingAvg = weekAttendance.Any()
                ? weekAttendance.GroupBy(a => a.CheckInTime.Date)
                                .Average(g => g.Count())
                : 0;

            // ── Capacity alert ────────────────────────────────────────────
            bool showAlert = occupancyPct >= 75;
            string alertMessage = showAlert
                ? $"Gym is at {occupancyPct}% capacity ({currentCount}/{MAX_CAPACITY}). Consider notifying members of quieter windows."
                : null;

            return Json(new
            {
                success = true,

                // Live
                currentCount,
                maxCapacity = MAX_CAPACITY,
                occupancyPct,
                occupancyStatus,
                freeSpots = Math.Max(0, MAX_CAPACITY - currentCount),
                showAlert,
                alertMessage,
                lastUpdated = now.ToString("HH:mm"),

                // Today
                totalVisitsToday = todayAll.Count,
                checkedOutCount = checkedOut.Count,
                avgSessionMins = Math.Round(avgSessionMins, 0),
                longSessionCount = longSessions,
                yesterdayVisits = yesterdayCount,

                // Breakdowns
                hourlyData,
                planBreakdown,
                heatmap,
                rollingAvg7Day = Math.Round(rollingAvg, 0),

                // Day labels for heatmap (Mon–Sun relative to today)
                dayLabels = Enumerable.Range(0, 7)
                    .Select(d => today.AddDays(-(6 - d)).ToString("ddd"))
                    .ToList()
            }, JsonRequestBehavior.AllowGet);
        }
        // ============================================================
        // TECHNICIAN MANAGEMENT
        // ============================================================
        public ActionResult TechnicianManagement()
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login");

            ViewBag.Technicians = db.Technicians
                .OrderByDescending(t => t.CreatedAt)
                .ToList();
            ViewBag.PageTitle = "TECHNICIAN MANAGEMENT";
            ViewBag.ActiveNav = "technicians";

            return View("TechnicianManagement", new Technician());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateTechnician(Technician technician)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill in all required fields correctly.";
                return RedirectToAction("TechnicianManagement");
            }

            if (db.Technicians.Any(t => t.Email == technician.Email))
            {
                TempData["Error"] = "A technician with this email already exists.";
                return RedirectToAction("TechnicianManagement");
            }

            int next = db.Technicians.Any()
                ? db.Technicians.Max(t => t.TechnicianId) + 1
                : 1;
            technician.TechnicianCode = "TECH-" + next.ToString("D4");

            string tempPassword = GenerateTempPassword();
            technician.PasswordHash = HashPassword(tempPassword);
            technician.MustChangePassword = true;
            technician.IsActive = true;
            technician.Role = "Technician";
            technician.CreatedAt = DateTime.Now;

            db.Technicians.Add(technician);
            db.SaveChanges();

            try
            {
                EmailService.SendTechnicianWelcome(
                    technician.Email, technician.FullName,
                    technician.TechnicianCode, tempPassword);
                TempData["Success"] = $"Technician {technician.FullName} added. Login details sent to {technician.Email}.";
            }
            catch
            {
                TempData["Warning"] = $"Technician {technician.FullName} added but email could not be sent to {technician.Email}.";
            }

            return RedirectToAction("TechnicianManagement");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditTechnician(Technician technician)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login");

            var existing = db.Technicians.Find(technician.TechnicianId);
            if (existing == null)
            {
                TempData["Error"] = "Technician not found.";
                return RedirectToAction("TechnicianManagement");
            }

            existing.FullName = technician.FullName;
            existing.Email = technician.Email;
            existing.PhoneNumber = technician.PhoneNumber;
            existing.Gender = technician.Gender;
            existing.DateOfBirth = technician.DateOfBirth;
            existing.Specialization = technician.Specialization;
            existing.CertificationLevel = technician.CertificationLevel;
            existing.EmploymentType = technician.EmploymentType;

            db.SaveChanges();

            TempData["Success"] = $"{existing.FullName} updated successfully.";
            return RedirectToAction("TechnicianManagement");
        }

        public ActionResult DeleteTechnician(int id)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login");

            var technician = db.Technicians.Find(id);
            if (technician == null)
            {
                TempData["Error"] = "Technician not found.";
                return RedirectToAction("TechnicianManagement");
            }

            string name = technician.FullName;
            db.Technicians.Remove(technician);
            db.SaveChanges();

            TempData["Success"] = $"Technician {name} deleted successfully.";
            return RedirectToAction("TechnicianManagement");
        }

        // ============================================================
        // COMPLAINTS MANAGEMENT
        // ============================================================

        public ActionResult AdminComplaintList(string statusFilter = null)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login");

            var query = db.ComplaintTickets
                .Include("Member")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(statusFilter) && ComplaintHelper.IsValidStatus(statusFilter))
                query = query.Where(c => c.Status == statusFilter);

            var complaints = query
                .OrderByDescending(c => c.SubmittedAt)
                .ToList();

            var allComplaints = db.ComplaintTickets.ToList(); // for stat counts regardless of filter

            ViewBag.PageTitle = "Complaints";
            ViewBag.ActiveNav = "complaints";
            ViewBag.Statuses = ComplaintHelper.ValidStatuses;
            ViewBag.CurrentFilter = statusFilter;

            ViewBag.TotalComplaints = allComplaints.Count;
            ViewBag.SubmittedCount = allComplaints.Count(c => c.Status == "Submitted");
            ViewBag.InvestigatingCount = allComplaints.Count(c => c.Status == "Under Investigation");
            ViewBag.ResolvedCount = allComplaints.Count(c => c.Status == "Resolved");
            ViewBag.ClosedCount = allComplaints.Count(c => c.Status == "Closed");

            return View("AdminComplaintList", complaints);
        }

        [HttpPost]
        public ActionResult UpdateComplaintStatus(int complaintId, string status, string staffComments, string resolutionDetails)
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            if (!ComplaintHelper.IsValidStatus(status))
                return Json(new { success = false, message = "Invalid status value." });

            var complaint = db.ComplaintTickets
                .Include("Member")
                .FirstOrDefault(c => c.ComplaintId == complaintId);

            if (complaint == null)
                return Json(new { success = false, message = "Complaint not found." });

            if (status == "Resolved" && string.IsNullOrWhiteSpace(resolutionDetails))
                return Json(new { success = false, message = "Please provide resolution details before marking as Resolved." });

            complaint.Status = status;
            complaint.StaffComments = staffComments;
            if (!string.IsNullOrWhiteSpace(resolutionDetails))
                complaint.ResolutionDetails = resolutionDetails;

            complaint.LastUpdatedAt = DateTime.Now;
            if (status == "Closed")
                complaint.ClosedAt = DateTime.Now;

            try
            {
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB ERROR] {ex.Message}");
                return Json(new { success = false, message = "Error updating the complaint. Please try again." });
            }

            if (complaint.Member != null)
            {
                new NotificationService(db).Notify(
                    "Member",
                    complaint.MemberId,
                    "Complaint Update",
                    $"Your complaint {complaint.ReferenceNumber} is now \"{status}\".",
                    status == "Resolved" ? "Success" : "Info",
                    "/Dashboard/MyComplaints"
                );

                try
                {
                    EmailService.SendComplaintStatusUpdate(
                        complaint.Member.Email, complaint.Member.FirstName,
                        complaint.ReferenceNumber, complaint.Status, complaint.StaffComments);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EMAIL ERROR] {ex.Message}");
                }
            }

            return Json(new { success = true, message = "Complaint updated successfully." });
        }
        // ============================================================
        // FEEDBACK — VIEW RATINGS
        // ============================================================

        public ActionResult FeedbackList(string categoryFilter = null)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login");

            var query = db.Feedbacks
                .Include("Member")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(categoryFilter) && FeedbackHelper.IsValidCategory(categoryFilter))
                query = query.Where(f => f.Category == categoryFilter);

            var feedbackList = query
                .OrderByDescending(f => f.SubmittedAt)
                .ToList();

            var allFeedback = db.Feedbacks.ToList(); // for stats regardless of filter

            ViewBag.PageTitle = "Feedback";
            ViewBag.ActiveNav = "feedback";
            ViewBag.Categories = FeedbackHelper.ValidCategories;
            ViewBag.CurrentFilter = categoryFilter;

            ViewBag.TotalFeedback = allFeedback.Count;
            ViewBag.AverageRating = allFeedback.Any()
                ? Math.Round(allFeedback.Average(f => f.Rating), 1)
                : 0;
            ViewBag.PositiveCount = allFeedback.Count(f => f.Rating >= 4);   // 4-5 stars
            ViewBag.NegativeCount = allFeedback.Count(f => f.Rating <= 2);   // 1-2 stars

            // Per-category average, used for the breakdown row
            ViewBag.CategoryAverages = FeedbackHelper.ValidCategories
                .Select(cat => new CategoryAverageStat
                {
                    Category = cat,
                    Average = (decimal)(allFeedback.Where(f => f.Category == cat).Any()
                        ? Math.Round(allFeedback.Where(f => f.Category == cat).Average(f => f.Rating), 1)
                        : 0),
                    Count = allFeedback.Count(f => f.Category == cat)
                })
                .ToList();

            return View("FeedbackList", feedbackList);
        }
        // ============================================================
        // LOCKER MANAGEMENT
        // ============================================================

        // GET: Admin/AddLocker
        public ActionResult AddLocker()
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login");

            return View();
        }

        // POST: Admin/AddLocker
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddLocker(string lockerNumber, string location, string gender)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login");

            if (string.IsNullOrWhiteSpace(lockerNumber))
            {
                TempData["Error"] = "Locker number is required.";
                return RedirectToAction("AddLocker");
            }

            if (gender != "Male" && gender != "Female")
            {
                TempData["Error"] = "Please select a valid gender section for this locker.";
                return RedirectToAction("AddLocker");
            }

            // Prevent duplicate locker numbers within the same gender section
            bool exists = db.Lockers.Any(l => l.LockerNumber == lockerNumber && l.Gender == gender);
            if (exists)
            {
                TempData["Error"] = $"Locker {lockerNumber} already exists in the {gender} section.";
                return RedirectToAction("AddLocker");
            }

            var locker = new Locker
            {
                LockerNumber = lockerNumber,
                Location = location,
                Gender = gender,
                IsOccupied = false
            };

            db.Lockers.Add(locker);
            db.SaveChanges();

            TempData["Success"] = $"Locker {lockerNumber} added to the {gender} section.";
            return RedirectToAction("ManageLockers");
        }

        public ActionResult ManageLockers()
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login");

            ViewBag.PageTitle = "LOCKER MANAGEMENT";
            ViewBag.ActiveNav = "lockers";

            var lockers = db.Lockers.OrderBy(l => l.LockerNumber).ToList();

            // ── Who currently holds each occupied locker, and since when ──
            var activeReservations = db.LockerReservations
                .Include(r => r.Member)
                .Where(r => r.IsActive)
                .ToList()
                .ToDictionary(r => r.LockerId, r => r);

            ViewBag.ActiveReservations = activeReservations;

            // ── Open faults, newest first ──────────────────────────────────
            var openFaults = db.LockerFaults
                .Include(f => f.Locker)
                .Include(f => f.Technician)
                .Include(f => f.Reports)
                .Where(f => LockerFaultHelper.OpenStatuses.Contains(f.Status))
                .OrderByDescending(f => f.ReportedDate)
                .ToList();

            ViewBag.OpenFaults = openFaults;
            ViewBag.AwaitingTriageCount = openFaults.Count(f => f.Status == "Reported");
            ViewBag.ActiveFaultCount = openFaults.Count;
            ViewBag.Technicians = db.Technicians.Where(t => t.IsActive).OrderBy(t => t.FullName).ToList();

            return View(lockers);
        }

        // POST: Admin/ForceReleaseLocker/5
        // Lets admin end a reservation on the member's behalf — needed when a
        // member's account is cancelled, they lose email access (so ForgotPin
        // can't help), or the locker is needed for maintenance. Without this,
        // an occupied locker can never be freed by anyone but its holder.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForceReleaseLocker(int id, string reason)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login");

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Please provide a reason for force-releasing this locker.";
                return RedirectToAction("ManageLockers");
            }

            var locker = db.Lockers.Find(id);
            if (locker == null)
            {
                TempData["Error"] = "Locker not found.";
                return RedirectToAction("ManageLockers");
            }

            var activeReservation = db.LockerReservations
                .Include("Member")
                .FirstOrDefault(r => r.LockerId == id && r.IsActive);

            if (activeReservation == null)
            {
                TempData["Error"] = "This locker has no active reservation.";
                return RedirectToAction("ManageLockers");
            }

            int memberId = activeReservation.MemberId;
            string memberName = activeReservation.Member?.FullName ?? "the member";

            // Same teardown as the member's own EndReservation — PIN is destroyed
            activeReservation.AccessCodeHash = null;
            activeReservation.IsActive = false;
            activeReservation.EndedAt = DateTime.Now;
            activeReservation.FailedAttempts = 0;
            activeReservation.LockedUntil = null;
            activeReservation.WasForceReleased = true;
            activeReservation.ForceReleaseReason = reason.Trim();

            locker.IsOccupied = false;

            db.Entry(activeReservation).State = EntityState.Modified;
            db.Entry(locker).State = EntityState.Modified;
            db.SaveChanges();

            try
            {
                new NotificationService(db).Notify(
                    "Member", memberId,
                    "Locker Reservation Ended by Staff",
                    $"Your reservation for locker {locker.LockerNumber} was ended by gym staff. Reason: {reason.Trim()}. Please collect any belongings from reception.",
                    "Warning",
                    "/Locker/Index");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }

            TempData["Success"] = $"Locker {locker.LockerNumber} force-released from {memberName}.";
            return RedirectToAction("ManageLockers");
        }
        // POST: Admin/DeleteLocker/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteLocker(int id)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login");

            var locker = db.Lockers.Find(id);
            if (locker == null)
            {
                TempData["Error"] = "Locker not found.";
                return RedirectToAction("ManageLockers");
            }

            if (locker.IsOccupied)
            {
                TempData["Error"] = "Cannot delete a locker that is currently occupied.";
                return RedirectToAction("ManageLockers");
            }

            db.Lockers.Remove(locker);
            db.SaveChanges();

            TempData["Success"] = "Locker deleted.";
            return RedirectToAction("ManageLockers");
        }

        // ============================================================
        // OVERVIEW  (was the "overview" section of Dashboard)
        // ============================================================
        public ActionResult Overview()
        {
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");

            var members = db.Members.OrderByDescending(m => m.RegisteredAt).ToList();
            var payments = db.Payments.OrderByDescending(p => p.PaymentDate).ToList();

            var thisMonth = DateTime.Now.Month;
            var thisYear = DateTime.Now.Year;

            ViewBag.PageTitle = "DASHBOARD";
            ViewBag.ActiveNav = "overview";

            ViewBag.TotalMembers = members.Count;
            ViewBag.ActiveMembers = members.Count(m => m.Status == "Active");
            ViewBag.SuspendedMembers = members.Count(m => m.Status == "Suspended");
            ViewBag.FrozenMembers = members.Count(m => m.Status == "Frozen");

            ViewBag.TotalRevenue = members
                .Where(m => m.Status == "Active" || m.Status == "Frozen")
                .Sum(m => (decimal?)m.MonthlyFee) ?? 0;

            ViewBag.PaymentsThisMonth = payments.Count(p =>
                p.PaymentDate.Month == thisMonth && p.PaymentDate.Year == thisYear);

            ViewBag.BasicCount = members.Count(m => m.Plan.ToLower() == "basic");
            ViewBag.StandardCount = members.Count(m => m.Plan.ToLower() == "standard");
            ViewBag.PremiumCount = members.Count(m => m.Plan.ToLower() == "premium");

            ViewBag.RecentMembers = members.Take(5).ToList();

            return View();
        }

        // ============================================================
        // MEMBERS  (was the "members" section of Dashboard)
        // ============================================================
        public ActionResult Members()
        {
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");

            var members = db.Members.OrderByDescending(m => m.RegisteredAt).ToList();

            ViewBag.PageTitle = "MEMBERS";
            ViewBag.ActiveNav = "members";

            ViewBag.TotalMembers = members.Count;
            ViewBag.ActiveMembers = members.Count(m => m.Status == "Active");

            return View(members);
        }

        // ============================================================
        // PAYMENTS  (was the "payments" section of Dashboard)
        // ============================================================
        public ActionResult Payments()
        {
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");

            var payments = db.Payments.OrderByDescending(p => p.PaymentDate).ToList();

            ViewBag.PageTitle = "PAYMENTS";
            ViewBag.ActiveNav = "payments";

            ViewBag.TotalPayments = payments.Count;
            ViewBag.TotalPaymentsAmount = payments
                .Where(p => p.Status == "Paid" || p.Status == "Success")
                .Sum(p => (decimal?)p.Amount) ?? 0;

            return View(payments);
        }

        // ============================================================
        // REFUNDS  (was the "refunds" section — data still loads via GetRefunds AJAX)
        // ============================================================
        public ActionResult Refunds()
        {
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");

            ViewBag.PageTitle = "REFUND REQUESTS";
            ViewBag.ActiveNav = "refunds";

            return View();
        }

        // ============================================================
        // FREEZES  (was the "freezes" section — data still loads via GetFreezes AJAX)
        // ============================================================
        public ActionResult Freezes()
        {
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");

            ViewBag.PageTitle = "FREEZE REQUESTS";
            ViewBag.ActiveNav = "freezes";

            return View();
        }

        // ============================================================
        // ATTENDANCE  (was the "attendance" section — data still loads via GetTodayAttendance AJAX)
        // ============================================================
        public ActionResult Attendance()
        {
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");

            ViewBag.PageTitle = "MANAGE ATTENDANCE";
            ViewBag.ActiveNav = "attendance";

            return View();
        }
        // ══════════════════════════════════════════════════════════════
        // STORE
        // ══════════════════════════════════════════════════════════════
        public ActionResult StoreStaffManagement()
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login");

            ViewBag.StoreStaffList = db.StoreStaffs
                .OrderByDescending(s => s.CreatedAt)
                .ToList();
            ViewBag.PageTitle = "STORE STAFF MANAGEMENT";
            ViewBag.ActiveNav = "storestaff";

            return View("StoreStaffManagement", new StoreStaff());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateStoreStaff(StoreStaff storeStaff)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill in all required fields correctly.";
                return RedirectToAction("StoreStaffManagement");
            }

            if (db.StoreStaffs.Any(s => s.Email == storeStaff.Email))
            {
                TempData["Error"] = "A store staff member with this email already exists.";
                return RedirectToAction("StoreStaffManagement");
            }

            int next = db.StoreStaffs.Any()
                ? db.StoreStaffs.Max(s => s.StoreStaffId) + 1
                : 1;
            storeStaff.StoreStaffCode = "SS-" + next.ToString("D4");

            string tempPassword = GenerateTempPassword();
            storeStaff.PasswordHash = HashPassword(tempPassword);
            storeStaff.MustChangePassword = true;
            storeStaff.IsActive = true;
            storeStaff.Role = "StoreStaff";
            storeStaff.CreatedAt = DateTime.Now;

            db.StoreStaffs.Add(storeStaff);
            db.SaveChanges();

            try
            {
                EmailService.SendStoreStaffWelcome(
                    storeStaff.Email, storeStaff.FullName,
                    storeStaff.StoreStaffCode, tempPassword);
                TempData["Success"] = $"Store staff {storeStaff.FullName} added. Login details sent to {storeStaff.Email}.";
            }
            catch
            {
                TempData["Warning"] = $"Store staff {storeStaff.FullName} added but email could not be sent to {storeStaff.Email}.";
            }

            return RedirectToAction("StoreStaffManagement");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditStoreStaff(StoreStaff storeStaff)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login");

            var existing = db.StoreStaffs.Find(storeStaff.StoreStaffId);
            if (existing == null)
            {
                TempData["Error"] = "Store staff member not found.";
                return RedirectToAction("StoreStaffManagement");
            }

            existing.FullName = storeStaff.FullName;
            existing.Email = storeStaff.Email;
            existing.PhoneNumber = storeStaff.PhoneNumber;
            existing.Gender = storeStaff.Gender;
            existing.DateOfBirth = storeStaff.DateOfBirth;

            db.SaveChanges();

            TempData["Success"] = $"{existing.FullName} updated successfully.";
            return RedirectToAction("StoreStaffManagement");
        }

        public ActionResult DeleteStoreStaff(int id)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login");

            var storeStaff = db.StoreStaffs.Find(id);
            if (storeStaff == null)
            {
                TempData["Error"] = "Store staff member not found.";
                return RedirectToAction("StoreStaffManagement");
            }

            string name = storeStaff.FullName;
            db.StoreStaffs.Remove(storeStaff);
            db.SaveChanges();

            TempData["Success"] = $"Store staff {name} deleted successfully.";
            return RedirectToAction("StoreStaffManagement");
        }

        // NOTE: EmailService.SendStoreStaffWelcome needs adding to EmailService.cs,
        // mirroring the existing SendTechnicianWelcome/SendTrainerWelcome methods.
        // I don't have EmailService.cs, so this call is written to match the
        // established naming pattern but needs the actual method added there.

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}       