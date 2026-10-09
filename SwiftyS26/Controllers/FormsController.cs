using SwiftyS26.Models;
using System;
using System.Linq;
using System.Web.Mvc;

namespace SwiftyS26.Controllers
{
    public class FormsController : Controller
    {
        private readonly GymHubDB db = new GymHubDB();

        // GET: /Forms/Register
        public ActionResult Register(string plan = "standard")
        {
            var model = new MemberForm
            {
                Plan = plan,
                MonthlyFee = MemberHelper.GetPlanFee(plan),
                Status = "Pending"
            };

            return View(model);
        }

        // POST: /Forms/SubmitPersonalInfo
        [HttpPost]
        public ActionResult SubmitPersonalInfo(MemberForm model)
        {
            // Hash password before any validation
            string rawPassword = model.PasswordHash;
            if (!string.IsNullOrWhiteSpace(rawPassword))
                model.PasswordHash = BCrypt.Net.BCrypt.HashPassword(rawPassword);

            if (!MemberHelper.IsValidSaId(model.IdNumber))
                return Json(new { success = false, message = "Please enter a valid 13-digit SA ID number." });

            // Auto-fill DOB and gender from ID if not provided
            if (model.DateOfBirth == DateTime.MinValue)
            {
                var dobFromId = MemberHelper.ExtractDobFromId(model.IdNumber);
                if (dobFromId.HasValue)
                    model.DateOfBirth = dobFromId.Value;
            }

            if (string.IsNullOrWhiteSpace(model.Gender))
            {
                var genderFromId = MemberHelper.ExtractGenderFromId(model.IdNumber);
                if (genderFromId != null)
                    model.Gender = genderFromId;
            }

            if (MemberHelper.CalculateAge(model.DateOfBirth) < 18)
                return Json(new { success = false, message = "You must be at least 18 years old to register." });

            if (!MemberHelper.IsValidEmail(model.Email))
                return Json(new { success = false, message = "Please enter a valid email address." });

            if (db.Members.Any(m => m.Email == model.Email && m.Status != "Cancelled"))
                return Json(new { success = false, message = "This email address is already registered." });

            if (db.Members.Any(m => m.Phone == model.Phone))
                return Json(new { success = false, message = "This phone number is already registered." });

            if (db.Members.Any(m => m.IdNumber == model.IdNumber))
                return Json(new { success = false, message = "This ID number is already registered." });

            model.MonthlyFee = MemberHelper.GetPlanFee(model.Plan);

            string otp = MemberHelper.GenerateOtp();
            model.OtpCode = otp;
            model.OtpGeneratedAt = DateTime.Now;
            model.IsEmailVerified = false;

            Session["PendingMember"] = model;

            try
            {
                EmailService.SendOtp(model.Email, model.FirstName, otp);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EMAIL ERROR] {ex.Message}");
            }

            return Json(new { success = true });
        }

        // POST: /Forms/VerifyOtp
        [HttpPost]
        public ActionResult VerifyOtp(string enteredOtp)
        {
            var model = Session["PendingMember"] as MemberForm;
            if (model == null)
                return Json(new { success = false, message = "Session expired. Please start again." });

            if (model.IsOtpExpired)
                return Json(new { success = false, message = "Your code has expired. Please request a new one." });

            if (!MemberHelper.ValidateOtp(model.OtpCode, model.OtpGeneratedAt, enteredOtp))
                return Json(new { success = false, message = "Incorrect code. Please try again." });

            model.IsEmailVerified = true;
            Session["PendingMember"] = model;

            return Json(new { success = true });
        }

        // POST: /Forms/ResendOtp
        [HttpPost]
        public ActionResult ResendOtp()
        {
            var model = Session["PendingMember"] as MemberForm;
            if (model == null)
                return Json(new { success = false, message = "Session expired. Please start again." });

            string newOtp = MemberHelper.GenerateOtp();
            model.OtpCode = newOtp;
            model.OtpGeneratedAt = DateTime.Now;
            Session["PendingMember"] = model;

            try
            {
                EmailService.SendOtp(model.Email, model.FirstName, newOtp);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EMAIL ERROR] {ex.Message}");
            }

            return Json(new { success = true, message = "A new code has been sent to your email." });
        }

        // POST: /Forms/AcceptTerms
        [HttpPost]
        public ActionResult AcceptTerms(bool tc1, bool tc2, bool tc3)
        {
            var model = Session["PendingMember"] as MemberForm;
            if (model == null)
                return Json(new { success = false, message = "Session expired. Please start again." });

            if (!model.IsEmailVerified)
                return Json(new { success = false, message = "Please verify your email first." });

            if (!tc1 || !tc2 || !tc3)
                return Json(new { success = false, message = "Please accept all terms to continue." });

            model.AcceptedTerms = tc1;
            model.AcceptedAgeConfirmation = tc2;
            model.AcceptedPrivacyPolicy = tc3;
            model.TermsAcceptedAt = DateTime.Now;
            Session["PendingMember"] = model;

            return Json(new { success = true });
        }

        // POST: /Forms/CompleteRegistration
        [HttpPost]
        public ActionResult CompleteRegistration(string paymentMethod, string plan)
        {
            var model = Session["PendingMember"] as MemberForm;
            if (model == null)
                return Json(new { success = false, message = "Session expired. Please start again." });

            if (!model.IsEmailVerified)
                return Json(new { success = false, message = "Please verify your email first." });

            model.IpAddress = Request.UserHostAddress;
            model.RegisteredAt = DateTime.Now;
            model.MemberNumber = MemberHelper.GenerateMemberNumber();
            model.Status = "Active";
            model.OtpCode = null;
            model.OtpGeneratedAt = null;

            try
            {
                db.Members.Add(model);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB ERROR] {ex.Message}");
                return Json(new { success = false, message = "Error saving registration. Please try again." });
            }

            try
            {
                EmailService.SendWelcome(model.Email, model.FullName, model.MemberNumber, model.Plan, model.MonthlyFee);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EMAIL ERROR] {ex.Message}");
            }

            Session.Remove("PendingMember");

            return Json(new { success = true, memberNumber = model.MemberNumber });
        }

        // GET: /Forms/Login
        public ActionResult Login()
        {
            if (Session["LoggedInMember"] != null)
                return RedirectToAction("Index", "Dashboard");

            return View(new LoginViewModel());
        }

        // POST: /Forms/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            const string genericError = "Invalid email or password.";

            var member = db.Members.FirstOrDefault(m => m.Email.ToLower() == model.Email.ToLower());
            if (member == null)
            {
                ModelState.AddModelError("", genericError);
                return View(model);
            }

            if (member.IsLockedOut)
            {
                int minutesLeft = (int)Math.Ceiling((member.LockoutEnd.Value - DateTime.Now).TotalMinutes);
                ModelState.AddModelError("", $"Account locked. Try again in {minutesLeft} minute(s).");
                return View(model);
            }

            if (member.Status == "Cancelled")
            {
                ModelState.AddModelError("", "This account has been cancelled. Please register a new account to rejoin GymHub.");
                return View(model);
            }

            if (member.Status == "Suspended")
            {
                ModelState.AddModelError("", "Your account is suspended. Please contact support at info@gymhub.co.za.");
                return View(model);
            }

            // "Pending Cancellation" — allow login, member still has access

            if (!BCrypt.Net.BCrypt.Verify(model.Password, member.PasswordHash))
            {
                member.FailedLoginAttempts++;

                if (member.FailedLoginAttempts >= 5)
                {
                    member.LockoutEnd = DateTime.Now.AddMinutes(15);
                    member.FailedLoginAttempts = 0;
                    db.SaveChanges();
                    ModelState.AddModelError("", "Too many failed attempts. Your account has been locked for 15 minutes.");
                    return View(model);
                }

                db.SaveChanges();
                int attemptsLeft = 5 - member.FailedLoginAttempts;
                ModelState.AddModelError("", $"{genericError} {attemptsLeft} attempt(s) remaining before lockout.");
                return View(model);
            }

            // Login successful
            member.FailedLoginAttempts = 0;
            member.LockoutEnd = null;
            member.LastLoginAt = DateTime.Now;
            db.SaveChanges();

            Session["LoggedInMember"] = member.MemberId;
            Session["LoggedInName"] = member.FullName;
            Session["LoggedInPlan"] = member.Plan;

            if (model.RememberMe)
                Session.Timeout = 43200;

            return RedirectToAction("Index", "Dashboard");
        }

        // POST: /Forms/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Index", "Home");
        }

        // GET: /Forms/ForgotPassword
        public ActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        // POST: /Forms/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var member = db.Members.FirstOrDefault(m => m.Email.ToLower() == model.Email.ToLower());

            // Always redirect — never reveal whether the email exists
            if (member != null)
            {
                string otp = MemberHelper.GenerateOtp();
                member.ResetOtpCode = otp;
                member.ResetOtpGeneratedAt = DateTime.Now;
                db.SaveChanges();

                try
                {
                    EmailService.SendPasswordResetOtp(member.Email, member.FirstName, otp);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EMAIL ERROR] {ex.Message}");
                }
            }

            return RedirectToAction("VerifyResetCode", new { email = model.Email });
        }

        // GET: /Forms/VerifyResetCode
        public ActionResult VerifyResetCode(string email)
        {
            return View(new ResetPasswordViewModel { Email = email });
        }

        // POST: /Forms/VerifyResetCode
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyResetCode(ResetPasswordViewModel model)
        {
            var member = db.Members.FirstOrDefault(m => m.Email.ToLower() == model.Email.ToLower());

            if (member == null || member.ResetOtpCode == null)
            {
                ModelState.AddModelError("OtpCode", "Invalid or expired reset request.");
                return View(model);
            }

            if (member.IsResetOtpExpired)
            {
                ModelState.AddModelError("OtpCode", "This code has expired. Please request a new one.");
                return View(model);
            }

            if (member.ResetOtpCode != model.OtpCode?.Trim())
            {
                ModelState.AddModelError("OtpCode", "Incorrect code. Please try again.");
                return View(model);
            }

            return RedirectToAction("ResetPassword", new { email = model.Email, otp = model.OtpCode });
        }

        // GET: /Forms/ResetPassword
        public ActionResult ResetPassword(string email, string otp)
        {
            return View(new ResetPasswordViewModel
            {
                Email = email,
                OtpCode = otp
            });
        }

        // POST: /Forms/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var member = db.Members.FirstOrDefault(m => m.Email.ToLower() == model.Email.ToLower());

            if (member == null || member.ResetOtpCode == null)
            {
                ModelState.AddModelError("", "Invalid or expired reset request. Please try again.");
                return View(model);
            }

            if (member.IsResetOtpExpired)
            {
                ModelState.AddModelError("OtpCode", "This code has expired. Please request a new one.");
                return View(model);
            }

            if (member.ResetOtpCode != model.OtpCode.Trim())
            {
                ModelState.AddModelError("OtpCode", "Incorrect code. Please try again.");
                return View(model);
            }

            member.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            member.ResetOtpCode = null;
            member.ResetOtpGeneratedAt = null;
            member.FailedLoginAttempts = 0;
            member.LockoutEnd = null;
            db.SaveChanges();

            TempData["ResetSuccess"] = "Your password has been reset. Please sign in.";
            return RedirectToAction("Login");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}