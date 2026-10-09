using SwiftyS26.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace SwiftyS26.Controllers
{
    public class DashboardController : Controller
    {
        private readonly GymHubDB db = new GymHubDB();

        private int? GetLoggedInMemberId() => Session["LoggedInMember"] as int?;
        private bool IsLoggedIn() => Session["LoggedInMember"] != null;

        // ── Helpers ──────────────────────────────────────────────────

        private DateTime CalculateNextBillingDate(DateTime registeredAt)
        {
            var today = DateTime.Today;
            int billingDay = registeredAt.Day;

            int daysInThisMonth = DateTime.DaysInMonth(today.Year, today.Month);
            int actualDay = Math.Min(billingDay, daysInThisMonth);
            var thisMonth = new DateTime(today.Year, today.Month, actualDay);
            if (thisMonth >= today) return thisMonth;

            var next = today.AddMonths(1);
            int daysInNextMonth = DateTime.DaysInMonth(next.Year, next.Month);
            int nextActualDay = Math.Min(billingDay, daysInNextMonth);
            return new DateTime(next.Year, next.Month, nextActualDay);
        }

        private decimal GetPlanFee(string plan)
        {
            switch (plan?.ToLower())
            {
                case "basic": return 199m;
                case "standard": return 399m;
                case "premium": return 699m;
                default: return 399m;
            }
        }

        private decimal CalculateProratedAmount(decimal oldFee, decimal newFee, DateTime nextBilling)
        {
            int days = Math.Max(1, (nextBilling - DateTime.Today).Days);
            decimal credit = Math.Round(days * (oldFee / 30m), 2);
            decimal charge = Math.Round(days * (newFee / 30m), 2);
            return Math.Round(charge - credit, 2);
        }

        // ── INDEX ────────────────────────────────────────────────────

        public ActionResult Index()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");

            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.Find(memberId);
            if (member == null) return RedirectToAction("Login", "Forms");

            member.Payments = db.Payments
                .Where(p => p.MemberId == memberId)
                .OrderByDescending(p => p.PaymentDate)
                .ToList();

            DateTime nextBilling = CalculateNextBillingDate(member.RegisteredAt);

            bool hasFutureExtendedExpiry = member.MembershipExpiry.HasValue
                && member.MembershipExpiry.Value.Date > nextBilling.Date;

            if (!hasFutureExtendedExpiry &&
                (!member.MembershipExpiry.HasValue ||
                 member.MembershipExpiry.Value.Date != nextBilling.Date))
            {
                member.MembershipExpiry = nextBilling;
                db.Entry(member).State = EntityState.Modified;
                db.SaveChanges();
            }

            AutoCompleteExpiredFreezes(memberId, member);
            db.Entry(member).Reload();

            bool isNew = (DateTime.Today - member.RegisteredAt.Date).TotalDays < 30;
            if (!isNew &&
                member.Status == "Active" &&
                member.MembershipExpiry.HasValue &&
                member.MembershipExpiry.Value < DateTime.Today.AddDays(-3))
            {
                member.Status = "Suspended";
                db.Entry(member).State = EntityState.Modified;
                db.SaveChanges();
            }

            if (member.Status == "Pending Cancellation" && DateTime.Today >= nextBilling)
            {
                member.Status = "Cancelled";
                db.Entry(member).State = EntityState.Modified;
                db.SaveChanges();
            }

            member.LastLoginAt = DateTime.Now;
            db.Entry(member).State = EntityState.Modified;
            db.SaveChanges();

            ViewBag.NextBillingDate = nextBilling.ToString("dd MMM yyyy");
            ViewBag.DaysRemaining = Math.Max(0, (int)(nextBilling - DateTime.Today).TotalDays);

            return View(member);
        }

        private void AutoCompleteExpiredFreezes(int memberId, MemberForm member)
        {
            var expiredFreezes = db.FreezeRequests
                .Where(f => f.MemberId == memberId &&
                            f.Status == "Approved" &&
                            f.FreezeEnd < DateTime.Today)
                .ToList();

            if (!expiredFreezes.Any()) return;

            foreach (var freeze in expiredFreezes)
                freeze.Status = "Completed";

            bool stillFrozen = db.FreezeRequests.Any(f =>
                f.MemberId == memberId &&
                f.Status == "Approved" &&
                f.FreezeEnd >= DateTime.Today);

            if (!stillFrozen && member.Status == "Frozen")
            {
                member.Status = "Active";
                db.Entry(member).State = EntityState.Modified;
            }

            db.SaveChanges();
        }

        // ── GET QR CODE ──────────────────────────────────────────────

        public ActionResult GetQrCode()
        {
            if (!IsLoggedIn())
                return Json(new { success = false, message = "Unauthorized" }, JsonRequestBehavior.AllowGet);

            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.Find(memberId);
            if (member == null)
                return Json(new { success = false, message = "Member not found." }, JsonRequestBehavior.AllowGet);

            if (string.IsNullOrWhiteSpace(member.QrCodeBase64))
            {
                member.QrCodeBase64 = MemberHelper.GenerateQrBase64(
                    member.MemberNumber, member.FullName, member.Plan, member.Email);
                db.Entry(member).State = EntityState.Modified;
                db.SaveChanges();
            }

            return Json(new
            {
                success = true,
                qrBase64 = member.QrCodeBase64,
                memberNumber = member.MemberNumber,
                memberName = member.FullName,
                plan = member.Plan.ToUpper()
            }, JsonRequestBehavior.AllowGet);
        }

        // ── CHANGE PLAN ──────────────────────────────────────────────

        [HttpPost]
        public ActionResult ChangePlan(string newPlan)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            var validPlans = new[] { "basic", "standard", "premium" };
            if (string.IsNullOrWhiteSpace(newPlan) || !validPlans.Contains(newPlan.ToLower()))
                return Json(new { success = false, message = "Invalid plan." });

            newPlan = newPlan.ToLower();
            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.Find(memberId);
            if (member == null)
                return Json(new { success = false, message = "Member not found." });

            if (member.Status == "Cancelled")
                return Json(new { success = false, message = "Membership is cancelled." });
            if (member.Status == "Suspended")
                return Json(new { success = false, message = "Membership is suspended." });
            if (member.Status == "Frozen")
                return Json(new { success = false, message = "Membership is currently frozen. You cannot change plans during a freeze." });
            if (member.Status == "Pending Cancellation")
                return Json(new { success = false, message = "Cancellation pending — cannot change plan." });
            if (member.Plan.ToLower() == newPlan)
                return Json(new { success = false, message = $"Already on {newPlan.ToUpper()}." });

            string oldPlan = member.Plan;
            decimal oldFee = member.MonthlyFee;
            decimal newFee = GetPlanFee(newPlan);
            string changeType = newFee > oldFee ? "Upgrade" : "Downgrade";
            DateTime nextBilling = CalculateNextBillingDate(member.RegisteredAt);
            decimal prorate = CalculateProratedAmount(oldFee, newFee, nextBilling);

            member.Plan = newPlan;
            member.MonthlyFee = newFee;
            member.QrCodeBase64 = MemberHelper.GenerateQrBase64(
                member.MemberNumber, member.FullName, newPlan, member.Email);

            db.Entry(member).State = EntityState.Modified;
            db.SaveChanges();

            if (prorate != 0)
            {
                db.Payments.Add(new Payment
                {
                    MemberId = memberId,
                    PaymentDate = DateTime.Now,
                    Description = $"Plan {changeType}: {oldPlan.ToUpper()} to {newPlan.ToUpper()} (prorated)",
                    Method = "Prorated Adjustment",
                    Amount = Math.Abs(prorate),
                    Status = prorate > 0 ? "Pending" : "Credit",
                    Reference = $"PRORATE-{member.MemberNumber}-{DateTime.Now:yyyyMMddHHmm}"
                });
                db.SaveChanges();
            }
            new NotificationService(db).Notify(
    "Member", memberId,
    $"Plan {changeType}d",
    $"You're now on the {newPlan.ToUpper()} plan (R{newFee:F2}/mo), effective immediately.",
    "Success",
    "/Dashboard/Membership");


            try
            {
                EmailService.SendPlanChange(
                    member.Email, member.FirstName, oldPlan, newPlan,
                    oldFee, newFee, changeType, nextBilling.ToString("dd MMM yyyy"));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[EMAIL] {ex.Message}"); }

            string proratedMsg = prorate > 0
                ? $" Prorated charge of R{prorate:F2} logged."
                : prorate < 0
                    ? $" Prorated credit of R{Math.Abs(prorate):F2} applied."
                    : "";

            return Json(new
            {
                success = true,
                changeType,
                newPlan,
                newFee = newFee.ToString("F2"),
                proratedDiff = prorate.ToString("F2"),
                effectiveDate = "immediately",
                message = $"Plan changed to {newPlan.ToUpper()} immediately.{proratedMsg}"
            });
        }

        // ── CANCEL MEMBERSHIP ────────────────────────────────────────

        [HttpPost]
        public ActionResult CancelMembership()
        {
            if (!IsLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.Find(memberId);
            if (member == null)
                return Json(new { success = false, message = "Member not found." });

            if (member.Status == "Cancelled")
                return Json(new { success = false, message = "Already cancelled." });
            if (member.Status == "Pending Cancellation")
                return Json(new { success = false, message = "Cancellation already processing." });
            if (member.Status == "Suspended")
                return Json(new { success = false, message = "Membership suspended. Contact support." });
            if (member.Status == "Frozen")
                return Json(new { success = false, message = "Membership is frozen. Please wait for the freeze to end or contact support." });

            DateTime nextBilling = CalculateNextBillingDate(member.RegisteredAt);
            bool coolingOff = (DateTime.Today - member.RegisteredAt.Date).TotalDays <= 7;

            if (coolingOff)
            {
                decimal refundAmt = member.MonthlyFee + 150m;
                member.Status = "Cancelled";
                db.Entry(member).State = EntityState.Modified;

                db.RefundRequests.Add(new RefundRequest
                {
                    MemberId = memberId,
                    MemberNumber = member.MemberNumber,
                    RefundType = "Cooling-off Period",
                    RefundAmount = refundAmt,
                    Status = "Pending",
                    RequestedAt = DateTime.Now,
                    Notes = $"Cooling-off: R{member.MonthlyFee:F2} + R150 joining = R{refundAmt:F2}"
                });
                db.SaveChanges();
                new NotificationService(db).Notify(
    "Member", memberId,
    "Membership Cancelled",
    $"Your cooling-off cancellation is complete. A refund of R{refundAmt:F2} has been submitted.",
    "Info",
    "/Dashboard/Payments");

                try
                {
                    EmailService.SendImmediateCancellation(
                        member.Email, member.FirstName, member.MemberNumber, refundAmt, true);
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[EMAIL] {ex.Message}"); }

                return Json(new
                {
                    success = true,
                    immediate = true,
                    isCoolingOff = true,
                    refundAmount = refundAmt.ToString("F2"),
                    message = $"Cancelled immediately. Refund of R{refundAmt:F2} submitted."
                });
            }
            else
            {
                member.Status = "Pending Cancellation";
                db.Entry(member).State = EntityState.Modified;
                db.SaveChanges();
                new NotificationService(db).Notify(
    "Member", memberId,
    "Cancellation Scheduled",
    $"Your membership will end on {nextBilling:dd MMM yyyy}. You'll keep access until then.",
    "Info",
    "/Dashboard/Membership");

                try
                {
                    EmailService.SendCancellationConfirmation(
                        member.Email, member.FirstName, member.MemberNumber,
                        nextBilling.ToString("dd MMM yyyy"));
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[EMAIL] {ex.Message}"); }

                return Json(new
                {
                    success = true,
                    immediate = false,
                    isCoolingOff = false,
                    accessUntil = nextBilling.ToString("dd MMM yyyy"),
                    message = $"Access continues until {nextBilling:dd MMM yyyy}. No further payments."
                });
            }
        }

        // ── REQUEST FREEZE ───────────────────────────────────────────

        [HttpPost]
        public ActionResult RequestFreeze(string freezeStart, string freezeEnd, string reason)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.Find(memberId);
            if (member == null)
                return Json(new { success = false, message = "Member not found." });

            if (member.Status != "Active")
                return Json(new
                {
                    success = false,
                    message = $"Only active members can request a freeze. Your current status is: <strong>{member.Status}</strong>."
                });

            if (db.Payments.Any(p => p.MemberId == memberId && p.Status == "Pending"))
                return Json(new
                {
                    success = false,
                    message = "You have outstanding pending payments. Please clear them before requesting a freeze."
                });

            if (db.FreezeRequests.Any(f =>
                f.MemberId == memberId &&
                (f.Status == "Pending" || f.Status == "Approved") &&
                f.FreezeEnd >= DateTime.Today))
                return Json(new
                {
                    success = false,
                    message = "You already have a pending or active freeze request."
                });

            DateTime startDate, endDate;
            if (!DateTime.TryParse(freezeStart, out startDate) ||
                !DateTime.TryParse(freezeEnd, out endDate))
                return Json(new { success = false, message = "Invalid date format." });

            startDate = startDate.Date;
            endDate = endDate.Date;

            if (startDate < DateTime.Today)
                return Json(new { success = false, message = "Freeze start date cannot be in the past." });
            if (endDate <= startDate)
                return Json(new { success = false, message = "End date must be after the start date." });

            int days = (int)(endDate - startDate).TotalDays;
            if (days < 7)
                return Json(new { success = false, message = "Minimum freeze duration is 7 days." });
            if (days > 90)
                return Json(new { success = false, message = "Maximum freeze duration is 90 days." });

            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
                return Json(new { success = false, message = "Please provide a reason of at least 10 characters." });

            var freeze = new FreezeRequest
            {
                MemberId = memberId,
                MemberNumber = member.MemberNumber,
                FreezeStart = startDate,
                FreezeEnd = endDate,
                Reason = reason.Trim(),
                Status = "Pending",
                RequestedAt = DateTime.Now
            };

            db.FreezeRequests.Add(freeze);
            db.SaveChanges();

            try
            {
                EmailService.SendFreezeRequested(
                    member.Email, member.FirstName, member.MemberNumber,
                    startDate.ToString("dd MMM yyyy"),
                    endDate.ToString("dd MMM yyyy"),
                    days);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[EMAIL] {ex.Message}"); }

            return Json(new
            {
                success = true,
                message = $"Freeze request submitted for {startDate:dd MMM yyyy} – {endDate:dd MMM yyyy} ({days} days). Pending admin approval.",
                freezeRequestId = freeze.FreezeRequestId,
                freezeStart = startDate.ToString("dd MMM yyyy"),
                freezeEnd = endDate.ToString("dd MMM yyyy"),
                days
            });
        }

        // ── GET FREEZE STATUS ────────────────────────────────────────

        public ActionResult GetFreezeStatus()
        {
            if (!IsLoggedIn())
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            int memberId = GetLoggedInMemberId().Value;

            var freezes = db.FreezeRequests
                .Where(f => f.MemberId == memberId)
                .OrderByDescending(f => f.RequestedAt)
                .ToList();

            var data = freezes.Select(f => new
            {
                freezeRequestId = f.FreezeRequestId,
                freezeStart = f.FreezeStart.ToString("dd MMM yyyy"),
                freezeEnd = f.FreezeEnd.ToString("dd MMM yyyy"),
                days = f.FreezeDurationDays,
                reason = f.Reason,
                status = f.Status,
                requestedAt = f.RequestedAt.ToString("dd MMM yyyy"),
                processedAt = f.ProcessedAt.HasValue ? f.ProcessedAt.Value.ToString("dd MMM yyyy") : null,
                adminNotes = f.AdminNotes,
                extendedExpiry = f.ExtendedExpiry.HasValue ? f.ExtendedExpiry.Value.ToString("dd MMM yyyy") : null,
                isCurrentlyFrozen = f.IsCurrentlyFrozen
            }).ToList();

            var activeMember = db.Members.Find(memberId);

            return Json(new
            {
                success = true,
                freezes = data,
                memberStatus = activeMember?.Status ?? "—",
                hasPendingFreeze = freezes.Any(f => f.Status == "Pending"),
                hasActiveFreeze = freezes.Any(f => f.IsCurrentlyFrozen)
            }, JsonRequestBehavior.AllowGet);
        }

        // ── DOWNLOAD STATEMENT ───────────────────────────────────────

        public ActionResult DownloadStatement()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");

            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.Find(memberId);
            if (member == null) return RedirectToAction("Index");

            var payments = db.Payments
                .Where(p => p.MemberId == memberId)
                .OrderByDescending(p => p.PaymentDate)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='UTF-8'/><style>");
            sb.AppendLine("body{font-family:Arial;padding:40px;color:#111}h1{color:#1A5FD4}");
            sb.AppendLine("table{width:100%;border-collapse:collapse;font-size:13px}");
            sb.AppendLine("th{background:#1A5FD4;color:#fff;padding:10px;text-align:left}");
            sb.AppendLine("td{padding:10px;border-bottom:1px solid #e5e7eb}");
            sb.AppendLine(".paid,.success{color:#16a34a;font-weight:700}.pending{color:#d97706;font-weight:700}");
            sb.AppendLine(".failed{color:#dc2626;font-weight:700}.credit{color:#2563eb;font-weight:700}");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine($"<h1>GymHub Statement — {DateTime.Now:dd MMM yyyy}</h1>");
            sb.AppendLine($"<p><b>{member.FullName}</b> | {member.MemberNumber} | {member.Plan.ToUpper()} | R{member.MonthlyFee:F2}/mo</p>");
            sb.AppendLine("<table><thead><tr><th>Date</th><th>Description</th><th>Method</th><th>Amount</th><th>Status</th></tr></thead><tbody>");

            if (payments.Any())
            {
                decimal total = 0;
                foreach (var p in payments)
                {
                    sb.AppendLine($"<tr><td>{p.PaymentDate:dd MMM yyyy}</td><td>{p.Description}</td><td>{p.Method}</td><td>R{p.Amount:F2}</td><td class='{p.Status.ToLower()}'>{p.Status}</td></tr>");
                    if (p.Status == "Paid" || p.Status == "Success") total += p.Amount;
                }
                sb.AppendLine($"<tr style='font-weight:700;background:#f4f8ff'><td colspan='3'>TOTAL PAID</td><td colspan='2'>R{total:F2}</td></tr>");
            }
            else
            {
                sb.AppendLine("<tr><td colspan='5' style='text-align:center;padding:24px;color:#999'>No records found.</td></tr>");
            }

            sb.AppendLine("</tbody></table>");
            sb.AppendLine("<div style='margin-top:32px;font-size:11px;color:#999'>GymHub (Pty) Ltd | Durban, KZN | info@gymhub.co.za</div>");
            sb.AppendLine("</body></html>");

            return File(
                Encoding.UTF8.GetBytes(sb.ToString()),
                "text/html",
                $"GymHub_Statement_{member.MemberNumber}_{DateTime.Now:yyyyMMdd}.html");
        }

        // ── LOGOUT ───────────────────────────────────────────────────

        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Login", "Forms");
        }

        // ── GET PROFILE ──────────────────────────────────────────────

        public ActionResult GetProfile()
        {
            if (!IsLoggedIn())
                return Json(new { success = false, message = "Unauthorized" }, JsonRequestBehavior.AllowGet);

            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.Find(memberId);
            if (member == null)
                return Json(new { success = false, message = "Not found." }, JsonRequestBehavior.AllowGet);

            DateTime nextBilling = CalculateNextBillingDate(member.RegisteredAt);

            return Json(new
            {
                success = true,
                firstName = member.FirstName,
                lastName = member.LastName,
                phone = member.Phone,
                email = member.Email,
                idNumber = member.IdNumber,
                dob = member.DateOfBirth.ToString("yyyy-MM-dd"),
                gender = member.Gender,
                plan = member.Plan,
                status = member.Status,
                memberNumber = member.MemberNumber,
                registeredAt = member.RegisteredAt.ToString("dd MMM yyyy"),
                nextBillingDate = nextBilling.ToString("dd MMM yyyy"),
                daysRemaining = Math.Max(0, (int)(nextBilling - DateTime.Today).TotalDays),
                monthlyFee = member.MonthlyFee,
                expiry = member.MembershipExpiry.HasValue
                    ? member.MembershipExpiry.Value.ToString("yyyy-MM-dd")
                    : nextBilling.ToString("yyyy-MM-dd")
            }, JsonRequestBehavior.AllowGet);
        }

        // ── UPDATE PROFILE ───────────────────────────────────────────

        [HttpPost]
        public ActionResult UpdateProfile()
        {
            if (!IsLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            string firstName = (Request.Form["FirstName"] ?? "").Trim();
            string lastName = (Request.Form["LastName"] ?? "").Trim();
            string email = (Request.Form["Email"] ?? "").Trim().ToLower();
            string phone = (Request.Form["Phone"] ?? "").Trim();

            if (string.IsNullOrWhiteSpace(firstName)) return Json(new { success = false, message = "First name required." });
            if (string.IsNullOrWhiteSpace(lastName)) return Json(new { success = false, message = "Last name required." });
            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@")) return Json(new { success = false, message = "Valid email required." });
            if (string.IsNullOrWhiteSpace(phone)) return Json(new { success = false, message = "Phone required." });

            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.Find(memberId);
            if (member == null)
                return Json(new { success = false, message = "Not found." });

            if (db.Members.Any(m => m.Email.ToLower() == email && m.MemberId != memberId))
                return Json(new { success = false, message = "Email taken by another account." });
            if (db.Members.Any(m => m.Phone == phone && m.MemberId != memberId))
                return Json(new { success = false, message = "Phone taken by another account." });

            member.FirstName = firstName;
            member.LastName = lastName;
            member.Email = email;
            member.Phone = phone;
            member.QrCodeBase64 = MemberHelper.GenerateQrBase64(
                member.MemberNumber, member.FullName, member.Plan, member.Email);

            db.Entry(member).State = EntityState.Modified;
            db.SaveChanges();

            Session["LoggedInName"] = member.FullName;

            return Json(new
            {
                success = true,
                message = "Profile updated!",
                fullName = member.FullName,
                email = member.Email,
                phone = member.Phone
            });
        }

        // ── FIX SUSPENDED ────────────────────────────────────────────

        public ActionResult FixSuspended()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");

            var wronglySuspended = db.Members
                .Where(m => m.Status == "Suspended" &&
                            DbFunctions.DiffDays(m.RegisteredAt, DateTime.Today) < 30)
                .ToList();

            foreach (var m in wronglySuspended)
                m.Status = "Active";

            db.SaveChanges();
            return Content($"Fixed {wronglySuspended.Count} wrongly suspended members.");
        }

        // ── WALLET ───────────────────────────────────────────────────

        [HttpPost]
        public ActionResult ConvertWalletToPoints(decimal amount)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            int memberId = GetLoggedInMemberId().Value;

            if (amount < 10m)
                return Json(new { success = false, message = "Minimum conversion is R10.00 (= 100 points)." });
            if (amount > 5000m)
                return Json(new { success = false, message = "Maximum conversion per transaction is R5,000.00." });

            var wallet = db.EWallets.FirstOrDefault(w => w.MemberId == memberId);
            if (wallet == null || wallet.Balance < amount)
                return Json(new { success = false, message = "Insufficient wallet balance." });

            int pointsEarned = (int)(amount * 10);

            wallet.Balance -= amount;
            wallet.LastUpdated = DateTime.Now;

            db.WalletTransactions.Add(new WalletTransaction
            {
                MemberId = memberId,
                TransactionType = "TopUpToPoints",
                Amount = amount,
                Description = $"Converted R{amount:F2} to {pointsEarned} loyalty points",
                CreatedAt = DateTime.Now
            });

            var loyalty = db.LoyaltyAccounts.FirstOrDefault(l => l.MemberId == memberId);
            if (loyalty == null)
            {
                loyalty = new LoyaltyAccount
                {
                    MemberId = memberId,
                    CurrentPoints = 0,
                    TotalPointsEarned = 0,
                    CreatedAt = DateTime.Now,
                    LastUpdated = DateTime.Now
                };
                db.LoyaltyAccounts.Add(loyalty);
                db.SaveChanges();
            }

            loyalty.CurrentPoints += pointsEarned;
            loyalty.TotalPointsEarned += pointsEarned;
            loyalty.LastUpdated = DateTime.Now;

            db.PointsTransactions.Add(new PointsTransaction
            {
                MemberId = memberId,
                TransactionType = "Converted",
                Points = pointsEarned,
                Description = $"Converted R{amount:F2} wallet credit to {pointsEarned} points",
                CreatedAt = DateTime.Now
            });

            db.SaveChanges();

            return Json(new
            {
                success = true,
                message = $"✅ R{amount:F2} converted to {pointsEarned} points!",
                pointsEarned,
                newPointsBalance = loyalty.CurrentPoints,
                newWalletBalance = wallet.Balance.ToString("F2")
            });
        }

        public ActionResult GetCafeProducts(bool featured = false)
        {
            if (!IsLoggedIn())
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            var query = db.CafeProducts
                .Where(p => p.IsActive)
                .OrderBy(p => p.PointsCost);

            var products = (featured ? query.Take(4) : query)
                .ToList()
                .Select(p => new
                {
                    id = p.CafeProductId,
                    name = p.Name,
                    description = p.Description,
                    emoji = p.Emoji,
                    category = p.Category,
                    pointsCost = p.PointsCost,
                    randValue = p.RandValue.ToString("F2"),
                    inStock = p.InStock
                }).ToList();

            return Json(new { success = true, products, total = products.Count }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult RedeemProduct(int productId)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.Find(memberId);
            if (member == null)
                return Json(new { success = false, message = "Member not found." });

            var product = db.CafeProducts.Find(productId);
            if (product == null || !product.IsActive)
                return Json(new { success = false, message = "Product not found or unavailable." });

            if (!product.InStock)
                return Json(new { success = false, message = $"Sorry, {product.Name} is currently out of stock." });

            if (product.DailyRedemptionLimit > 0)
            {
                int todayCount = db.CafeRedemptions.Count(r =>
                    r.MemberId == memberId &&
                    r.CafeProductId == productId &&
                    r.RedeemedAt >= DateTime.Today);

                if (todayCount >= product.DailyRedemptionLimit)
                    return Json(new { success = false, message = $"You have reached the daily limit for {product.Name}." });
            }

            var loyalty = db.LoyaltyAccounts.FirstOrDefault(l => l.MemberId == memberId);
            if (loyalty == null || loyalty.CurrentPoints < product.PointsCost)
                return Json(new
                {
                    success = false,
                    message = $"Insufficient points. You need {product.PointsCost} pts but have {loyalty?.CurrentPoints ?? 0} pts."
                });

            string collectionCode = GenerateCollectionCode();

            loyalty.CurrentPoints -= product.PointsCost;
            loyalty.LastUpdated = DateTime.Now;

            db.PointsTransactions.Add(new PointsTransaction
            {
                MemberId = memberId,
                TransactionType = "Redeemed",
                Points = -product.PointsCost,
                Description = $"Redeemed {product.Emoji} {product.Name} for {product.PointsCost} pts",
                CreatedAt = DateTime.Now
            });

            var redemption = new CafeRedemption
            {
                MemberId = memberId,
                CafeProductId = productId,
                PointsSpent = product.PointsCost,
                RandValue = product.RandValue,
                CollectionCode = collectionCode,
                Status = "Pending",
                RedeemedAt = DateTime.Now
            };
            db.CafeRedemptions.Add(redemption);
            db.SaveChanges();
            try
            {
                new NotificationService(db).Notify(
                    "Member", memberId,
                    "Café Order Ready",
                    $"{product.Emoji} {product.Name} redeemed — show code {collectionCode} at the counter.",
                    "Success",
                    "/Dashboard/Wallet");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }

            try
            {
                EmailService.SendRedemptionConfirmation(
                    member.Email, member.FirstName,
                    product.Name, product.Emoji,
                    product.PointsCost, collectionCode,
                    redemption.CafeRedemptionId);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[REDEMPTION EMAIL] {ex.Message}"); }

            string qrContent = $"GH-CAFE|ORDER:{redemption.CafeRedemptionId}|CODE:{collectionCode}|EXP:{DateTime.Now.AddMinutes(30):yyyyMMddHHmmss}";
            string qrBase64 = GenerateOrderQrBase64(qrContent);

            return Json(new
            {
                success = true,
                message = $"✅ {product.Emoji} {product.Name} redeemed! Show your QR code at the café counter.",
                collectionCode,
                qrBase64,
                productName = product.Name,
                pointsSpent = product.PointsCost,
                newPointsBalance = loyalty.CurrentPoints,
                redemptionId = redemption.CafeRedemptionId
            });
        }

        private string GenerateCollectionCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var rng = new Random();
            string code;
            int attempts = 0;
            do
            {
                code = new string(Enumerable.Repeat(chars, 6)
                    .Select(s => s[rng.Next(s.Length)]).ToArray());
                attempts++;
            }
            while (attempts < 10 && db.CafeRedemptions.Any(r =>
                r.CollectionCode == code && r.RedeemedAt >= DateTime.Today));
            return code;
        }

        private string GenerateOrderQrBase64(string content)
        {
            using (var qrGenerator = new QRCoder.QRCodeGenerator())
            using (var qrData = qrGenerator.CreateQrCode(content, QRCoder.QRCodeGenerator.ECCLevel.Q))
            using (var qrCode = new QRCoder.QRCode(qrData))
            using (var bitmap = qrCode.GetGraphic(10))
            using (var ms = new System.IO.MemoryStream())
            {
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
            }
        }

        public ActionResult GetRedemptionHistory()
        {
            if (!IsLoggedIn())
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            int memberId = GetLoggedInMemberId().Value;

            var redemptions = db.CafeRedemptions
                .Where(r => r.MemberId == memberId)
                .OrderByDescending(r => r.RedeemedAt)
                .Take(20)
                .ToList();

            var data = redemptions.Select(r => new
            {
                redemptionId = r.CafeRedemptionId,
                productName = r.Product != null ? r.Product.Name : "—",
                emoji = r.Product != null ? r.Product.Emoji : "🛒",
                pointsSpent = r.PointsSpent,
                collectionCode = r.CollectionCode,
                status = r.Status,
                redeemedAt = r.RedeemedAt.ToString("dd MMM yyyy HH:mm"),
                collectedAt = r.CollectedAt.HasValue ? r.CollectedAt.Value.ToString("dd MMM yyyy HH:mm") : null
            }).ToList();

            return Json(new { success = true, redemptions = data }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetLoyaltySummary()
        {
            if (!IsLoggedIn())
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            int memberId = GetLoggedInMemberId().Value;

            var loyalty = db.LoyaltyAccounts.FirstOrDefault(l => l.MemberId == memberId);
            if (loyalty == null)
            {
                loyalty = new LoyaltyAccount
                {
                    MemberId = memberId,
                    CurrentPoints = 0,
                    TotalPointsEarned = 0,
                    CreatedAt = DateTime.Now,
                    LastUpdated = DateTime.Now
                };
                db.LoyaltyAccounts.Add(loyalty);
                db.SaveChanges();
            }

            var wallet = db.EWallets.FirstOrDefault(w => w.MemberId == memberId);
            if (wallet == null)
            {
                wallet = new EWallet
                {
                    MemberId = memberId,
                    Balance = 0m,
                    CreatedAt = DateTime.Now,
                    LastUpdated = DateTime.Now
                };
                db.EWallets.Add(wallet);
                db.SaveChanges();
            }

            return Json(new
            {
                success = true,
                currentPoints = loyalty.CurrentPoints,
                totalPointsEarned = loyalty.TotalPointsEarned,
                walletBalance = wallet.Balance.ToString("F2")
            }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetPointsHistory()
        {
            if (!IsLoggedIn())
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            int memberId = GetLoggedInMemberId().Value;

            var transactions = db.PointsTransactions
                .Where(p => p.MemberId == memberId)
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            var data = transactions.Select(t => new
            {
                transactionType = t.TransactionType,
                points = t.Points,
                description = t.Description,
                createdAt = t.CreatedAt.ToString("dd MMM yyyy HH:mm")
            }).ToList();

            return Json(new { success = true, transactions = data }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetWalletHistory()
        {
            if (!IsLoggedIn())
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            int memberId = GetLoggedInMemberId().Value;

            var transactions = db.WalletTransactions
                .Where(w => w.MemberId == memberId)
                .OrderByDescending(w => w.CreatedAt)
                .ToList();

            var data = transactions.Select(t => new
            {
                transactionType = t.TransactionType,
                amount = t.Amount.ToString("F2"),
                description = t.Description,
                createdAt = t.CreatedAt.ToString("dd MMM yyyy HH:mm")
            }).ToList();

            return Json(new { success = true, transactions = data }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult InitializeTopUp(decimal amount)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            if (amount < 10m || amount > 5000m)
                return Json(new { success = false, message = "Amount must be between R10.00 and R5,000.00." });

            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.FirstOrDefault(m => m.MemberId == memberId);
            if (member == null)
                return Json(new { success = false, message = "Member not found." });

            string reference = $"GH-TOPUP-{DateTime.Now:yyyyMMddHHmmss}-" +
                               Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();

            Session["TopUpRef"] = reference;
            Session["TopUpAmount"] = amount;

            string publicKey = ConfigurationManager.AppSettings["Paystack:PublicKey"];

            return Json(new
            {
                success = true,
                reference,
                publicKey,
                email = member.Email,
                amount = (int)(amount * 100),
                currency = "ZAR",
                firstName = member.FirstName,
                lastName = member.LastName
            });
        }

        [HttpPost]
        public async Task<ActionResult> VerifyTopUp(string reference)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            if (string.IsNullOrWhiteSpace(reference))
                return Json(new { success = false, message = "No reference provided." });

            var sessionRef = Session["TopUpRef"] as string;
            if (sessionRef == null || sessionRef != reference)
                return Json(new { success = false, message = "Reference mismatch." });

            string secretKey = ConfigurationManager.AppSettings["Paystack:SecretKey"];
            int memberId = GetLoggedInMemberId().Value;

            try
            {
                var http = new System.Net.Http.HttpClient();
                var request = new System.Net.Http.HttpRequestMessage(
                    System.Net.Http.HttpMethod.Get,
                    $"https://api.paystack.co/transaction/verify/{reference}");
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", secretKey);

                var response = await http.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                dynamic result = Newtonsoft.Json.JsonConvert.DeserializeObject(json);

                if (result.status != true || result.data.status != "success")
                    return Json(new { success = false, message = "Payment not successful. Please try again." });

                decimal amount = (decimal)(Session["TopUpAmount"] ?? 0m);

                var wallet = db.EWallets.FirstOrDefault(w => w.MemberId == memberId);
                if (wallet == null)
                {
                    wallet = new EWallet { MemberId = memberId, Balance = 0m, CreatedAt = DateTime.Now };
                    db.EWallets.Add(wallet);
                    await db.SaveChangesAsync();
                }

                wallet.Balance += amount;
                wallet.LastUpdated = DateTime.Now;

                db.WalletTransactions.Add(new WalletTransaction
                {
                    MemberId = memberId,
                    TransactionType = "TopUp",
                    Amount = amount,
                    Description = $"Wallet top-up via Paystack — R{amount:F2}",
                    CreatedAt = DateTime.Now
                });

                db.Payments.Add(new Payment
                {
                    MemberId = memberId,
                    Reference = reference,
                    TransactionId = result.data.id.ToString(),
                    PaymentDate = DateTime.Now,
                    Description = $"GymHub wallet top-up — R{amount:F2}",
                    Method = result.data.channel.ToString(),
                    Amount = amount,
                    Status = "Success"
                });

                await db.SaveChangesAsync();
                try
                {
                    new NotificationService(db).Notify(
                        "Member", memberId,
                        "Wallet Topped Up",
                        $"R{amount:F2} added to your wallet.",
                        "Success",
                        "/Dashboard/Wallet");
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }

                Session.Remove("TopUpRef");
                Session.Remove("TopUpAmount");

                return Json(new
                {
                    success = true,
                    message = $"✅ R{amount:F2} added to your wallet!",
                    newBalance = wallet.Balance.ToString("F2"),
                    amountAdded = amount.ToString("F2")
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TOPUP ERROR] {ex.Message}");
                return Json(new { success = false, message = "Verification failed. Please contact support." });
            }
        }

        // ── FITNESS ──────────────────────────────────────────────────

        private decimal CalculateBMI(decimal weightKg, decimal heightCm)
        {
            if (heightCm <= 0) return 0;
            decimal heightM = heightCm / 100m;
            return Math.Round(weightKg / (heightM * heightM), 1);
        }

        private string GetBMICategory(decimal bmi)
        {
            if (bmi < 18.5m) return "Underweight";
            if (bmi < 25.0m) return "Normal";
            if (bmi < 30.0m) return "Overweight";
            return "Obese";
        }

        private decimal CalculateBMR(decimal weightKg, decimal heightCm, int age, string gender)
        {
            decimal bmr = (10m * weightKg) + (6.25m * heightCm) - (5m * age);
            bmr += (gender?.ToLower() == "female") ? -161m : 5m;
            return Math.Round(bmr, 0);
        }

        private decimal GetActivityMultiplier(string activityLevel)
        {
            switch (activityLevel?.ToLower())
            {
                case "sedentary": return 1.2m;
                case "lightly active": return 1.375m;
                case "moderately active": return 1.55m;
                case "very active": return 1.725m;
                case "extremely active": return 1.9m;
                default: return 1.375m;
            }
        }

        private string RecommendGoal(decimal bmi, decimal bodyFatPercent, string gender)
        {
            bool isFemale = gender?.ToLower() == "female";
            if ((isFemale && bodyFatPercent > 32) || (!isFemale && bodyFatPercent > 25)) return "Weight Loss";
            if (bmi < 18.5m || (!isFemale && bodyFatPercent < 10) || (isFemale && bodyFatPercent < 16)) return "Muscle Gain";
            if (bmi >= 25m) return "Weight Loss";
            if ((!isFemale && bodyFatPercent > 18) || (isFemale && bodyFatPercent > 28)) return "Weight Loss";
            return "Maintenance";
        }

        private int CalculateProjectedWeeks(string goalType, decimal currentWeight,
            decimal targetWeight, decimal currentBodyFat, decimal targetBodyFat)
        {
            switch (goalType)
            {
                case "Weight Loss":
                    int weeksByWeight = (int)Math.Ceiling(Math.Max(currentWeight - targetWeight, 1m) / 0.5m);
                    return Math.Min(Math.Max(weeksByWeight, 4), 52);
                case "Muscle Gain":
                    int weeksByMuscle = (int)Math.Ceiling(Math.Max(targetWeight - currentWeight, 1m) / 0.25m);
                    return Math.Min(Math.Max(weeksByMuscle, 8), 52);
                case "Endurance":
                    return 12;
                default:
                    return 8;
            }
        }

        [HttpPost]
        public ActionResult SaveFitnessProfile(
            decimal heightCm, decimal weightKg, decimal bodyFatPercent,
            string activityLevel, string goalType,
            decimal targetWeightKg, decimal targetBodyFatPercent)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.Find(memberId);
            if (member == null)
                return Json(new { success = false, message = "Member not found." });

            if (heightCm < 100 || heightCm > 250) return Json(new { success = false, message = "Height must be between 100 cm and 250 cm." });
            if (weightKg < 30 || weightKg > 300) return Json(new { success = false, message = "Weight must be between 30 kg and 300 kg." });
            if (bodyFatPercent < 3 || bodyFatPercent > 60) return Json(new { success = false, message = "Body fat % must be between 3% and 60%." });
            if (targetWeightKg < 30 || targetWeightKg > 300) return Json(new { success = false, message = "Target weight must be between 30 kg and 300 kg." });

            var validGoals = new[] { "Weight Loss", "Muscle Gain", "Endurance", "Maintenance" };
            if (!validGoals.Contains(goalType))
                return Json(new { success = false, message = "Invalid goal type." });

            int age = member.Age > 0 ? member.Age : 25;
            string gender = member.Gender ?? "Male";

            decimal bmi = CalculateBMI(weightKg, heightCm);
            string bmiCategory = GetBMICategory(bmi);
            decimal bmr = CalculateBMR(weightKg, heightCm, age, gender);
            decimal tdee = Math.Round(bmr * GetActivityMultiplier(activityLevel), 0);
            string recommendedGoal = RecommendGoal(bmi, bodyFatPercent, gender);
            string finalGoal = string.IsNullOrWhiteSpace(goalType) ? recommendedGoal : goalType;
            int projectedWeeks = CalculateProjectedWeeks(finalGoal, weightKg, targetWeightKg, bodyFatPercent, targetBodyFatPercent);
            DateTime projectedDate = DateTime.Today.AddDays(projectedWeeks * 7);

            var profile = db.FitnessProfiles.FirstOrDefault(f => f.MemberId == memberId);
            bool isNew = profile == null;

            if (isNew)
            {
                profile = new FitnessProfile { MemberId = memberId, CreatedAt = DateTime.Now };
                db.FitnessProfiles.Add(profile);
            }

            profile.HeightCm = heightCm;
            profile.WeightKg = weightKg;
            profile.BodyFatPercent = bodyFatPercent;
            profile.Age = age;
            profile.Gender = gender;
            profile.BMI = bmi;
            profile.BMICategory = bmiCategory;
            profile.BMR = bmr;
            profile.TDEE = tdee;
            profile.ActivityLevel = activityLevel;
            profile.GoalType = finalGoal;
            profile.TargetWeightKg = targetWeightKg;
            profile.TargetBodyFatPercent = targetBodyFatPercent;
            profile.ProjectedWeeks = projectedWeeks;
            profile.ProjectedGoalDate = projectedDate;
            profile.IsOnTrack = true;
            profile.LastUpdated = DateTime.Now;

            db.SaveChanges();

            if (isNew)
            {
                db.FitnessProgressLogs.Add(new FitnessProgressLog
                {
                    MemberId = memberId,
                    WeightKg = weightKg,
                    BodyFatPercent = bodyFatPercent,
                    WorkoutsCompleted = 0,
                    BMI = bmi,
                    WeightChangeSinceStart = 0,
                    BodyFatChangeSinceStart = 0,
                    WeekNumber = 1,
                    Notes = "Baseline measurement",
                    LoggedAt = DateTime.Now
                });
                db.SaveChanges();
            }

            return Json(new
            {
                success = true,
                isNew,
                bmi = bmi.ToString("F1"),
                bmiCategory,
                bmr = bmr.ToString("F0"),
                tdee = tdee.ToString("F0"),
                recommendedGoal,
                finalGoal,
                projectedWeeks,
                projectedGoalDate = projectedDate.ToString("dd MMM yyyy"),
                message = isNew
                    ? $"Fitness profile created! Goal: {finalGoal}. Projected timeline: {projectedWeeks} weeks."
                    : $"Fitness profile updated! Goal: {finalGoal}. New timeline: {projectedWeeks} weeks."
            });
        }

        public ActionResult GetFitnessProfile()
        {
            if (!IsLoggedIn())
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            int memberId = GetLoggedInMemberId().Value;
            var profile = db.FitnessProfiles.FirstOrDefault(f => f.MemberId == memberId);

            if (profile == null)
                return Json(new { success = true, hasProfile = false }, JsonRequestBehavior.AllowGet);

            var logs = db.FitnessProgressLogs
                .Where(l => l.MemberId == memberId)
                .OrderBy(l => l.WeekNumber)
                .ToList();

            var chartData = logs.Select(l => new
            {
                week = l.WeekNumber,
                weight = l.WeightKg,
                bodyFat = l.BodyFatPercent,
                bmi = l.BMI,
                workouts = l.WorkoutsCompleted,
                weightChange = l.WeightChangeSinceStart,
                bodyFatChange = l.BodyFatChangeSinceStart,
                isUnderperforming = l.IsUnderperforming,
                loggedAt = l.LoggedAt.ToString("dd MMM")
            }).ToList();

            decimal totalWeightChange = logs.Count > 1 ? logs.Last().WeightKg - logs.First().WeightKg : 0;
            decimal totalBodyFatChange = logs.Count > 1 ? logs.Last().BodyFatPercent - logs.First().BodyFatPercent : 0;
            int weeksCompleted = logs.Count;
            decimal progressPct = profile.ProjectedWeeks > 0
                ? Math.Min(100, Math.Round((weeksCompleted / (decimal)profile.ProjectedWeeks) * 100, 0))
                : 0;

            return Json(new
            {
                success = true,
                hasProfile = true,
                heightCm = profile.HeightCm,
                weightKg = profile.WeightKg,
                bodyFatPercent = profile.BodyFatPercent,
                bmi = profile.BMI.ToString("F1"),
                bmiCategory = profile.BMICategory,
                bmr = profile.BMR.ToString("F0"),
                tdee = profile.TDEE.ToString("F0"),
                activityLevel = profile.ActivityLevel,
                goalType = profile.GoalType,
                targetWeightKg = profile.TargetWeightKg,
                targetBodyFatPercent = profile.TargetBodyFatPercent,
                projectedWeeks = profile.ProjectedWeeks,
                projectedGoalDate = profile.ProjectedGoalDate.HasValue
                    ? profile.ProjectedGoalDate.Value.ToString("dd MMM yyyy") : "—",
                isOnTrack = profile.IsOnTrack,
                createdAt = profile.CreatedAt.ToString("dd MMM yyyy"),
                weeksCompleted,
                progressPct,
                totalWeightChange = totalWeightChange.ToString("F1"),
                totalBodyFatChange = totalBodyFatChange.ToString("F1"),
                chartData,
                totalLogs = logs.Count
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult LogWeeklyProgress(
            decimal weightKg, decimal bodyFatPercent, int workoutsCompleted, string notes)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            int memberId = GetLoggedInMemberId().Value;
            var profile = db.FitnessProfiles.FirstOrDefault(f => f.MemberId == memberId);
            if (profile == null)
                return Json(new { success = false, message = "Please set up your fitness profile first." });

            if (weightKg < 30 || weightKg > 300) return Json(new { success = false, message = "Weight must be between 30 kg and 300 kg." });
            if (bodyFatPercent < 3 || bodyFatPercent > 60) return Json(new { success = false, message = "Body fat % must be between 3% and 60%." });
            if (workoutsCompleted < 0 || workoutsCompleted > 21) return Json(new { success = false, message = "Workouts completed must be between 0 and 21." });

            var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            bool alreadyLoggedThisWeek = db.FitnessProgressLogs
                .Any(l => l.MemberId == memberId && l.LoggedAt >= startOfWeek);

            if (alreadyLoggedThisWeek)
                return Json(new { success = false, message = "You have already logged progress this week. Come back next week!" });

            decimal bmi = CalculateBMI(weightKg, profile.HeightCm);
            decimal weightChange = Math.Round(weightKg - profile.WeightKg, 2);
            decimal fatChange = Math.Round(bodyFatPercent - profile.BodyFatPercent, 2);
            int weekNumber = db.FitnessProgressLogs.Count(l => l.MemberId == memberId) + 1;

            bool isUnderperforming = false;
            if (weekNumber >= 3)
            {
                var lastTwoLogs = db.FitnessProgressLogs
                    .Where(l => l.MemberId == memberId)
                    .OrderByDescending(l => l.WeekNumber)
                    .Take(2).ToList();

                if (profile.GoalType == "Weight Loss" && lastTwoLogs.Count == 2)
                {
                    bool gainedLastWeek = lastTwoLogs[0].WeightKg >= lastTwoLogs[1].WeightKg;
                    bool gainingThisWeek = weightKg >= lastTwoLogs[0].WeightKg;
                    if (gainedLastWeek && gainingThisWeek) isUnderperforming = true;
                }
                else if (profile.GoalType == "Muscle Gain" && weightKg < profile.WeightKg - 1m)
                    isUnderperforming = true;
                else if (profile.GoalType == "Endurance" && workoutsCompleted < 3)
                    isUnderperforming = true;
            }

            db.FitnessProgressLogs.Add(new FitnessProgressLog
            {
                MemberId = memberId,
                WeightKg = weightKg,
                BodyFatPercent = bodyFatPercent,
                WorkoutsCompleted = workoutsCompleted,
                Notes = (notes ?? "").Trim(),
                BMI = bmi,
                WeightChangeSinceStart = weightChange,
                BodyFatChangeSinceStart = fatChange,
                IsUnderperforming = isUnderperforming,
                WeekNumber = weekNumber,
                LoggedAt = DateTime.Now
            });

            profile.IsOnTrack = !isUnderperforming;
            profile.LastUpdated = DateTime.Now;
            db.SaveChanges();

            if (isUnderperforming)
            {
                bool alertedRecently = profile.LastAlertSentAt.HasValue &&
                    (DateTime.Now - profile.LastAlertSentAt.Value).TotalDays < 6;

                if (!alertedRecently)
                {
                    try
                    {
                        var member = db.Members.Find(memberId);
                        EmailService.SendFitnessAlert(
                            member.Email, member.FirstName,
                            profile.GoalType, weekNumber,
                            weightKg.ToString("F1"), profile.TargetWeightKg.ToString("F1"));
                        profile.AlertsSentCount++;
                        profile.LastAlertSentAt = DateTime.Now;
                        db.SaveChanges();
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[FITNESS ALERT EMAIL] {ex.Message}"); }
                }
            }

            return Json(new
            {
                success = true,
                weekNumber,
                bmi = bmi.ToString("F1"),
                weightChange = weightChange.ToString("F1"),
                fatChange = fatChange.ToString("F1"),
                isUnderperforming,
                message = isUnderperforming
                    ? $"⚠️ Week {weekNumber} logged. You appear to be falling behind your {profile.GoalType} goal — stay consistent!"
                    : $"✅ Week {weekNumber} progress logged! Keep it up!"
            });
        }

        public ActionResult GetRecommendedGoal(decimal bmi, decimal bodyFat, string gender)
        {
            if (!IsLoggedIn())
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            string recommended = RecommendGoal(bmi, bodyFat, gender ?? "Male");
            string bmiCategory = GetBMICategory(bmi);

            string tip;
            switch (recommended)
            {
                case "Weight Loss": tip = "Your BMI or body fat suggests weight loss would most benefit your health. Safe target: 0.5 kg/week."; break;
                case "Muscle Gain": tip = "Your measurements suggest you'd benefit from building lean muscle. Realistic gain: 0.25 kg lean muscle/week."; break;
                case "Endurance": tip = "Your body composition is good — focus on building cardiovascular endurance over a 12-week block."; break;
                default: tip = "Your measurements are in a healthy range. A maintenance programme will keep you on track."; break;
            }

            return Json(new { success = true, recommended, bmiCategory, tip }, JsonRequestBehavior.AllowGet);
        }

        // ── PAGES ────────────────────────────────────────────────────

        public ActionResult Wallet()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");
            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.Find(memberId);
            if (member == null) return RedirectToAction("Login", "Forms");
            member.Payments = db.Payments.Where(p => p.MemberId == memberId).OrderByDescending(p => p.PaymentDate).ToList();
            ViewBag.NextBillingDate = CalculateNextBillingDate(member.RegisteredAt).ToString("dd MMM yyyy");
            return View(member);
        }

        public ActionResult FitnessGoals() => View();

        public ActionResult Membership()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");
            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.Find(memberId);
            if (member == null) return RedirectToAction("Login", "Forms");
            member.Payments = db.Payments.Where(p => p.MemberId == memberId).OrderByDescending(p => p.PaymentDate).ToList();
            DateTime nextBilling = CalculateNextBillingDate(member.RegisteredAt);
            ViewBag.NextBillingDate = nextBilling.ToString("dd MMM yyyy");
            ViewBag.DaysRemaining = Math.Max(0, (int)(nextBilling - DateTime.Today).TotalDays);
            return View(member);
        }

        public ActionResult Freeze()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");
            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.Find(memberId);
            if (member == null) return RedirectToAction("Login", "Forms");
            DateTime nextBilling = CalculateNextBillingDate(member.RegisteredAt);
            ViewBag.NextBillingDate = nextBilling.ToString("dd MMM yyyy");
            ViewBag.DaysRemaining = Math.Max(0, (int)(nextBilling - DateTime.Today).TotalDays);
            return View(member);
        }

        public ActionResult Payments()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");
            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.Find(memberId);
            if (member == null) return RedirectToAction("Login", "Forms");
            member.Payments = db.Payments.Where(p => p.MemberId == memberId).OrderByDescending(p => p.PaymentDate).ToList();
            DateTime nextBilling = CalculateNextBillingDate(member.RegisteredAt);
            ViewBag.NextBillingDate = nextBilling.ToString("dd MMM yyyy");
            ViewBag.DaysRemaining = Math.Max(0, (int)(nextBilling - DateTime.Today).TotalDays);
            return View(member);
        }

        public ActionResult Profile()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");
            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.Find(memberId);
            if (member == null) return RedirectToAction("Login", "Forms");
            DateTime nextBilling = CalculateNextBillingDate(member.RegisteredAt);
            ViewBag.NextBillingDate = nextBilling.ToString("dd MMM yyyy");
            ViewBag.DaysRemaining = Math.Max(0, (int)(nextBilling - DateTime.Today).TotalDays);
            return View(member);
        }

        public ActionResult QrCode()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");
            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.Find(memberId);
            if (member == null) return RedirectToAction("Login", "Forms");
            DateTime nextBilling = CalculateNextBillingDate(member.RegisteredAt);
            ViewBag.NextBillingDate = nextBilling.ToString("dd MMM yyyy");
            ViewBag.DaysRemaining = Math.Max(0, (int)(nextBilling - DateTime.Today).TotalDays);
            return View(member);
        }

        // ============================================================
        // MEMBER TRAINER ACTIONS
        // ============================================================

        private static readonly Dictionary<string, string[]> GoalSpecMap =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Weight Loss", new[] { "Weight Loss & Cardio", "CrossFit & HIIT", "General Fitness" } },
                { "Muscle Gain", new[] { "Muscle Building & Strength", "CrossFit & HIIT", "General Fitness" } },
                { "Endurance",   new[] { "CrossFit & HIIT", "Sports Performance", "General Fitness" } },
                { "Maintenance", new[] { "General Fitness", "Yoga & Flexibility", "Sports Performance" } },
            };

        private static int ExperienceRank(string level)
        {
            if (level == null) return 0;
            if (level.StartsWith("Elite")) return 4;
            if (level.StartsWith("Senior")) return 3;
            if (level.StartsWith("Intermediate")) return 2;
            return 1;
        }

        public ActionResult TrainerHub()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");

            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.Find(memberId);
            if (member == null) return RedirectToAction("Login", "Forms");

            var assignment = db.MemberTrainers
                .Where(mt => mt.MemberId == memberId)
                .Include(mt => mt.Trainer)
                .Include(mt => mt.Trainer.Availabilities)
                .OrderByDescending(mt => mt.AssignedDate)
                .FirstOrDefault();

            var fitnessProfile = db.FitnessProfiles.FirstOrDefault(f => f.MemberId == memberId);

            List<TrainingSession> upcomingSessions = new List<TrainingSession>();
            List<WorkoutProgram> programs = new List<WorkoutProgram>();

            if (assignment != null)
            {
                upcomingSessions = db.TrainingSessions
                    .Where(s => s.MemberId == memberId && s.Status == "Scheduled" && s.SessionDate >= DateTime.Today)
                    .Include(s => s.Trainer)
                    .OrderBy(s => s.SessionDate)
                    .Take(5).ToList();

                programs = db.WorkoutPrograms
                    .Where(w => w.MemberId == memberId && w.TrainerId == assignment.TrainerId)
                    .Include(w => w.ProgramExercises)
                    .ToList();
            }

            var vm = new MemberTrainerHubViewModel
            {
                Member = member,
                Assignment = assignment,
                FitnessProfile = fitnessProfile,
                UpcomingSessions = upcomingSessions,
                WorkoutPrograms = programs
            };

            ViewBag.ActivePage = "trainer";
            ViewBag.PageTitle = "My Trainer";
            return View("~/Views/Dashboard/TrainerHub.cshtml", vm);
        }

        public ActionResult FindTrainer()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");

            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.Find(memberId);
            if (member == null) return RedirectToAction("Login", "Forms");

            bool alreadyAssigned = db.MemberTrainers.Any(mt => mt.MemberId == memberId);
            if (alreadyAssigned) return RedirectToAction("TrainerHub");

            var fitnessProfile = db.FitnessProfiles.FirstOrDefault(f => f.MemberId == memberId);
            string memberGoal = fitnessProfile?.GoalType ?? "";

            var allTrainers = db.Trainers
                .Where(t => t.IsActive)
                .Include(t => t.MemberTrainers)
                .Include(t => t.Availabilities)
                .ToList();

            var availableTrainers = allTrainers.Where(t => t.MemberTrainers.Count < t.MaxCapacity).ToList();

            string[] preferredSpecs = new string[0];
            if (!string.IsNullOrEmpty(memberGoal) && GoalSpecMap.ContainsKey(memberGoal))
                preferredSpecs = GoalSpecMap[memberGoal];

            var scored = availableTrainers
                .Select(t => new TrainerScoreItem
                {
                    Trainer = t,
                    IsMatch = preferredSpecs.Any(s => string.Equals(t.Specialization, s, StringComparison.OrdinalIgnoreCase)),
                    ExperienceRank = ExperienceRank(t.ExperienceLevel),
                    CurrentLoad = t.MemberTrainers.Count
                })
                .OrderByDescending(x => x.IsMatch)
                .ThenByDescending(x => x.ExperienceRank)
                .ThenBy(x => x.CurrentLoad)
                .ToList();

            ViewBag.ActivePage = "trainer";
            ViewBag.PageTitle = "Find a Trainer";
            ViewBag.MemberGoal = memberGoal;
            ViewBag.HasProfile = fitnessProfile != null;
            ViewBag.MemberName = member.FullName;

            return View("~/Views/Dashboard/FindTrainer.cshtml", scored);
        }

        public ActionResult TrainerProfile(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");

            int memberId = GetLoggedInMemberId().Value;

            var trainer = db.Trainers
                .Where(t => t.TrainerId == id && t.IsActive)
                .Include(t => t.Availabilities)
                .Include(t => t.MemberTrainers)
                .FirstOrDefault();

            if (trainer == null) return HttpNotFound();

            var fitnessProfile = db.FitnessProfiles.FirstOrDefault(f => f.MemberId == memberId);
            string memberGoal = fitnessProfile?.GoalType ?? "";
            bool isMatch = !string.IsNullOrEmpty(memberGoal) && GoalSpecMap.ContainsKey(memberGoal)
                && GoalSpecMap[memberGoal].Any(s => string.Equals(trainer.Specialization, s, StringComparison.OrdinalIgnoreCase));

            bool alreadyAssigned = db.MemberTrainers.Any(mt => mt.MemberId == memberId);
            bool atCapacity = trainer.MemberTrainers.Count >= trainer.MaxCapacity;

            ViewBag.ActivePage = "trainer";
            ViewBag.PageTitle = trainer.FullName;
            ViewBag.IsMatch = isMatch;
            ViewBag.MemberGoal = memberGoal;
            ViewBag.AlreadyAssigned = alreadyAssigned;
            ViewBag.AtCapacity = atCapacity;
            ViewBag.CurrentLoad = trainer.MemberTrainers.Count;

            return View("~/Views/Dashboard/TrainerProfile.cshtml", trainer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RequestTrainer(int trainerId)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");

            int memberId = GetLoggedInMemberId().Value;

            if (db.MemberTrainers.Any(mt => mt.MemberId == memberId))
            {
                TempData["Error"] = "You are already assigned to a trainer.";
                return RedirectToAction("TrainerHub");
            }

            var trainer = db.Trainers.Find(trainerId);
            if (trainer == null || !trainer.IsActive)
            {
                TempData["Error"] = "Trainer not found or no longer available.";
                return RedirectToAction("FindTrainer");
            }

            int currentLoad = db.MemberTrainers.Count(mt => mt.TrainerId == trainerId);
            if (currentLoad >= trainer.MaxCapacity)
            {
                TempData["Error"] = $"{trainer.FullName} has reached maximum capacity. Please choose another trainer.";
                return RedirectToAction("FindTrainer");
            }

            db.MemberTrainers.Add(new MemberTrainer
            {
                MemberId = memberId,
                TrainerId = trainerId,
                AssignedDate = DateTime.Today
            });
            db.SaveChanges();
            try
            {
                new NotificationService(db).Notify(
                    "Member", memberId,
                    "Trainer Assigned",
                    $"You're now training with {trainer.FullName}. Book your first session anytime.",
                    "Success",
                    "/Dashboard/TrainerHub");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }

            TempData["Success"] = $"You have been assigned to {trainer.FullName}! You can now book your first session.";
            return RedirectToAction("TrainerHub");
        }

        // ── GET: BookSession ─────────────────────────────────────────

        public ActionResult BookSession()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");

            int memberId = GetLoggedInMemberId().Value;
            var assignment = db.MemberTrainers
                .Where(mt => mt.MemberId == memberId)
                .Include(mt => mt.Trainer)
                .FirstOrDefault();

            if (assignment == null)
            {
                TempData["Error"] = "You need to be assigned to a trainer before booking a session.";
                return RedirectToAction("TrainerHub");
            }

            var availability = db.TrainerAvailabilities
                .Where(a => a.TrainerId == assignment.TrainerId && a.IsAvailable)
                .ToList();

            ViewBag.Trainer = assignment.Trainer;
            ViewBag.Availability = availability;
            ViewBag.SessionTypes = new SelectList(new[] { "Strength", "Cardio", "HIIT", "Flexibility", "Mixed" });
            ViewBag.Durations = new SelectList(new[] { "30 min", "45 min", "60 min", "90 min" });
            ViewBag.ActivePage = "trainer";
            ViewBag.PageTitle = "Book a Session";

            return View("~/Views/Dashboard/BookSession.cshtml", new TrainingSession
            {
                MemberId = memberId,
                TrainerId = assignment.TrainerId,
                SessionDate = DateTime.Today.AddDays(1),
                Status = "Scheduled"
            });
        }

        // ── POST: BookSession ────────────────────────────────────────
        // FIX: validates chosen date + time slot against TrainerAvailability
        // before accepting the booking.

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BookSession(TrainingSession model)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");

            int memberId = GetLoggedInMemberId().Value;

            // Verify the trainer is still this member's trainer
            bool validAssignment = db.MemberTrainers
                .Any(mt => mt.MemberId == memberId && mt.TrainerId == model.TrainerId);

            if (!validAssignment)
            {
                TempData["Error"] = "Invalid assignment. Please contact support.";
                return RedirectToAction("TrainerHub");
            }

            model.MemberId = memberId;
            model.Status = "Scheduled";

            // ── Basic date guard ─────────────────────────────────────
            if (model.SessionDate < DateTime.Today)
                ModelState.AddModelError("SessionDate", "Session date cannot be in the past.");

            // ── Availability guard (server-side) ─────────────────────
            // Check that the chosen day has availability rows for this trainer
            string dayName = model.SessionDate.DayOfWeek.ToString(); // "Monday", "Tuesday", …

            var availWindows = db.TrainerAvailabilities
                .Where(a => a.TrainerId == model.TrainerId &&
                            a.IsAvailable == true &&
                            a.DayOfWeek == dayName)
                .ToList();

            if (!availWindows.Any())
            {
                ModelState.AddModelError("SessionDate",
                    $"The trainer is not available on {dayName}. Please choose a different date.");
            }
            else if (!string.IsNullOrWhiteSpace(model.TimeSlot))
            {
                // Check the chosen time slot falls inside at least one availability window
                TimeSpan slotTs;
                bool parsed = TimeSpan.TryParse(model.TimeSlot, out slotTs);
                int slotMins = parsed ? (int)slotTs.TotalMinutes : -1;

                bool withinWindow = availWindows.Any(w =>
                {
                    TimeSpan wStart, wEnd;
                    TimeSpan.TryParse(w.StartTime, out wStart);
                    TimeSpan.TryParse(w.EndTime, out wEnd);
                    return slotMins >= (int)wStart.TotalMinutes &&
                           slotMins < (int)wEnd.TotalMinutes;
                });

                if (!withinWindow)
                    ModelState.AddModelError("TimeSlot",
                        $"The selected time ({model.TimeSlot}) is outside the trainer's available hours on {dayName}.");
            }

            // ── Double-booking guard ─────────────────────────────────
            bool clash = db.TrainingSessions.Any(s =>
                s.TrainerId == model.TrainerId &&
                s.SessionDate == model.SessionDate &&
                s.TimeSlot == model.TimeSlot &&
                s.Status != "Cancelled");

            if (clash)
                ModelState.AddModelError("TimeSlot",
                    "That time slot is already booked. Please choose another.");

            // ── Save if valid ────────────────────────────────────────
            if (ModelState.IsValid)
            {
                db.TrainingSessions.Add(model);
                db.SaveChanges();
                try
                {
                    new NotificationService(db).Notify(
                        "Member", memberId,
                        "Session Booked",
                        $"Your session on {model.SessionDate:dd MMM yyyy} at {model.TimeSlot} is confirmed.",
                        "Success",
                        "/Dashboard/TrainerHub");
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }
                TempData["Success"] = $"Session booked for {model.SessionDate:dd MMM yyyy} at {model.TimeSlot}!";
                return RedirectToAction("TrainerHub");
            }

            // Re-populate ViewBag for the re-displayed form
            var assign = db.MemberTrainers
                .Where(mt => mt.MemberId == memberId)
                .Include(mt => mt.Trainer)
                .FirstOrDefault();

            var avail = db.TrainerAvailabilities
                .Where(a => a.TrainerId == model.TrainerId && a.IsAvailable)
                .ToList();

            ViewBag.Trainer = assign?.Trainer;
            ViewBag.Availability = avail;
            ViewBag.SessionTypes = new SelectList(new[] { "Strength", "Cardio", "HIIT", "Flexibility", "Mixed" }, model.SessionType);
            ViewBag.Durations = new SelectList(new[] { "30 min", "45 min", "60 min", "90 min" }, model.Duration);
            ViewBag.ActivePage = "trainer";
            ViewBag.PageTitle = "Book a Session";

            return View("~/Views/Dashboard/BookSession.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelSession(int sessionId)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");

            int memberId = GetLoggedInMemberId().Value;

            var session = db.TrainingSessions
                .FirstOrDefault(s => s.SessionId == sessionId && s.MemberId == memberId);

            if (session == null) { TempData["Error"] = "Session not found."; return RedirectToAction("TrainerHub"); }
            if (session.Status == "Completed") { TempData["Error"] = "Cannot cancel a completed session."; return RedirectToAction("TrainerHub"); }

            session.Status = "Cancelled";
            db.SaveChanges();
            try
            {
                new NotificationService(db).Notify(
                    "Member", memberId,
                    "Session Cancelled",
                    $"Your session on {session.SessionDate:dd MMM yyyy} was cancelled.",
                    "Warning",
                    "/Dashboard/TrainerHub");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }
            TempData["Success"] = $"Session on {session.SessionDate:dd MMM yyyy} has been cancelled.";
            return RedirectToAction("TrainerHub");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UnassignTrainer()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");

            int memberId = GetLoggedInMemberId().Value;
            var assignment = db.MemberTrainers.FirstOrDefault(mt => mt.MemberId == memberId);

            if (assignment != null)
            {
                db.MemberTrainers.Remove(assignment);
                db.SaveChanges();
                TempData["Success"] = "Trainer unassigned. You can find a new trainer anytime.";
            }

            return RedirectToAction("TrainerHub");
        }

        // ── LOG WORKOUT SESSION ──────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogWorkoutSession(
            int workoutProgramId,
            int actualDurationSeconds,
            string exercisesSummary,
            string notes)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            int memberId = GetLoggedInMemberId().Value;

            var program = db.WorkoutPrograms
                .FirstOrDefault(w => w.WorkoutProgramId == workoutProgramId && w.MemberId == memberId);

            if (program == null)
                return Json(new { success = false, message = "Program not found." });

            var assignment = db.MemberTrainers.FirstOrDefault(mt => mt.MemberId == memberId);

            if (assignment == null)
                return Json(new { success = false, message = "No trainer assigned." });

            var log = new SwiftyS26.Models.WorkoutSessionLog
            {
                MemberId = memberId,
                WorkoutProgramId = workoutProgramId,
                TrainerId = assignment.TrainerId,
                Status = "Completed",
                SessionDate = DateTime.Today,
                ActualDurationSeconds = actualDurationSeconds,
                ExercisesSummary = exercisesSummary ?? "",
                Notes = (notes ?? "").Trim(),
                LoggedAt = DateTime.Now
            };

            db.WorkoutSessionLogs.Add(log);
            db.SaveChanges();

            return Json(new
            {
                success = true,
                logId = log.WorkoutSessionLogId,
                message = "Workout session recorded successfully!"
            });
        }

        // ── MARK ABSENT ──────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkAbsent(int workoutProgramId, string notes)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            int memberId = GetLoggedInMemberId().Value;

            var program = db.WorkoutPrograms
                .FirstOrDefault(w => w.WorkoutProgramId == workoutProgramId && w.MemberId == memberId);

            if (program == null)
                return Json(new { success = false, message = "Program not found." });

            var assignment = db.MemberTrainers.FirstOrDefault(mt => mt.MemberId == memberId);

            if (assignment == null)
                return Json(new { success = false, message = "No trainer assigned." });

            bool alreadyLogged = db.WorkoutSessionLogs.Any(l =>
                l.MemberId == memberId &&
                l.WorkoutProgramId == workoutProgramId &&
                l.SessionDate == DateTime.Today);

            if (alreadyLogged)
                return Json(new { success = false, message = "You have already logged this program today." });

            var log = new SwiftyS26.Models.WorkoutSessionLog
            {
                MemberId = memberId,
                WorkoutProgramId = workoutProgramId,
                TrainerId = assignment.TrainerId,
                Status = "Absent",
                SessionDate = DateTime.Today,
                ActualDurationSeconds = 0,
                ExercisesSummary = "",
                Notes = (notes ?? "").Trim(),
                LoggedAt = DateTime.Now
            };

            db.WorkoutSessionLogs.Add(log);
            db.SaveChanges();

            return Json(new
            {
                success = true,
                logId = log.WorkoutSessionLogId,
                message = "Absence recorded. Your trainer has been notified."
            });
        }

        // ── GET SESSION LOGS FOR A PROGRAM ───────────────────────────

        public ActionResult GetProgramSessionLogs(int workoutProgramId)
        {
            if (!IsLoggedIn())
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            int memberId = GetLoggedInMemberId().Value;

            var logs = db.WorkoutSessionLogs
                .Where(l => l.MemberId == memberId && l.WorkoutProgramId == workoutProgramId)
                .OrderByDescending(l => l.SessionDate)
                .Take(10)
                .ToList()
                .Select(l => new
                {
                    date = l.SessionDate.ToString("dd MMM yyyy"),
                    status = l.Status,
                    duration = l.ActualDurationSeconds,
                    exercises = l.ExercisesSummary,
                    notes = l.Notes
                }).ToList();

            return Json(new { success = true, logs }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogWeeklyProgressAlt(decimal weightKg, decimal? bodyFatPercent,
                                                  int workoutsThisWeek, string notes)
        {
            var memberId = GetLoggedInMemberId() ?? 0;

            int weekNum = 1;
            var firstLog = db.WorkoutSessionLogs
                .Where(l => l.MemberId == memberId)
                .OrderBy(l => l.SessionDate)
                .FirstOrDefault();

            if (firstLog != null)
            {
                int days = (int)(DateTime.Today - firstLog.SessionDate).TotalDays;
                weekNum = Math.Max(1, (int)Math.Ceiling(days / 7.0));
            }

            bool alreadyLogged = db.WeeklyProgressLogs
                .Any(l => l.MemberId == memberId && l.WeekNumber == weekNum);

            if (alreadyLogged)
            {
                TempData["Error"] = "You have already logged progress for this week.";
                return RedirectToAction("Progress", "Member");
            }

            var log = new WeeklyProgressLog
            {
                MemberId = memberId,
                WeekNumber = weekNum,
                LoggedDate = DateTime.Now,
                WeightKg = weightKg,
                BodyFatPercent = bodyFatPercent,
                WorkoutsThisWeek = workoutsThisWeek,
                Notes = notes?.Trim()
            };

            db.WeeklyProgressLogs.Add(log);
            db.SaveChanges();

            TempData["Success"] = "Your progress for Week " + weekNum + " has been saved!";
            return RedirectToAction("Progress", "Member");
        }

        // ============================================================
        // EQUIPMENT — member-facing list + report fault
        // ============================================================
        public ActionResult Equipment()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");

            var equipmentList = db.Equipment
                .Where(e => !e.IsDeleted && e.Status != "Retired")
                .OrderBy(e => e.GymArea)
                .ThenBy(e => e.Name)
                .ToList();

            ViewBag.ActivePage = "equipment";
            ViewBag.PageTitle = "Equipment";
            return View(equipmentList);
        }

        // ============================================================
        // REPORT FAULT — POST (updated: category, severity, photo)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReportFault(int equipmentId, string category, string severity,
            string faultDescription, HttpPostedFileBase photoFile)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, message = "Please log in first." });

            if (!FaultCategoryHelper.IsValidCategory(category))
                return Json(new { success = false, message = "Please select a valid fault category." });

            var validSeverities = new[] { "Minor", "Critical" };
            if (string.IsNullOrWhiteSpace(severity) || !validSeverities.Contains(severity))
                return Json(new { success = false, message = "Please select a severity — Minor or Critical." });

            if (string.IsNullOrWhiteSpace(faultDescription) || faultDescription.Trim().Length < 10)
                return Json(new { success = false, message = "Please describe the issue (at least 10 characters)." });

            var equipment = db.Equipment.Find(equipmentId);
            if (equipment == null || equipment.IsDeleted)
                return Json(new { success = false, message = "Equipment not found." });

            int memberId = GetLoggedInMemberId().Value;

            // ── Optional photo upload — same pattern as complaint evidence ──
            string photoPath = null;
            if (photoFile != null && photoFile.ContentLength > 0)
            {
                var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var ext = System.IO.Path.GetExtension(photoFile.FileName).ToLower();

                if (!allowedExt.Contains(ext))
                    return Json(new { success = false, message = "Only JPG, PNG or WEBP images are allowed." });

                if (photoFile.ContentLength > 5 * 1024 * 1024)
                    return Json(new { success = false, message = "Photo must be smaller than 5MB." });

                try
                {
                    string folder = Server.MapPath("~/Uploads/Repairs");
                    if (!System.IO.Directory.Exists(folder))
                        System.IO.Directory.CreateDirectory(folder);

                    string safeName = $"{Guid.NewGuid()}{ext}";
                    photoFile.SaveAs(System.IO.Path.Combine(folder, safeName));
                    photoPath = $"/Uploads/Repairs/{safeName}";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UPLOAD ERROR] {ex.Message}");
                    return Json(new { success = false, message = "Problem uploading the photo. Please try again." });
                }
            }

            // ── Reference number ──
            string refNumber;
            do
            {
                refNumber = "RF-" + DateTime.Now.Year + "-" + new Random().Next(1000, 9999);
            } while (db.Repairs.Any(r => r.ReferenceNumber == refNumber));

            var repair = new Repair
            {
                EquipmentId = equipmentId,
                FaultDescription = faultDescription.Trim(),
                Category = category,
                Severity = severity,
                ReferenceNumber = refNumber,
                PhotoPath = photoPath,
                RepairStatus = "Pending",
                ReportedDate = DateTime.Now,
                ReportedBySource = "Member",
                ReportedByMemberId = memberId
            };
            db.Repairs.Add(repair);

            // ── Critical fault auto-marks equipment Out of Service ──
            if (severity == "Critical")
            {
                equipment.Status = "OutOfService";
                db.Entry(equipment).State = EntityState.Modified;
            }

            db.SaveChanges();

            try
            {
                new NotificationService(db).Notify(
                    "Member", memberId,
                    "Fault Report Received",
                    $"Reference {refNumber} — \"{equipment.Name}\" has been flagged for review.",
                    "Info",
                    "/Dashboard/Equipment");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }

            try
            {
                new NotificationService(db).NotifyAdmin(
                    "New Member Fault Report",
                    $"{(severity == "Critical" ? "CRITICAL: " : "")}\"{equipment.Name}\" flagged by a member — ref {refNumber}." +
                        (severity == "Critical" ? " Equipment marked Out of Service." : ""),
                    severity == "Critical" ? "Alert" : "Warning",
                    "/Equipment/Index");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }
            return Json(new
            {
                success = true,
                referenceNumber = refNumber,
                message = severity == "Critical"
                    ? $"Reported — \"{equipment.Name}\" has been marked Out of Service and flagged as critical. Ref: {refNumber}"
                    : $"Thanks — \"{equipment.Name}\" has been flagged for review. Ref: {refNumber}"
            });
        }

        // ─────────────────────────────────────────────────────────────

        // GET: /Dashboard/SubmitComplaintForm
        public ActionResult SubmitComplaintForm()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");

            ViewBag.Categories = ComplaintHelper.ValidCategories;
            ViewBag.ActivePage = "complaints";
            ViewBag.PageTitle = "Submit a Complaint";
            return View("~/Views/Dashboard/SubmitComplaintForm.cshtml", new ComplaintTicket());
        }

        // POST: /Dashboard/SubmitComplaint
        [HttpPost]
        public ActionResult SubmitComplaint(ComplaintTicket model, HttpPostedFileBase evidenceFile)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, message = "Please log in to submit a complaint." });

            if (!ComplaintHelper.IsValidCategory(model.Category))
                return Json(new { success = false, message = "Please select a valid complaint category." });

            if (string.IsNullOrWhiteSpace(model.Details) || model.Details.Trim().Length < 10)
                return Json(new { success = false, message = "Please provide more detail about your complaint (at least 10 characters)." });

            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.Find(memberId);
            if (member == null)
                return Json(new { success = false, message = "Session expired. Please log in again." });

            if (member.Status != "Active" && member.Status != "Pending Cancellation")
                return Json(new { success = false, message = "Only active members can submit complaints." });

            var complaint = new ComplaintTicket
            {
                MemberId = member.MemberId,
                Category = model.Category.Trim(),
                Details = model.Details.Trim(),
                Status = "Submitted",
                SubmittedAt = DateTime.Now,
                IpAddress = Request.UserHostAddress
            };

            // ── Optional evidence upload ────────────────────────────
            if (evidenceFile != null && evidenceFile.ContentLength > 0)
            {
                if (!ComplaintHelper.IsAllowedEvidenceExtension(evidenceFile.FileName))
                    return Json(new { success = false, message = "Unsupported file type. Allowed: jpg, jpeg, png, pdf, docx." });

                if (evidenceFile.ContentLength > 5 * 1024 * 1024) // 5MB cap
                    return Json(new { success = false, message = "Evidence file must be smaller than 5MB." });

                try
                {
                    string folder = Server.MapPath("~/Uploads/Complaints");
                    if (!System.IO.Directory.Exists(folder))
                        System.IO.Directory.CreateDirectory(folder);

                    string safeName = $"{Guid.NewGuid()}{System.IO.Path.GetExtension(evidenceFile.FileName)}";
                    string fullPath = System.IO.Path.Combine(folder, safeName);
                    evidenceFile.SaveAs(fullPath);

                    complaint.EvidenceFileName = System.IO.Path.GetFileName(evidenceFile.FileName);
                    complaint.EvidenceFilePath = $"/Uploads/Complaints/{safeName}";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UPLOAD ERROR] {ex.Message}");
                    return Json(new { success = false, message = "There was a problem uploading your evidence file. Please try again." });
                }
            }

            // Ensure reference number uniqueness before saving
            string refNumber;
            do
            {
                refNumber = ComplaintHelper.GenerateReferenceNumber();
            } while (db.ComplaintTickets.Any(c => c.ReferenceNumber == refNumber));

            complaint.ReferenceNumber = refNumber;

            try
            {
                db.ComplaintTickets.Add(complaint);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB ERROR] {ex.Message}");
                return Json(new { success = false, message = "Error saving your complaint. Please try again." });
            }
            try
            {
                new NotificationService(db).Notify(
                    "Member", memberId,
                    "Complaint Submitted",
                    $"Reference {complaint.ReferenceNumber} — we've received your complaint and will follow up.",
                    "Info",
                    "/Dashboard/MyComplaints");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }

            // Notify admin of the new complaint
            try
            {
                EmailService.SendComplaintNotificationToAdmin(
                    complaint.ReferenceNumber, member.FullName, member.Email, complaint.Category, complaint.Details);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EMAIL ERROR] {ex.Message}");
            }

            return Json(new
            {
                success = true,
                referenceNumber = complaint.ReferenceNumber,
                message = "Your complaint has been submitted. Please keep your reference number to track its progress."
            });
        }

        // ── STEP 12-14: MEMBER TRACKS A COMPLAINT ───────────────────────

        // GET: /Dashboard/MyComplaints
        public ActionResult MyComplaints()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");

            int memberId = GetLoggedInMemberId().Value;

            var myComplaints = db.ComplaintTickets
                .Where(c => c.MemberId == memberId)
                .OrderByDescending(c => c.SubmittedAt)
                .ToList();

            ViewBag.ActivePage = "complaints";
            ViewBag.PageTitle = "My Complaints";
            return View("~/Views/Dashboard/MyComplaints.cshtml", myComplaints);
        }

        // POST: /Dashboard/TrackComplaint
        [HttpPost]
        public ActionResult TrackComplaint(string referenceNumber)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, message = "Please log in to track a complaint." });

            if (string.IsNullOrWhiteSpace(referenceNumber))
                return Json(new { success = false, message = "Please enter a reference number." });

            int memberId = GetLoggedInMemberId().Value;

            var complaint = db.ComplaintTickets
                .FirstOrDefault(c => c.ReferenceNumber == referenceNumber.Trim());

            // Only let a member view their own complaint
            if (complaint == null || complaint.MemberId != memberId)
                return Json(new { success = false, message = "No complaint found with that reference number." });

            return Json(new
            {
                success = true,
                referenceNumber = complaint.ReferenceNumber,
                category = complaint.Category,
                status = complaint.Status,
                details = complaint.Details,
                staffComments = complaint.StaffComments,
                resolutionDetails = complaint.ResolutionDetails,
                submittedAt = complaint.SubmittedAtDisplay,
                lastUpdatedAt = complaint.LastUpdatedAtDisplay,
                isClosed = complaint.IsClosed
            });
        }
        // ══════════════════════════════════════════════════════════════
        // FEEDBACK FEATURE
        // ══════════════════════════════════════════════════════════════

        // GET: /Dashboard/SubmitFeedbackForm
        public ActionResult SubmitFeedbackForm()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");

            ViewBag.Categories = FeedbackHelper.ValidCategories;
            ViewBag.ActivePage = "feedback";
            ViewBag.PageTitle = "Submit Feedback";
            return View("~/Views/Dashboard/SubmitFeedbackForm.cshtml", new Feedback());
        }

        // POST: /Dashboard/SubmitFeedback
        [HttpPost]
        public ActionResult SubmitFeedback(string category, string comments, int rating)
        {
            if (!IsLoggedIn())
                return Json(new { success = false, message = "Please log in to submit feedback." });

            if (!FeedbackHelper.IsValidCategory(category))
                return Json(new { success = false, message = "Please select a valid feedback category." });

            if (string.IsNullOrWhiteSpace(comments) || comments.Trim().Length < 5)
                return Json(new { success = false, message = "Please enter a few words of feedback (at least 5 characters)." });

            if (!FeedbackHelper.IsValidRating(rating))
                return Json(new { success = false, message = "Please select a rating between 1 and 5 stars." });

            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.Find(memberId);
            if (member == null)
                return Json(new { success = false, message = "Session expired. Please log in again." });

            var feedback = new Feedback
            {
                MemberId = member.MemberId,
                Category = category.Trim(),
                Comments = comments.Trim(),
                Rating = rating,
                SubmittedAt = DateTime.Now,
                IpAddress = Request.UserHostAddress
            };

            try
            {
                db.Feedbacks.Add(feedback);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB ERROR] {ex.Message}");
                return Json(new { success = false, message = "Error saving your feedback. Please try again." });
            }

            return Json(new
            {
                success = true,
                message = "Thank you! Your feedback has been submitted successfully."
            });
        }

        // ══════════════════════════════════════════════════════════════
        // END FEEDBACK FEATURE
        // ══════════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════════
        // REFERRAL FEATURE
        // ══════════════════════════════════════════════════════════════

        // GET: /Dashboard/ReferAFriend
        public ActionResult ReferAFriend()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Forms");

            int memberId = GetLoggedInMemberId().Value;
            var member = db.Members.Find(memberId);
            if (member == null) return RedirectToAction("Login", "Forms");

            // Precondition: active membership required to refer (per UC08 preconditions)
            if (member.Status != "Active")
            {
                ViewBag.NotEligible = true;
                ViewBag.ActivePage = "referral";
                ViewBag.PageTitle = "Refer a Friend";
                return View("~/Views/Dashboard/ReferAFriend.cshtml", member);
            }

            // Step 2: generate the code once, then reuse it for every future share
            if (string.IsNullOrWhiteSpace(member.ReferralCode))
            {
                member.ReferralCode = ReferralHelper.GenerateUniqueReferralCode(db);
                db.Entry(member).State = EntityState.Modified;
                db.SaveChanges();
            }

            string referralLink = Url.Action("Register", "Forms",
                new { refCode = member.ReferralCode }, Request.Url.Scheme);

            var referrals = db.Referrals
                .Where(r => r.ReferrerId == memberId)
                .Include(r => r.ReferredMember)
                .OrderByDescending(r => r.RegisteredAt)
                .ToList();

            ViewBag.ReferralLink = referralLink;
            ViewBag.Referrals = referrals;
            ViewBag.TotalReferrals = referrals.Count;
            ViewBag.TotalPointsEarned = referrals.Sum(r => r.RewardPoints);
            ViewBag.ActivePage = "referral";
            ViewBag.PageTitle = "Refer a Friend";

            return View("~/Views/Dashboard/ReferAFriend.cshtml", member);
        }

        // ══════════════════════════════════════════════════════════════
        // END REFERRAL FEATURE
        // ══════════════════════════════════════════════════════════════

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}