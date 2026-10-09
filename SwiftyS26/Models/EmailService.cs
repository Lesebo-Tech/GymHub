using System;
using MimeKit;
using MimeKit.Utils;
using MailKit.Net.Smtp;
using MailKit.Security;
using QRCoder;
using System.Web.Configuration;

namespace SwiftyS26.Models
{
    public static class EmailService
    {
        private static string SenderEmail =>
            WebConfigurationManager.AppSettings[
                "GymHubEmail:SenderEmail"];

        private static string SenderName =>
            WebConfigurationManager.AppSettings[
                "GymHubEmail:SenderName"] ?? "GymHub";

        private static string AppPassword =>
            WebConfigurationManager.AppSettings[
                "GymHubEmail:AppPassword"];

        private static string SmtpHost =>
            WebConfigurationManager.AppSettings[
                "GymHubEmail:SmtpHost"] ?? "smtp.gmail.com";

        private static int SmtpPort
        {
            get
            {
                int port;

                return int.TryParse(
                    WebConfigurationManager.AppSettings[
                        "GymHubEmail:SmtpPort"],
                    out port)
                        ? port
                        : 587;
            }
        }

        // ============================================================
        // OTP — Email Verification
        // ============================================================
        public static void SendOtp(string toEmail, string toName, string otp)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(SenderName, SenderEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = "GymHub — Your Email Verification Code";
            message.Body = new TextPart("html")
            {
                Text = $@"<!DOCTYPE html><html><head><style>
body{{font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0}}
.container{{max-width:600px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden}}
.header{{background:#141414;padding:32px;text-align:center}}
.header h1{{color:#fff;font-size:28px;margin:0;letter-spacing:3px}}
.header h1 span{{color:#3B82F6}}
.body{{padding:40px 32px}}.body p{{color:#555;font-size:15px;line-height:1.7}}
.otp-box{{background:#141414;border-radius:12px;padding:24px;text-align:center;margin:28px 0}}
.otp-code{{font-size:42px;font-weight:bold;letter-spacing:12px;color:#3B82F6}}
.otp-expiry{{color:#999;font-size:13px;margin-top:8px}}
.footer{{background:#f9f9f9;padding:20px 32px;text-align:center}}
.footer p{{color:#aaa;font-size:12px;margin:0}}
</style></head><body><div class='container'><div class='header'><h1>GYM<span>HUB</span></h1></div>
<div class='body'><p>Hi <strong>{toName}</strong>,</p>
<p>Thank you for registering with GymHub! Please enter the verification code below:</p>
<div class='otp-box'><div class='otp-code'>{otp}</div><div class='otp-expiry'>This code expires in 10 minutes</div></div>
<p>If you did not request this code, please ignore this email.</p></div>
<div class='footer'><p>© {DateTime.Now.Year} GymHub. All rights reserved.</p></div></div></body></html>"
            };
            Send(message);
        }

        // ============================================================
        // WELCOME — with membership QR code
        // ============================================================
        public static void SendWelcome(string toEmail, string toName, string memberNumber, string plan, decimal monthlyFee)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(SenderName, SenderEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = "Welcome to GymHub — Registration Confirmed!";

            byte[] qrBytes = MemberHelper.GenerateQrCodeBytes(memberNumber, toName, plan, toEmail);
            var builder = new BodyBuilder();
            var qrImage = builder.LinkedResources.Add("gymhub-qr.png", qrBytes, ContentType.Parse("image/png"));
            qrImage.ContentId = MimeUtils.GenerateMessageId();
            qrImage.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
            string cid = qrImage.ContentId;

            builder.HtmlBody = $@"<!DOCTYPE html><html><head><style>
body{{font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0}}
.container{{max-width:600px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden}}
.header{{background:#141414;padding:32px;text-align:center}}
.header h1{{color:#fff;font-size:28px;margin:0;letter-spacing:3px}}
.header h1 span{{color:#3B82F6}}
.body{{padding:40px 32px}}.body p{{color:#555;font-size:15px;line-height:1.7}}
.member-card{{background:#141414;border-radius:12px;padding:28px;margin:28px 0}}
.member-card-row{{display:flex;justify-content:space-between;padding:10px 0;border-bottom:1px solid rgba(255,255,255,0.06)}}
.member-card-row:last-child{{border-bottom:none}}
.card-label{{color:#999;font-size:12px;text-transform:uppercase;letter-spacing:1px}}
.card-value{{color:#fff;font-size:15px;font-weight:bold}}
.member-id{{color:#3B82F6;font-size:22px;letter-spacing:3px}}
.qr-section{{text-align:center;margin:28px 0;padding:24px;background:#f9f9f9;border-radius:12px;border:2px dashed #ddd}}
.qr-section h3{{color:#333;font-size:16px;margin-bottom:16px;margin-top:0}}
.qr-section img{{width:180px;height:180px;border:4px solid #141414;border-radius:8px;display:block;margin:0 auto}}
.footer{{background:#f9f9f9;padding:20px 32px;text-align:center}}
.footer p{{color:#aaa;font-size:12px;margin:0}}
</style></head><body><div class='container'><div class='header'><h1>GYM<span>HUB</span></h1></div>
<div class='body'><p>Hi <strong>{toName}</strong>,</p>
<p>Welcome to GymHub! Your membership has been successfully activated.</p>
<div class='member-card'>
<div class='member-card-row'><span class='card-label'>Member Name</span><span class='card-value'>{toName}</span></div>
<div class='member-card-row'><span class='card-label'>Member ID</span><span class='card-value member-id'>{memberNumber}</span></div>
<div class='member-card-row'><span class='card-label'>Plan</span><span class='card-value'>{plan.ToUpper()}</span></div>
<div class='member-card-row'><span class='card-label'>Monthly Fee</span><span class='card-value'>R{monthlyFee:F2}/month</span></div>
<div class='member-card-row'><span class='card-label'>Status</span><span class='card-value' style='color:#22C55E;'>Active</span></div>
</div>
<div class='qr-section'><h3>🏷️ Your GymHub Access QR Code</h3>
<img src='cid:{cid}' alt='QR Code' /><p>Show this at the front desk to access the gym.</p></div>
</div>
<div class='footer'><p>© {DateTime.Now.Year} GymHub. All rights reserved.</p></div></div></body></html>";

            message.Body = builder.ToMessageBody();
            Send(message);
        }

        // ============================================================
        // PASSWORD RESET OTP
        // ============================================================
        public static void SendPasswordResetOtp(string toEmail, string toName, string otp)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(SenderName, SenderEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = "GymHub — Password Reset Code";
            message.Body = new TextPart("html")
            {
                Text = $@"<!DOCTYPE html><html><head><style>
body{{font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0}}
.container{{max-width:600px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden}}
.header{{background:#141414;padding:32px;text-align:center}}
.header h1{{color:#fff;font-size:28px;margin:0;letter-spacing:3px}}
.header h1 span{{color:#3B82F6}}
.body{{padding:40px 32px}}.body p{{color:#555;font-size:15px;line-height:1.7}}
.otp-box{{background:#141414;border-radius:12px;padding:24px;text-align:center;margin:28px 0}}
.otp-code{{font-size:42px;font-weight:bold;letter-spacing:12px;color:#EF4444}}
.otp-expiry{{color:#999;font-size:13px;margin-top:8px}}
.footer{{background:#f9f9f9;padding:20px 32px;text-align:center}}
.footer p{{color:#aaa;font-size:12px;margin:0}}
</style></head><body><div class='container'><div class='header'><h1>GYM<span>HUB</span></h1></div>
<div class='body'><p>Hi <strong>{toName}</strong>,</p>
<p>Use the code below to reset your password:</p>
<div class='otp-box'><div class='otp-code'>{otp}</div><div class='otp-expiry'>Expires in 10 minutes</div></div>
<p>If you did not request this, ignore this email.</p></div>
<div class='footer'><p>© {DateTime.Now.Year} GymHub. All rights reserved.</p></div></div></body></html>"
            };
            Send(message);
        }

        // ============================================================
        // PLAN CHANGE — Upgrade / Downgrade
        // ============================================================
        public static void SendPlanChange(string toEmail, string firstName, string oldPlan, string newPlan,
            decimal oldFee, decimal newFee, string changeType, string effectiveDate)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(SenderName, SenderEmail));
            message.To.Add(new MailboxAddress(firstName, toEmail));
            message.Subject = $"GymHub — Membership {changeType} Confirmed";
            string accentColor = changeType == "Upgrade" ? "#2563EB" : "#D97706";
            string arrow = changeType == "Upgrade" ? "⬆️" : "⬇️";
            message.Body = new TextPart("html")
            {
                Text = $@"<!DOCTYPE html><html><head><style>
body{{font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0}}
.container{{max-width:600px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden}}
.header{{background:#141414;padding:32px;text-align:center}}
.header h1{{color:#fff;font-size:28px;margin:0;letter-spacing:3px}}
.header h1 span{{color:#3B82F6}}
.body{{padding:40px 32px}}.body p{{color:#555;font-size:15px;line-height:1.7}}
.change-box{{background:#f8faff;border:1px solid #e2e8f0;border-radius:12px;padding:24px;margin:24px 0;text-align:center}}
.old-plan{{color:#94a3b8;text-decoration:line-through;font-size:18px;font-weight:700}}
.new-plan{{color:{accentColor};font-size:28px;font-weight:700}}
.footer{{background:#f9f9f9;padding:20px 32px;text-align:center}}
.footer p{{color:#aaa;font-size:12px;margin:0}}
</style></head><body><div class='container'><div class='header'><h1>GYM<span>HUB</span></h1></div>
<div class='body'><p>Hi <strong>{firstName}</strong>,</p>
<p>Your membership {changeType.ToLower()} has been confirmed.</p>
<div class='change-box'>
<div class='old-plan'>{oldPlan.ToUpper()} — R{oldFee:F2}/mo</div>
<div style='font-size:32px;margin:12px 0;'>{arrow}</div>
<div class='new-plan'>{newPlan.ToUpper()} — R{newFee:F2}/mo</div>
</div>
<p>Effective from <strong>{effectiveDate}</strong>.</p></div>
<div class='footer'><p>© {DateTime.Now.Year} GymHub. All rights reserved.</p></div></div></body></html>"
            };
            Send(message);
        }

        // ============================================================
        // CANCELLATION CONFIRMATION — end of billing period
        // ============================================================
        public static void SendCancellationConfirmation(string toEmail, string firstName, string memberNumber, string accessUntil)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(SenderName, SenderEmail));
            message.To.Add(new MailboxAddress(firstName, toEmail));
            message.Subject = "GymHub — Cancellation Confirmation";
            message.Body = new TextPart("html")
            {
                Text = $@"<!DOCTYPE html><html><head><style>
body{{font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0}}
.container{{max-width:600px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden}}
.header{{background:#141414;padding:32px;text-align:center}}
.header h1{{color:#fff;font-size:28px;margin:0;letter-spacing:3px}}
.header h1 span{{color:#3B82F6}}
.body{{padding:40px 32px}}.body p{{color:#555;font-size:15px;line-height:1.7}}
.access-box{{background:#fff8e1;border:1px solid #fde68a;border-radius:12px;padding:20px 24px;margin:24px 0}}
.access-date{{font-size:22px;font-weight:700;color:#D97706;margin-top:6px}}
.footer{{background:#f9f9f9;padding:20px 32px;text-align:center}}
.footer p{{color:#aaa;font-size:12px;margin:0}}
</style></head><body><div class='container'><div class='header'><h1>GYM<span>HUB</span></h1></div>
<div class='body'><p>Hi <strong>{firstName}</strong>,</p>
<p>Your cancellation request has been confirmed.</p>
<div class='access-box'><div style='font-size:13px;font-weight:700;text-transform:uppercase;letter-spacing:1px;color:#92400E;'>Your access continues until</div>
<div class='access-date'>{accessUntil}</div></div>
<p>Member ID: <strong>{memberNumber}</strong> | No further payments will be taken.</p></div>
<div class='footer'><p>© {DateTime.Now.Year} GymHub. All rights reserved.</p></div></div></body></html>"
            };
            Send(message);
        }

        // ============================================================
        // IMMEDIATE CANCELLATION — cooling-off / refund
        // ============================================================
        public static void SendImmediateCancellation(string toEmail, string firstName, string memberNumber, decimal refundAmount, bool isCoolingOff)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(SenderName, SenderEmail));
            message.To.Add(new MailboxAddress(firstName, toEmail));
            message.Subject = "GymHub — Membership Cancelled & Refund Requested";
            message.Body = new TextPart("html")
            {
                Text = $@"<!DOCTYPE html><html><head><style>
body{{font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0}}
.container{{max-width:600px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden}}
.header{{background:#141414;padding:32px;text-align:center}}
.header h1{{color:#fff;font-size:28px;margin:0;letter-spacing:3px}}
.header h1 span{{color:#3B82F6}}
.body{{padding:40px 32px}}.body p{{color:#555;font-size:15px;line-height:1.7}}
.refund-box{{background:#f0fdf4;border:1px solid #bbf7d0;border-radius:12px;padding:24px;margin:24px 0;text-align:center}}
.refund-amount{{font-size:36px;font-weight:700;color:#16a34a;margin:8px 0}}
.footer{{background:#f9f9f9;padding:20px 32px;text-align:center}}
.footer p{{color:#aaa;font-size:12px;margin:0}}
</style></head><body><div class='container'><div class='header'><h1>GYM<span>HUB</span></h1></div>
<div class='body'><p>Hi <strong>{firstName}</strong>,</p>
<p>Your GymHub membership has been <strong>cancelled immediately</strong>. A full refund has been requested.</p>
<div class='refund-box'><div style='font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:1px;color:#166534;'>Refund Amount</div>
<div class='refund-amount'>R{refundAmount:F2}</div><div style='font-size:12px;color:#166534;'>Pending Admin Approval — 5–7 business days</div></div>
<p>Member ID: <strong>{memberNumber}</strong></p></div>
<div class='footer'><p>© {DateTime.Now.Year} GymHub. All rights reserved.</p></div></div></body></html>"
            };
            Send(message);
        }

        // ============================================================
        // REFUND APPROVED
        // ============================================================
        public static void SendRefundApproved(string toEmail, string firstName, string memberNumber, decimal amount)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(SenderName, SenderEmail));
            message.To.Add(new MailboxAddress(firstName, toEmail));
            message.Subject = "GymHub — Your Refund Has Been Approved ✅";
            message.Body = new TextPart("html")
            {
                Text = $@"<!DOCTYPE html><html><head><style>
body{{font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0}}
.container{{max-width:600px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden}}
.header{{background:#141414;padding:32px;text-align:center}}
.header h1{{color:#fff;font-size:28px;margin:0;letter-spacing:3px}}
.header h1 span{{color:#3B82F6}}
.body{{padding:40px 32px}}.body p{{color:#555;font-size:15px;line-height:1.7}}
.refund-box{{background:#f0fdf4;border:1px solid #bbf7d0;border-radius:12px;padding:24px;margin:24px 0;text-align:center}}
.refund-amount{{font-size:40px;font-weight:700;color:#16a34a;margin:8px 0}}
.footer{{background:#f9f9f9;padding:20px 32px;text-align:center}}
.footer p{{color:#aaa;font-size:12px;margin:0}}
</style></head><body><div class='container'><div class='header'><h1>GYM<span>HUB</span></h1></div>
<div class='body'><p>Hi <strong>{firstName}</strong>,</p>
<p>Your refund of <strong style='color:#16a34a;'>R{amount:F2}</strong> has been approved and is being processed.</p>
<div class='refund-box'><div class='refund-amount'>R{amount:F2}</div>
<div style='font-size:13px;color:#166534;'>5–7 business days to your original payment method</div></div>
<p>Member ID: <strong>{memberNumber}</strong></p></div>
<div class='footer'><p>© {DateTime.Now.Year} GymHub. All rights reserved.</p></div></div></body></html>"
            };
            Send(message);
        }

        // ============================================================
        // REFUND REJECTED
        // ============================================================
        public static void SendRefundRejected(string toEmail, string firstName, string memberNumber, decimal amount, string reason)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(SenderName, SenderEmail));
            message.To.Add(new MailboxAddress(firstName, toEmail));
            message.Subject = "GymHub — Update on Your Refund Request";
            message.Body = new TextPart("html")
            {
                Text = $@"<!DOCTYPE html><html><head><style>
body{{font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0}}
.container{{max-width:600px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden}}
.header{{background:#141414;padding:32px;text-align:center}}
.header h1{{color:#fff;font-size:28px;margin:0;letter-spacing:3px}}
.header h1 span{{color:#3B82F6}}
.body{{padding:40px 32px}}.body p{{color:#555;font-size:15px;line-height:1.7}}
.denied-box{{background:#fef2f2;border:1px solid #fecaca;border-radius:12px;padding:24px;margin:24px 0}}
.reason-box{{background:#fff8e1;border:1px solid #fde68a;border-radius:10px;padding:16px 20px;font-size:14px;color:#92400e;margin:16px 0}}
.footer{{background:#f9f9f9;padding:20px 32px;text-align:center}}
.footer p{{color:#aaa;font-size:12px;margin:0}}
</style></head><body><div class='container'><div class='header'><h1>GYM<span>HUB</span></h1></div>
<div class='body'><p>Hi <strong>{firstName}</strong>,</p>
<p>Your refund request of <strong>R{amount:F2}</strong> could not be approved.</p>
<div class='denied-box'><strong style='color:#dc2626;'>❌ Refund Not Approved</strong></div>
<div class='reason-box'><strong>Reason:</strong> {reason}</div>
<p>Contact us at info@gymhub.co.za for queries.</p></div>
<div class='footer'><p>© {DateTime.Now.Year} GymHub. All rights reserved.</p></div></div></body></html>"
            };
            Send(message);
        }

        // ============================================================
        // FREEZE — Member submitted request
        // ============================================================
        public static void SendFreezeRequested(string toEmail, string firstName, string memberNumber,
            string startDate, string endDate, int days)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(SenderName, SenderEmail));
            message.To.Add(new MailboxAddress(firstName, toEmail));
            message.Subject = "GymHub — Freeze Request Received ❄️";
            message.Body = new TextPart("html")
            {
                Text = $@"<!DOCTYPE html><html><head><style>
body{{font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0}}
.container{{max-width:600px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden}}
.header{{background:#141414;padding:32px;text-align:center}}
.header h1{{color:#fff;font-size:28px;margin:0;letter-spacing:3px}}
.header h1 span{{color:#3B82F6}}
.body{{padding:40px 32px}}.body p{{color:#555;font-size:15px;line-height:1.7}}
.freeze-box{{background:#eff6ff;border:1px solid #bfdbfe;border-radius:12px;padding:24px;margin:24px 0}}
.freeze-dates{{font-size:20px;font-weight:700;color:#1d4ed8;margin:8px 0}}
.info-row{{display:flex;justify-content:space-between;padding:9px 0;border-bottom:1px solid #e2e8f0;font-size:14px}}
.info-row:last-child{{border-bottom:none}}
.notice-box{{background:#fff8e1;border:1px solid #fde68a;border-radius:10px;padding:14px 18px;font-size:13px;color:#92400e;margin:16px 0}}
.footer{{background:#f9f9f9;padding:20px 32px;text-align:center}}
.footer p{{color:#aaa;font-size:12px;margin:0}}
</style></head><body><div class='container'><div class='header'><h1>GYM<span>HUB</span></h1></div>
<div class='body'>
<p>Hi <strong>{firstName}</strong>,</p>
<p>We've received your membership freeze request. It is currently <strong>pending admin review</strong>.</p>
<div class='freeze-box'>
<div style='font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:1px;color:#1e40af;margin-bottom:8px;'>Freeze Period Requested</div>
<div class='freeze-dates'>❄️ {startDate} → {endDate}</div>
<div style='font-size:13px;color:#3730a3;margin-top:4px;'>{days} days</div>
</div>
<div style='margin:20px 0;'>
<div class='info-row'><span>Member ID</span><span><strong>{memberNumber}</strong></span></div>
<div class='info-row'><span>Requested On</span><span><strong>{DateTime.Now:dd MMM yyyy}</strong></span></div>
<div class='info-row'><span>Duration</span><span><strong>{days} days</strong></span></div>
<div class='info-row'><span>Status</span><span><strong style='color:#D97706;'>Pending Approval</strong></span></div>
</div>
<div class='notice-box'>⚠️ Your membership remains <strong>Active</strong> until the admin reviews your request. You will receive another email once a decision is made.</div>
<p>If you did not submit this request, please contact us immediately at <strong>info@gymhub.co.za</strong>.</p>
<p>— The GymHub Team</p>
</div>
<div class='footer'><p>© {DateTime.Now.Year} GymHub. All rights reserved.</p><p>This is an automated message — please do not reply.</p></div>
</div></body></html>"
            };
            Send(message);
        }

        // ============================================================
        // FREEZE — Admin approved
        // ============================================================
        public static void SendFreezeApproved(string toEmail, string firstName, string memberNumber,
            string startDate, string endDate, int days,
            string originalExpiry, string newExpiry)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(SenderName, SenderEmail));
            message.To.Add(new MailboxAddress(firstName, toEmail));
            message.Subject = "GymHub — Your Freeze Has Been Approved ✅❄️";
            message.Body = new TextPart("html")
            {
                Text = $@"<!DOCTYPE html><html><head><style>
body{{font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0}}
.container{{max-width:600px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden}}
.header{{background:#141414;padding:32px;text-align:center}}
.header h1{{color:#fff;font-size:28px;margin:0;letter-spacing:3px}}
.header h1 span{{color:#3B82F6}}
.body{{padding:40px 32px}}.body p{{color:#555;font-size:15px;line-height:1.7}}
.approved-badge{{display:inline-block;background:rgba(22,163,74,0.1);border:1px solid rgba(22,163,74,0.3);color:#16a34a;padding:6px 18px;border-radius:50px;font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:1px;margin-bottom:20px;}}
.freeze-box{{background:#eff6ff;border:1px solid #bfdbfe;border-radius:12px;padding:24px;margin:24px 0;text-align:center}}
.freeze-dates{{font-size:22px;font-weight:700;color:#1d4ed8;margin:8px 0}}
.info-row{{display:flex;justify-content:space-between;padding:9px 0;border-bottom:1px solid #e2e8f0;font-size:14px}}
.info-row:last-child{{border-bottom:none}}
.expiry-box{{background:#f0fdf4;border:1px solid #bbf7d0;border-radius:12px;padding:20px 24px;margin:20px 0}}
.new-expiry{{font-size:22px;font-weight:700;color:#16a34a;margin-top:6px}}
.warn-box{{background:rgba(239,68,68,0.05);border:1px solid rgba(239,68,68,0.15);border-radius:10px;padding:14px 18px;font-size:13px;color:#DC2626;margin:20px 0}}
.footer{{background:#f9f9f9;padding:20px 32px;text-align:center}}
.footer p{{color:#aaa;font-size:12px;margin:0}}
</style></head><body><div class='container'><div class='header'><h1>GYM<span>HUB</span></h1></div>
<div class='body'>
<div class='approved-badge'>✓ Freeze Approved</div>
<p>Hi <strong>{firstName}</strong>,</p>
<p>Great news! Your membership freeze request has been <strong style='color:#16a34a;'>approved</strong>.</p>
<div class='freeze-box'>
<div style='font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:1px;color:#1e40af;margin-bottom:8px;'>Freeze Period</div>
<div class='freeze-dates'>❄️ {startDate} → {endDate}</div>
<div style='font-size:13px;color:#3730a3;margin-top:4px;'>{days} days</div>
</div>
<div style='margin:20px 0;'>
<div class='info-row'><span>Member ID</span><span><strong>{memberNumber}</strong></span></div>
<div class='info-row'><span>Freeze Duration</span><span><strong>{days} days</strong></span></div>
<div class='info-row'><span>Original Expiry</span><span><strong>{originalExpiry}</strong></span></div>
</div>
<div class='expiry-box'>
<div style='font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:1px;color:#166534;'>Your New Membership Expiry</div>
<div class='new-expiry'>📅 {newExpiry}</div>
<div style='font-size:12px;color:#166534;margin-top:4px;'>Extended by {days} days to account for your freeze period</div>
</div>
<div class='warn-box'>
⚠️ During the freeze period ({startDate} – {endDate}), you <strong>cannot access gym services</strong>.
Payments are paused for this duration. Your membership will automatically resume on <strong>{endDate}</strong>.
</div>
<p>You can still log in to your GymHub account during the freeze.</p>
<p>— The GymHub Team</p>
</div>
<div class='footer'><p>© {DateTime.Now.Year} GymHub. All rights reserved.</p><p>This is an automated message — please do not reply.</p></div>
</div></body></html>"
            };
            Send(message);
        }

        // ============================================================
        // FREEZE — Admin rejected
        // ============================================================
        public static void SendFreezeRejected(string toEmail, string firstName, string memberNumber,
            string startDate, string endDate, string reason)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(SenderName, SenderEmail));
            message.To.Add(new MailboxAddress(firstName, toEmail));
            message.Subject = "GymHub — Update on Your Freeze Request";
            message.Body = new TextPart("html")
            {
                Text = $@"<!DOCTYPE html><html><head><style>
body{{font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0}}
.container{{max-width:600px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden}}
.header{{background:#141414;padding:32px;text-align:center}}
.header h1{{color:#fff;font-size:28px;margin:0;letter-spacing:3px}}
.header h1 span{{color:#3B82F6}}
.body{{padding:40px 32px}}.body p{{color:#555;font-size:15px;line-height:1.7}}
.denied-box{{background:#fef2f2;border:1px solid #fecaca;border-radius:12px;padding:24px;margin:24px 0}}
.info-row{{display:flex;justify-content:space-between;padding:9px 0;border-bottom:1px solid #e2e8f0;font-size:14px}}
.info-row:last-child{{border-bottom:none}}
.reason-box{{background:#fff8e1;border:1px solid #fde68a;border-radius:10px;padding:16px 20px;font-size:14px;color:#92400e;margin:16px 0}}
.footer{{background:#f9f9f9;padding:20px 32px;text-align:center}}
.footer p{{color:#aaa;font-size:12px;margin:0}}
</style></head><body><div class='container'><div class='header'><h1>GYM<span>HUB</span></h1></div>
<div class='body'>
<p>Hi <strong>{firstName}</strong>,</p>
<p>We have reviewed your freeze request for <strong>{startDate} – {endDate}</strong>.</p>
<div class='denied-box'><strong style='color:#dc2626;'>❌ Freeze Request Not Approved</strong>
<p style='color:#374151;font-size:13px;margin:8px 0 0;line-height:1.6;'>Unfortunately, your freeze request could not be approved at this time.</p></div>
<div class='reason-box'><strong>Reason:</strong> {reason}</div>
<div style='margin:20px 0;'>
<div class='info-row'><span>Member ID</span><span><strong>{memberNumber}</strong></span></div>
<div class='info-row'><span>Requested Period</span><span><strong>{startDate} – {endDate}</strong></span></div>
<div class='info-row'><span>Decision Date</span><span><strong>{DateTime.Now:dd MMM yyyy}</strong></span></div>
</div>
<p>Your membership remains <strong>Active</strong>. If you believe this is incorrect or would like to discuss further, contact us at <strong>info@gymhub.co.za</strong>.</p>
<p>— The GymHub Team</p>
</div>
<div class='footer'><p>© {DateTime.Now.Year} GymHub. All rights reserved.</p><p>This is an automated message — please do not reply.</p></div>
</div></body></html>"
            };
            Send(message);
        }

        // ============================================================
        // FITNESS PROGRESS ALERT
        // ============================================================
        public static void SendFitnessAlert(
            string toEmail, string firstName,
            string goalType, int weekNumber,
            string currentWeight, string targetWeight)
        {
            string tipHtml;
            switch (goalType)
            {
                case "Weight Loss":
                    tipHtml = @"<ul style='color:#374151;font-size:14px;line-height:1.8;'>
                        <li>Aim for a 300–500 kcal daily deficit through diet and exercise</li>
                        <li>Complete at least 3 cardio sessions this week (30+ minutes each)</li>
                        <li>Track your meals — liquid calories are easy to miss</li>
                        <li>Stay hydrated: 2–3 litres of water per day</li>
                    </ul>";
                    break;
                case "Muscle Gain":
                    tipHtml = @"<ul style='color:#374151;font-size:14px;line-height:1.8;'>
                        <li>Ensure you are in a slight caloric surplus (200–300 kcal above TDEE)</li>
                        <li>Prioritise protein: aim for 1.6–2.2g per kg of body weight</li>
                        <li>Progressive overload — increase weight or reps each session</li>
                        <li>Sleep 7–9 hours for optimal muscle recovery</li>
                    </ul>";
                    break;
                case "Endurance":
                    tipHtml = @"<ul style='color:#374151;font-size:14px;line-height:1.8;'>
                        <li>Target at least 3 cardio sessions of 30+ minutes this week</li>
                        <li>Mix intensities: 2 moderate sessions + 1 higher-intensity session</li>
                        <li>Don't skip rest days — recovery is part of building endurance</li>
                        <li>Stay consistent: endurance builds over 8–12 week blocks</li>
                    </ul>";
                    break;
                default:
                    tipHtml = @"<ul style='color:#374151;font-size:14px;line-height:1.8;'>
                        <li>Check in at the gym at least 3 times this week</li>
                        <li>Balance strength and cardio training</li>
                        <li>Focus on consistent habits over intense short bursts</li>
                    </ul>";
                    break;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(SenderName, SenderEmail));
            message.To.Add(new MailboxAddress(firstName, toEmail));
            message.Subject = $"GymHub Fitness Check-In — Week {weekNumber} Progress Alert 💪";
            message.Body = new TextPart("html")
            {
                Text = $@"<!DOCTYPE html><html><head><meta charset='UTF-8'/></head>
<body style='margin:0;padding:0;background:#f3f4f6;font-family:Arial,sans-serif;'>
  <div style='max-width:560px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);'>
    <div style='background:#141414;padding:32px;text-align:center;'>
      <h1 style='color:#fff;font-size:28px;margin:0;letter-spacing:3px;'>GYM<span style='color:#3B82F6;'>HUB</span></h1>
      <div style='font-size:13px;color:rgba(255,255,255,0.5);margin-top:6px;'>Fitness Progress Alert</div>
    </div>
    <div style='padding:36px 32px;'>
      <p style='font-size:18px;font-weight:700;color:#111;margin-bottom:8px;'>
        Hey {firstName}, your Week {weekNumber} check-in needs attention 💪
      </p>
      <p style='color:#6B7280;font-size:14px;line-height:1.6;margin-bottom:24px;'>
        Based on your latest progress log, you may be falling slightly behind your
        <strong style='color:#1A5FD4;'>{goalType}</strong> goal.
        Don't worry — this happens to everyone. Here's what to focus on this week:
      </p>
      <div style='background:#F0F7FF;border-left:4px solid #1A5FD4;border-radius:0 8px 8px 0;padding:16px 20px;margin-bottom:24px;'>
        <div style='font-size:12px;font-weight:700;color:#1A5FD4;margin-bottom:8px;text-transform:uppercase;letter-spacing:0.5px;'>Your Action Plan for This Week</div>
        {tipHtml}
      </div>
      <table style='width:100%;border-collapse:collapse;margin-bottom:24px;'>
        <tr>
          <td style='background:#F9FAFB;border-radius:8px;padding:14px;text-align:center;width:33%;'>
            <div style='font-size:11px;color:#9CA3AF;text-transform:uppercase;letter-spacing:0.5px;'>Current Weight</div>
            <div style='font-size:20px;font-weight:700;color:#111;margin-top:4px;'>{currentWeight} kg</div>
          </td>
          <td style='width:8px;'></td>
          <td style='background:#F9FAFB;border-radius:8px;padding:14px;text-align:center;width:33%;'>
            <div style='font-size:11px;color:#9CA3AF;text-transform:uppercase;letter-spacing:0.5px;'>Target Weight</div>
            <div style='font-size:20px;font-weight:700;color:#1A5FD4;margin-top:4px;'>{targetWeight} kg</div>
          </td>
          <td style='width:8px;'></td>
          <td style='background:#F9FAFB;border-radius:8px;padding:14px;text-align:center;width:33%;'>
            <div style='font-size:11px;color:#9CA3AF;text-transform:uppercase;letter-spacing:0.5px;'>Week</div>
            <div style='font-size:20px;font-weight:700;color:#111;margin-top:4px;'>{weekNumber}</div>
          </td>
        </tr>
      </table>
      <p style='color:#6B7280;font-size:13px;line-height:1.6;'>
        Log your next check-in on your GymHub dashboard to keep tracking your progress.
        Remember: <strong>consistency beats perfection every time.</strong>
      </p>
      <div style='text-align:center;margin-top:28px;'>
        <a href='https://gymhub.co.za/Dashboard'
           style='background:linear-gradient(135deg,#1A5FD4,#3B82F6);color:#fff;
                  text-decoration:none;padding:13px 32px;border-radius:8px;
                  font-size:14px;font-weight:700;display:inline-block;'>
          Log This Week's Progress
        </a>
      </div>
    </div>
    <div style='padding:20px 32px;background:#f9f9f9;border-top:1px solid #e5e7eb;
                text-align:center;font-size:11px;color:#aaa;'>
      © {DateTime.Now.Year} GymHub (Pty) Ltd | Durban, KZN | info@gymhub.co.za<br/>
      You're receiving this because you have an active fitness goal on GymHub.
    </div>
  </div>
</body></html>"
            };
            Send(message);
        }

        // ============================================================
        // CAFÉ REDEMPTION CONFIRMATION — with embedded QR code
        // Call from DashboardController.RedeemProduct() passing redemptionId
        // ============================================================
        public static void SendRedemptionConfirmation(
            string toEmail, string firstName,
            string productName, string emoji,
            int pointsSpent, string collectionCode,
            int redemptionId)
        {
            // ── Build QR content identical to what the scanner reads ──
            string qrContent = $"GH-CAFE|ORDER:{redemptionId}|CODE:{collectionCode}|EXP:{DateTime.Now.AddMinutes(30):yyyyMMddHHmmss}";

            byte[] qrBytes;
            using (var qrGenerator = new QRCodeGenerator())
            using (var qrData = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q))
            using (var qrCode = new QRCode(qrData))
            using (var bitmap = qrCode.GetGraphic(10))
            using (var ms = new System.IO.MemoryStream())
            {
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                qrBytes = ms.ToArray();
            }

            var builder = new BodyBuilder();
            var qrImage = builder.LinkedResources.Add("cafe-order-qr.png", qrBytes, ContentType.Parse("image/png"));
            qrImage.ContentId = MimeUtils.GenerateMessageId();
            qrImage.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
            string cid = qrImage.ContentId;

            builder.HtmlBody = $@"<!DOCTYPE html><html><head><style>
body{{font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0}}
.container{{max-width:560px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08)}}
.header{{background:#141414;padding:32px;text-align:center}}
.header h1{{color:#fff;font-size:28px;margin:0;letter-spacing:3px}}
.header h1 span{{color:#3B82F6}}
.body{{padding:36px 32px}}
.body p{{color:#555;font-size:15px;line-height:1.7}}
.product-box{{background:#F0F7FF;border:1px solid #BFDBFE;border-radius:12px;padding:24px;margin:24px 0;text-align:center}}
.product-emoji{{font-size:56px;display:block;margin-bottom:12px}}
.product-name{{font-size:22px;font-weight:700;color:#1E40AF;margin-bottom:6px}}
.product-pts{{font-size:14px;color:#3B82F6;font-weight:600}}
.code-box{{background:#141414;border-radius:12px;padding:28px;margin:24px 0;text-align:center}}
.code-label{{font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:2px;color:rgba(255,255,255,0.5);margin-bottom:12px}}
.code-value{{font-size:44px;font-weight:700;letter-spacing:10px;color:#3B82F6;font-family:monospace}}
.code-sub{{font-size:12px;color:rgba(255,255,255,0.4);margin-top:10px}}
.qr-section{{text-align:center;margin:24px 0;padding:24px;background:#f9f9f9;border-radius:12px;border:2px dashed #ddd}}
.qr-section h3{{color:#333;font-size:15px;margin-bottom:16px;margin-top:0}}
.qr-section img{{width:200px;height:200px;border:4px solid #141414;border-radius:8px;display:block;margin:0 auto}}
.qr-note{{font-size:12px;color:#999;margin-top:10px;margin-bottom:0}}
.steps{{margin:24px 0}}
.step{{display:flex;align-items:flex-start;gap:14px;padding:10px 0;border-bottom:1px solid #F3F4F6;font-size:14px;color:#374151}}
.step:last-child{{border-bottom:none}}
.step-num{{background:#1A5FD4;color:#fff;min-width:26px;height:26px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:12px;font-weight:700}}
.notice{{background:#FFF8E1;border:1px solid #FDE68A;border-radius:8px;padding:14px 18px;font-size:13px;color:#92400E;margin-top:20px}}
.footer{{background:#f9f9f9;padding:20px 32px;text-align:center;border-top:1px solid #E5E7EB}}
.footer p{{color:#aaa;font-size:12px;margin:0}}
</style></head><body>
<div class='container'>
  <div class='header'><h1>GYM<span>HUB</span></h1></div>
  <div class='body'>
    <p>Hi <strong>{firstName}</strong>, your redemption was successful! 🎉</p>

    <div class='product-box'>
      <span class='product-emoji'>{emoji}</span>
      <div class='product-name'>{productName}</div>
      <div class='product-pts'>{pointsSpent} points redeemed</div>
    </div>

    <div class='code-box'>
      <div class='code-label'>Your Collection Code</div>
      <div class='code-value'>{collectionCode}</div>
      <div class='code-sub'>Show this code OR scan the QR below at the café counter</div>
    </div>

    <div class='qr-section'>
      <h3>📱 Scan at the café counter</h3>
      <img src='cid:{cid}' alt='Collection QR Code' />
      <p class='qr-note'>Valid for 30 minutes from time of redemption</p>
    </div>

    <div class='steps'>
      <div class='step'><div class='step-num'>1</div><div>Head to the GymHub café counter</div></div>
      <div class='step'><div class='step-num'>2</div><div>Show this email — scan the QR code above or read out your code <strong>{collectionCode}</strong></div></div>
      <div class='step'><div class='step-num'>3</div><div>Collect your {emoji} <strong>{productName}</strong> and enjoy!</div></div>
    </div>

    <div class='notice'>
      ⚠️ This QR code expires <strong>30 minutes</strong> after redemption. If it expires, go to your GymHub account and show your QR code from there.
      Contact <strong>info@gymhub.co.za</strong> if you have any issues.
    </div>
  </div>
  <div class='footer'>
    <p>© {DateTime.Now.Year} GymHub (Pty) Ltd | Durban, KZN | info@gymhub.co.za</p>
    <p style='margin-top:4px;'>This is an automated message — please do not reply.</p>
  </div>
</div>
</body></html>";

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(SenderName, SenderEmail));
            message.To.Add(new MailboxAddress(firstName, toEmail));
            message.Subject = $"GymHub Café — Your {productName} is ready! {emoji}";
            message.Body = builder.ToMessageBody();
            Send(message);
        }

        public static void SendTrainerWelcome(string toEmail, string fullName, string trainerCode, string tempPassword)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(SenderName, SenderEmail));
            message.To.Add(new MailboxAddress(fullName, toEmail));
            message.Subject = "Welcome to GymHub — Your Trainer Account";
            message.Body = new TextPart("html")
            {
                Text = $@"<!DOCTYPE html><html><head><style>
body{{font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0}}
.container{{max-width:600px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden}}
.header{{background:#141414;padding:32px;text-align:center}}
.header h1{{color:#fff;font-size:28px;margin:0;letter-spacing:3px}}
.header h1 span{{color:#3B82F6}}
.body{{padding:40px 32px}}.body p{{color:#555;font-size:15px;line-height:1.7}}
.card{{background:#141414;border-radius:12px;padding:28px;margin:28px 0}}
.card-row{{display:flex;justify-content:space-between;padding:10px 0;border-bottom:1px solid rgba(255,255,255,0.06)}}
.card-row:last-child{{border-bottom:none}}
.card-label{{color:#999;font-size:12px;text-transform:uppercase;letter-spacing:1px}}
.card-value{{color:#fff;font-size:15px;font-weight:bold}}
.card-value.highlight{{color:#3B82F6;letter-spacing:2px}}
.warning{{background:#fff8e1;border:1px solid #fde68a;border-radius:10px;padding:14px 18px;font-size:13px;color:#92400e;margin:16px 0}}
.footer{{background:#f9f9f9;padding:20px 32px;text-align:center}}
.footer p{{color:#aaa;font-size:12px;margin:0}}
</style></head><body>
<div class='container'>
  <div class='header'><h1>GYM<span>HUB</span></h1></div>
  <div class='body'>
    <p>Hi <strong>{fullName}</strong>,</p>
    <p>Your trainer account has been created by the GymHub admin. Below are your login details:</p>
    <div class='card'>
      <div class='card-row'>
        <span class='card-label'>Full Name</span>
        <span class='card-value'>{fullName}</span>
      </div>
      <div class='card-row'>
        <span class='card-label'>Trainer Code</span>
        <span class='card-value highlight'>{trainerCode}</span>
      </div>
      <div class='card-row'>
        <span class='card-label'>Login Email</span>
        <span class='card-value'>{toEmail}</span>
      </div>
      <div class='card-row'>
        <span class='card-label'>Temporary Password</span>
        <span class='card-value highlight'>{tempPassword}</span>
      </div>
    </div>
    <div class='warning'>
      ⚠️ You will be required to <strong>change your password</strong> on your first login.
      Do not share these credentials with anyone.
    </div>
    <p>— The GymHub Admin Team</p>
  </div>
  <div class='footer'><p>© {DateTime.Now.Year} GymHub. All rights reserved.</p></div>
</div>
</body></html>"
            };
            Send(message);
        }

        // ============================================================
        // CORE SMTP SENDER
        // ============================================================
        private static void Send(MimeMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (string.IsNullOrWhiteSpace(SenderEmail) ||
                string.IsNullOrWhiteSpace(AppPassword))
            {
                System.Diagnostics.Debug.WriteLine(
                    "[EMAIL ERROR] GymHub SMTP settings are missing " +
                    "from Web.config.");

                return;
            }

            try
            {
                using (var client = new SmtpClient())
                {
                    client.Timeout = 30000;

                    // Use normal TLS certificate validation. Never install
                    // a callback that accepts every certificate.
                    client.Connect(
                        SmtpHost,
                        SmtpPort,
                        SecureSocketOptions.StartTls);

                    client.Authenticate(SenderEmail, AppPassword);
                    client.Send(message);
                    client.Disconnect(true);

                    System.Diagnostics.Debug.WriteLine(
                        $"[EMAIL SUCCESS] {message.Subject}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[EMAIL ERROR] Subject: {message.Subject}; " +
                    $"Details: {ex}");

            }
        }
        public static void SendTechnicianWelcome(string toEmail, string fullName, string technicianCode, string tempPassword)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(SenderName, SenderEmail));
            message.To.Add(new MailboxAddress(fullName, toEmail));
            message.Subject = "Welcome to GymHub — Your Technician Account";
            message.Body = new TextPart("html")
            {
                Text = $@"<!DOCTYPE html><html><head><style>
body{{font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0}}
.container{{max-width:600px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden}}
.header{{background:#141414;padding:32px;text-align:center}}
.header h1{{color:#fff;font-size:28px;margin:0;letter-spacing:3px}}
.header h1 span{{color:#3B82F6}}
.body{{padding:40px 32px}}.body p{{color:#555;font-size:15px;line-height:1.7}}
.card{{background:#141414;border-radius:12px;padding:28px;margin:28px 0}}
.card-row{{display:flex;justify-content:space-between;padding:10px 0;border-bottom:1px solid rgba(255,255,255,0.06)}}
.card-row:last-child{{border-bottom:none}}
.card-label{{color:#999;font-size:12px;text-transform:uppercase;letter-spacing:1px}}
.card-value{{color:#fff;font-size:15px;font-weight:bold}}
.card-value.highlight{{color:#3B82F6;letter-spacing:2px}}
.warning{{background:#fff8e1;border:1px solid #fde68a;border-radius:10px;padding:14px 18px;font-size:13px;color:#92400e;margin:16px 0}}
.footer{{background:#f9f9f9;padding:20px 32px;text-align:center}}
.footer p{{color:#aaa;font-size:12px;margin:0}}
</style></head><body>
<div class='container'>
  <div class='header'><h1>GYM<span>HUB</span></h1></div>
  <div class='body'>
    <p>Hi <strong>{fullName}</strong>,</p>
    <p>Your technician account has been created by the GymHub admin. Below are your login details:</p>
    <div class='card'>
      <div class='card-row'>
        <span class='card-label'>Full Name</span>
        <span class='card-value'>{fullName}</span>
      </div>
      <div class='card-row'>
        <span class='card-label'>Technician Code</span>
        <span class='card-value highlight'>{technicianCode}</span>
      </div>
      <div class='card-row'>
        <span class='card-label'>Login Email</span>
        <span class='card-value'>{toEmail}</span>
      </div>
      <div class='card-row'>
        <span class='card-label'>Temporary Password</span>
        <span class='card-value highlight'>{tempPassword}</span>
      </div>
    </div>
    <div class='warning'>
      ⚠️ You will be required to <strong>change your password</strong> on your first login.
      Do not share these credentials with anyone.
    </div>
    <p>— The GymHub Admin Team</p>
  </div>
  <div class='footer'><p>© {DateTime.Now.Year} GymHub. All rights reserved.</p></div>
</div>
</body></html>"
            };
            Send(message);
        }

        public static void SendComplaintNotificationToAdmin(
    string referenceNumber, string memberName, string memberEmail,
    string category, string details)
        {
            // e.g. send to a fixed admin/support inbox, e.g. complaints@gymhub.co.za
            string subject = $"New Complaint Submitted — {referenceNumber}";
            string body = $"Member: {memberName} ({memberEmail})\n" +
                          $"Category: {category}\n" +
                          $"Reference: {referenceNumber}\n\n" +
                          $"Details:\n{details}";

            // ... send using your existing mail client setup
        }

        public static void SendComplaintStatusUpdate(
            string memberEmail, string memberFirstName, string referenceNumber,
            string status, string staffComments)
        {
            string subject = $"Update on your complaint {referenceNumber}";
            string body = $"Hi {memberFirstName},\n\n" +
                          $"Your complaint ({referenceNumber}) status is now: {status}\n" +
                          (string.IsNullOrWhiteSpace(staffComments) ? "" : $"\nStaff comments:\n{staffComments}\n") +
                          $"\nYou can check the latest status anytime under My Complaints.";

            // ... send using your existing mail client setup
        }

        public static void SendReferralReward(
    string referrerEmail, string referrerFirstName,
    string referredFriendName, int pointsAwarded)
        {
            string subject = "You just earned referral points! 🎉";
            string body = $"Hi {referrerFirstName},\n\n" +
                          $"{referredFriendName} just joined GymHub using your referral link!\n\n" +
                          $"You've been credited {pointsAwarded} loyalty points as a thank-you.\n\n" +
                          $"Keep sharing your referral link from the Refer a Friend page to earn more.";

            // ... send using your existing mail client setup
        }

        // ============================================================

        // LOCKER PIN RESET — new PIN generated after "Forgot PIN"

        // ============================================================

        public static void SendLockerPinReset(string toEmail, string firstName, string lockerNumber, string newPin)

        {

            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(SenderName, SenderEmail));

            message.To.Add(new MailboxAddress(firstName, toEmail));

            message.Subject = "GymHub — Your Locker PIN Has Been Reset 🔒";

            message.Body = new TextPart("html")

            {

                Text = $@"<!DOCTYPE html><html><head><style>

body{{font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0}}

.container{{max-width:600px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden}}

.header{{background:#141414;padding:32px;text-align:center}}

.header h1{{color:#fff;font-size:28px;margin:0;letter-spacing:3px}}

.header h1 span{{color:#3B82F6}}

.body{{padding:40px 32px}}.body p{{color:#555;font-size:15px;line-height:1.7}}

.pin-box{{background:#141414;border-radius:12px;padding:28px;text-align:center;margin:28px 0}}

.pin-label{{font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:2px;color:rgba(255,255,255,0.5);margin-bottom:12px}}

.pin-code{{font-size:42px;font-weight:bold;letter-spacing:12px;color:#3B82F6}}

.locker-tag{{font-size:13px;color:rgba(255,255,255,0.4);margin-top:10px}}

.warning{{background:#fff8e1;border:1px solid #fde68a;border-radius:10px;padding:14px 18px;font-size:13px;color:#92400e;margin:16px 0}}

.footer{{background:#f9f9f9;padding:20px 32px;text-align:center}}

.footer p{{color:#aaa;font-size:12px;margin:0}}
</style></head><body><div class='container'><div class='header'><h1>GYM<span>HUB</span></h1></div>
<div class='body'>
<p>Hi <strong>{firstName}</strong>,</p>
<p>Your access PIN for <strong>Locker {lockerNumber}</strong> has been reset. Your previous PIN no longer works.</p>
<div class='pin-box'>
<div class='pin-label'>Your New PIN</div>
<div class='pin-code'>{newPin}</div>
<div class='locker-tag'>Locker {lockerNumber}</div>
</div>
<div class='warning'>⚠️ For your security, do not share this PIN with anyone. If you did not request this reset, please contact GymHub staff immediately at info@gymhub.co.za.</div>
<p>— The GymHub Team</p>
</div>
<div class='footer'><p>© {DateTime.Now.Year} GymHub. All rights reserved.</p><p>This is an automated message — please do not reply.</p></div>
</div></body></html>"

            };

            Send(message);

        }
        public static void SendOrderUncollectedPendingReview(
    string toEmail, string toName, string referenceNumber)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(SenderName, SenderEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = "GymHub - Collection Deadline Passed (" + referenceNumber + ")";
            message.Body = new TextPart("html")
            {
                Text = BrandedUncollectedBody(
                    toName,
                    "Collection deadline passed",
                    referenceNumber,
                    "Your order was not collected before the pickup deadline. " +
                    "The reserved products have been released back into available stock. " +
                    "The order is now awaiting an administrative resolution.",
                    "We will notify you when the review has been completed.")
            };
            Send(message);
        }

        public static void SendPickupDeadlineExtended(
            string toEmail, string toName, string referenceNumber, DateTime newDeadline)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(SenderName, SenderEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = "GymHub - Pickup Deadline Extended (" + referenceNumber + ")";
            message.Body = new TextPart("html")
            {
                Text = BrandedUncollectedBody(
                    toName,
                    "Pickup deadline extended",
                    referenceNumber,
                    "Your order has been reopened and the products have been reserved again.",
                    "Please collect it by " + newDeadline.ToString("dd MMMM yyyy 'at' HH:mm") +
                    " and present your pickup QR code at the GymHub counter.")
            };
            Send(message);
        }

        public static void SendUncollectedOrderRefunded(
            string toEmail, string toName, string referenceNumber,
            decimal amount, string refundReference)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(SenderName, SenderEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = "GymHub - Uncollected Order Refund (" + referenceNumber + ")";
            message.Body = new TextPart("html")
            {
                Text = BrandedUncollectedBody(
                    toName,
                    "Refund submitted",
                    referenceNumber,
                    "GymHub submitted a refund of <strong>R" + amount.ToString("F2") +
                    "</strong> through the original Paystack transaction.",
                    "Refund reference: <strong>" + System.Net.WebUtility.HtmlEncode(refundReference) +
                    "</strong>. Bank processing times may apply.")
            };
            Send(message);
        }

        private static string BrandedUncollectedBody(
            string name, string heading, string reference, string message, string nextStep)
        {
            return @"<!doctype html><html><head><meta charset='utf-8'><style>
body{margin:0;background:#f1f5f9;font-family:Arial,sans-serif;color:#334155}
.wrap{max-width:620px;margin:30px auto;background:#fff;border-radius:14px;overflow:hidden}
.head{background:#0f172a;padding:28px 32px;color:#fff}.brand{font-size:25px;font-weight:900;letter-spacing:2px}.brand span{color:#3b82f6}
.body{padding:32px}.body h2{margin:0 0 16px;color:#0f172a}.body p{font-size:14px;line-height:1.7}
.ref{background:#eff6ff;color:#1d4ed8;border-radius:10px;padding:14px;text-align:center;font-size:18px;font-weight:800;margin:20px 0}
.next{background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:14px}
.foot{background:#f8fafc;padding:18px 32px;color:#94a3b8;font-size:11px;text-align:center}</style></head><body><div class='wrap'>
<div class='head'><div class='brand'>GYM<span>HUB</span></div></div><div class='body'><p>Hi <strong>" +
                System.Net.WebUtility.HtmlEncode(name) + "</strong>,</p><h2>" + heading +
                "</h2><div class='ref'>" + System.Net.WebUtility.HtmlEncode(reference) +
                "</div><p>" + message + "</p><div class='next'>" + nextStep +
                "</div></div><div class='foot'>&copy; " + DateTime.Now.Year +
                " GymHub. This is an automated service message.</div></div></body></html>";
        }


        public static void SendStoreStaffWelcome(string toEmail, string fullName, string StoreStaffCode, string tempPassword)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(SenderName, SenderEmail));
            message.To.Add(new MailboxAddress(fullName, toEmail));
            message.Subject = "Welcome to GymHub — Your Store Staff Account";
            message.Body = new TextPart("html")
            {
                Text = $@"<!DOCTYPE html><html><head><style>
body{{font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0}}
.container{{max-width:600px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden}}
.header{{background:#141414;padding:32px;text-align:center}}
.header h1{{color:#fff;font-size:28px;margin:0;letter-spacing:3px}}
.header h1 span{{color:#3B82F6}}
.body{{padding:40px 32px}}.body p{{color:#555;font-size:15px;line-height:1.7}}
.card{{background:#141414;border-radius:12px;padding:28px;margin:28px 0}}
.card-row{{display:flex;justify-content:space-between;padding:10px 0;border-bottom:1px solid rgba(255,255,255,0.06)}}
.card-row:last-child{{border-bottom:none}}
.card-label{{color:#999;font-size:12px;text-transform:uppercase;letter-spacing:1px}}
.card-value{{color:#fff;font-size:15px;font-weight:bold}}
.card-value.highlight{{color:#3B82F6;letter-spacing:2px}}
.warning{{background:#fff8e1;border:1px solid #fde68a;border-radius:10px;padding:14px 18px;font-size:13px;color:#92400e;margin:16px 0}}
.footer{{background:#f9f9f9;padding:20px 32px;text-align:center}}
.footer p{{color:#aaa;font-size:12px;margin:0}}
</style></head><body>
<div class='container'>
  <div class='header'><h1>GYM<span>HUB</span></h1></div>
  <div class='body'>
    <p>Hi <strong>{fullName}</strong>,</p>
    <p>Your Store Staff account has been created by the GymHub admin. Below are your login details:</p>
    <div class='card'>
      <div class='card-row'>
        <span class='card-label'>Full Name</span>
        <span class='card-value'>{fullName}</span>
      </div>
      <div class='card-row'>
        <span class='card-label'>Trainer Code</span>
        <span class='card-value highlight'>{StoreStaffCode}</span>
      </div>
      <div class='card-row'>
        <span class='card-label'>Login Email</span>
        <span class='card-value'>{toEmail}</span>
      </div>
      <div class='card-row'>
        <span class='card-label'>Temporary Password</span>
        <span class='card-value highlight'>{tempPassword}</span>
      </div>
    </div>
    <div class='warning'>
      ⚠️ You will be required to <strong>change your password</strong> on your first login.
      Do not share these credentials with anyone.
    </div>
    <p>— The GymHub Admin Team</p>
  </div>
  <div class='footer'><p>© {DateTime.Now.Year} GymHub. All rights reserved.</p></div>
</div>
</body></html>"
            };
            Send(message);
        }
        // ============================================================
        // STORE ORDER FIRST COLLECTION REMINDER
        // Sent approximately 48 hours after the order becomes ready.
        // ============================================================
        public static void SendOrderFirstReminder(
            string toEmail,
            string toName,
            string referenceNumber)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                return;

            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(SenderName, SenderEmail));

            message.To.Add(
                new MailboxAddress(
                    string.IsNullOrWhiteSpace(toName)
                        ? "GymHub Member"
                        : toName,
                    toEmail));

            message.Subject =
                $"GymHub - Your Order Is Waiting for Collection ({referenceNumber})";

            message.Body = new TextPart("html")
            {
                Text = BuildCollectionReminderEmail(
                    toName,
                    referenceNumber,
                    "Collection reminder",
                    "Your GymHub store order is still waiting for collection.",
                    "Please collect your order from the GymHub store counter before the collection deadline. Remember to present the pickup QR code available under My Orders.",
                    "#D97706",
                    "#FFFBEB",
                    "#FDE68A")
            };

            Send(message);
        }


        // ============================================================
        // STORE ORDER FINAL COLLECTION REMINDER
        // Sent approximately 96 hours after the order becomes ready.
        // ============================================================
        public static void SendOrderFinalReminder(
            string toEmail,
            string toName,
            string referenceNumber)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                return;

            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(SenderName, SenderEmail));

            message.To.Add(
                new MailboxAddress(
                    string.IsNullOrWhiteSpace(toName)
                        ? "GymHub Member"
                        : toName,
                    toEmail));

            message.Subject =
                $"GymHub - Final Collection Reminder ({referenceNumber})";

            message.Body = new TextPart("html")
            {
                Text = BuildCollectionReminderEmail(
                    toName,
                    referenceNumber,
                    "Final collection reminder",
                    "Your collection deadline is approaching.",
                    "Please collect your order by the deadline shown under My Orders. If it is not collected in time, the reserved products will be released and the order will be sent for administrative resolution.",
                    "#DC2626",
                    "#FEF2F2",
                    "#FECACA")
            };

            Send(message);
        }


        // ============================================================
        // SHARED BRANDED COLLECTION REMINDER TEMPLATE
        // ============================================================
        private static string BuildCollectionReminderEmail(
            string toName,
            string referenceNumber,
            string heading,
            string bannerText,
            string instructions,
            string accentColour,
            string bannerBackground,
            string bannerBorder)
        {
            string safeName =
                System.Net.WebUtility.HtmlEncode(
                    string.IsNullOrWhiteSpace(toName)
                        ? "GymHub Member"
                        : toName);

            string safeReference =
                System.Net.WebUtility.HtmlEncode(
                    referenceNumber ?? "Not available");

            string safeHeading =
                System.Net.WebUtility.HtmlEncode(
                    heading ?? "Collection reminder");

            string safeBanner =
                System.Net.WebUtility.HtmlEncode(
                    bannerText ?? string.Empty);

            string safeInstructions =
                System.Net.WebUtility.HtmlEncode(
                    instructions ?? string.Empty);

            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <meta name='viewport'
          content='width=device-width, initial-scale=1' />

    <style>
        body {{
            margin: 0;
            padding: 0;
            background: #F1F5F9;
            font-family: Arial, Helvetica, sans-serif;
            color: #334155;
        }}

        .email-wrapper {{
            width: 100%;
            padding: 32px 12px;
            box-sizing: border-box;
        }}

        .email-container {{
            max-width: 620px;
            margin: 0 auto;
            background: #FFFFFF;
            border-radius: 16px;
            overflow: hidden;
            box-shadow: 0 10px 30px rgba(15, 23, 42, 0.08);
        }}

        .email-header {{
            background: #0F172A;
            padding: 30px 34px;
        }}

        .brand {{
            margin: 0;
            color: #FFFFFF;
            font-size: 27px;
            font-weight: 900;
            letter-spacing: 3px;
        }}

        .brand span {{
            color: #3B82F6;
        }}

        .email-body {{
            padding: 34px;
        }}

        .email-body h2 {{
            margin: 0 0 18px;
            color: #0F172A;
            font-size: 22px;
        }}

        .email-body p {{
            margin: 0 0 16px;
            font-size: 14px;
            line-height: 1.7;
        }}

        .notice {{
            margin: 22px 0;
            padding: 16px;
            background: {bannerBackground};
            border: 1px solid {bannerBorder};
            border-radius: 11px;
            color: {accentColour};
            font-size: 14px;
            font-weight: 700;
            text-align: center;
        }}

        .reference-label {{
            margin-bottom: 6px;
            color: #64748B;
            font-size: 10px;
            font-weight: 700;
            letter-spacing: 1px;
            text-align: center;
            text-transform: uppercase;
        }}

        .reference {{
            padding: 14px;
            background: #EFF6FF;
            border-radius: 10px;
            color: #1D4ED8;
            font-size: 19px;
            font-weight: 800;
            letter-spacing: 1px;
            text-align: center;
        }}

        .instructions {{
            margin-top: 22px;
            padding: 16px;
            background: #F8FAFC;
            border: 1px solid #E2E8F0;
            border-radius: 10px;
        }}

        .email-footer {{
            padding: 20px 34px;
            background: #F8FAFC;
            color: #94A3B8;
            font-size: 11px;
            line-height: 1.6;
            text-align: center;
        }}
    </style>
</head>

<body>
    <div class='email-wrapper'>
        <div class='email-container'>

            <div class='email-header'>
                <div class='brand'>
                    GYM<span>HUB</span>
                </div>
            </div>

            <div class='email-body'>
                <p>Hi <strong>{safeName}</strong>,</p>

                <h2>{safeHeading}</h2>

                <div class='notice'>
                    {safeBanner}
                </div>

                <div class='reference-label'>
                    Order reference
                </div>

                <div class='reference'>
                    {safeReference}
                </div>

                <div class='instructions'>
                    <p style='margin:0;'>
                        {safeInstructions}
                    </p>
                </div>

                <p style='margin-top:22px;'>
                    If you have already collected your order,
                    no further action is required.
                </p>
            </div>

            <div class='email-footer'>
                &copy; {DateTime.Now.Year} GymHub.
                This is an automated service message.
            </div>

        </div>
    </div>
</body>
</html>";
        }
    }
}
