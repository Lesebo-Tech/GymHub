using Newtonsoft.Json;
using SwiftyS26.Models;
using System;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace SwiftyS26.Controllers
{
    public class PaymentController : Controller
    {
        private GymHubDB db = new GymHubDB();
        private static readonly HttpClient http = new HttpClient();

        // ============================================================
        // INITIALIZE PAYMENT — POST: /Payment/Initialize
        // ============================================================
        [HttpPost]
        public ActionResult Initialize()
        {
            var pendingMember = Session["PendingMember"] as MemberForm;
            if (pendingMember == null)
                return Json(new
                {
                    success = false,
                    message = "Session expired. " +
                                  "Please start registration again."
                });

            string reference =
                $"GH-{DateTime.Now:yyyyMMddHHmmss}-" +
                Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();

            Session["PaystackRef"] = reference;

            string publicKey = ConfigurationManager
                .AppSettings["Paystack:PublicKey"];
            decimal amount = pendingMember.MonthlyFee + 150m;

            return Json(new
            {
                success = true,
                reference = reference,
                publicKey = publicKey,
                email = pendingMember.Email,
                amount = (int)(amount * 100), // kobo/cents
                currency = "ZAR",
                firstName = pendingMember.FirstName,
                lastName = pendingMember.LastName
            });
        }

        // ============================================================
        // VERIFY PAYMENT — POST: /Payment/Verify
        // Verifies with Paystack, saves member + payment + QR to DB
        // ============================================================
        [HttpPost]
        public async Task<ActionResult> Verify(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
                return Json(new
                {
                    success = false,
                    message = "No reference provided."
                });

            var sessionRef = Session["PaystackRef"] as string;
            if (sessionRef == null || sessionRef != reference)
                return Json(new
                {
                    success = false,
                    message = "Reference mismatch."
                });

            string secretKey = ConfigurationManager
                .AppSettings["Paystack:SecretKey"];

            try
            {
                // ── Verify with Paystack ──
                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"https://api.paystack.co/transaction/verify/{reference}");
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", secretKey);

                var response = await http.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine(
                    $"[PAYSTACK] Verify response: {json}");

                dynamic result = JsonConvert.DeserializeObject(json);

                if (result.status != true ||
                    result.data.status != "success")
                    return Json(new
                    {
                        success = false,
                        message = "Payment was not successful. " +
                                  "Please try again."
                    });

                // ── Get pending member from session ──
                var pendingMember = Session["PendingMember"] as MemberForm;
                if (pendingMember == null)
                    return Json(new
                    {
                        success = false,
                        message = "Session expired."
                    });

                // ── Complete registration ──
                pendingMember.IpAddress = Request.UserHostAddress;
                pendingMember.RegisteredAt = DateTime.Now;
                pendingMember.MemberNumber =
                    MemberHelper.GenerateMemberNumber();
                pendingMember.Status = "Active";
                pendingMember.OtpCode = null;
                pendingMember.OtpGeneratedAt = null;

                // ── Generate QR code and save to DB ──
                // This is the ONLY place QR is ever generated.
                // All display (dashboard, email, standalone page)
                // reads this stored value — never regenerates.
                pendingMember.QrCodeBase64 =
                    MemberHelper.GenerateQrBase64(
                        pendingMember.MemberNumber,
                        pendingMember.FullName,
                        pendingMember.Plan,
                        pendingMember.Email);

                System.Diagnostics.Debug.WriteLine(
                    $"[QR] Generated for {pendingMember.MemberNumber}");

                db.Members.Add(pendingMember);
                await db.SaveChangesAsync();

                // ── Save payment record ──
                var payment = new Payment
                {
                    MemberId = pendingMember.MemberId,
                    Reference = reference,
                    TransactionId = result.data.id.ToString(),
                    PaymentDate = DateTime.Now,
                    Description = $"{pendingMember.Plan} membership " +
                                    $"+ once-off joining fee",
                    Method = result.data.channel.ToString(),
                    Amount = pendingMember.MonthlyFee + 150m,
                    Status = "Success"
                };

                db.Payments.Add(payment);
                await db.SaveChangesAsync();

                // ── Send welcome email with QR ──
                try
                {
                    EmailService.SendWelcome(
                        pendingMember.Email,
                        pendingMember.FullName,
                        pendingMember.MemberNumber,
                        pendingMember.Plan,
                        pendingMember.MonthlyFee);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[EMAIL ERROR] {ex.Message}");
                }

                Session.Remove("PendingMember");
                Session.Remove("PaystackRef");

                return Json(new
                {
                    success = true,
                    memberNumber = pendingMember.MemberNumber
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PAYSTACK ERROR] {ex.Message}");
                return Json(new
                {
                    success = false,
                    message = "Verification failed. Please contact support."
                });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}