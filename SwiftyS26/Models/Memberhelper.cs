using QRCoder;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace SwiftyS26.Models
{
    public static class MemberHelper
    {
        // ============================================================
        // PLAN HELPERS
        // ============================================================
        public static decimal GetPlanFee(string plan)
        {
            switch (plan?.ToLower())
            {
                case "basic": return 199.00m;
                case "premium": return 699.00m;
                default: return 399.00m;
            }
        }

        public static string GetPlanDisplay(string plan)
        {
            switch (plan?.ToLower())
            {
                case "basic": return "Basic – R199/month";
                case "premium": return "Premium – R699/month";
                default: return "Standard – R399/month";
            }
        }

        // ============================================================
        // MEMBER NUMBER GENERATOR
        // Format: GH-XXXXXX
        // ============================================================
        public static string GenerateMemberNumber()
        {
            var random = new Random();
            int number = random.Next(100000, 999999);
            return $"GH-{number}";
        }

        // ============================================================
        // SA ID VALIDATION
        // ============================================================
        public static bool IsValidSaId(string idNumber)
        {
            if (string.IsNullOrWhiteSpace(idNumber)) return false;
            if (idNumber.Length != 13) return false;
            if (!idNumber.All(char.IsDigit)) return false;

            int sum = 0;
            bool alternate = false;
            for (int i = idNumber.Length - 1; i >= 0; i--)
            {
                int n = int.Parse(idNumber[i].ToString());
                if (alternate) { n *= 2; if (n > 9) n -= 9; }
                sum += n;
                alternate = !alternate;
            }
            return sum % 10 == 0;
        }

        public static DateTime? ExtractDobFromId(string idNumber)
        {
            if (!IsValidSaId(idNumber)) return null;
            try
            {
                string yy = idNumber.Substring(0, 2);
                string mm = idNumber.Substring(2, 2);
                string dd = idNumber.Substring(4, 2);
                int currentYear = DateTime.Now.Year % 100;
                string century = int.Parse(yy) <= currentYear ? "20" : "19";
                return DateTime.ParseExact(
                    $"{century}{yy}-{mm}-{dd}",
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture);
            }
            catch { return null; }
        }

        public static string ExtractGenderFromId(string idNumber)
        {
            if (!IsValidSaId(idNumber)) return null;
            int genderDigits = int.Parse(idNumber.Substring(6, 4));
            return genderDigits >= 5000 ? "Male" : "Female";
        }

        // ============================================================
        // AGE CALCULATOR
        // ============================================================
        public static int CalculateAge(DateTime dob)
        {
            var today = DateTime.Today;
            int age = today.Year - dob.Year;
            if (dob.Date > today.AddYears(-age)) age--;
            return age;
        }

        // ============================================================
        // OTP
        // ============================================================
        public static string GenerateOtp()
        {
            return new Random().Next(100000, 999999).ToString();
        }

        public static bool ValidateOtp(string storedOtp,
            DateTime? generatedAt, string enteredOtp)
        {
            if (string.IsNullOrWhiteSpace(storedOtp)) return false;
            if (generatedAt == null) return false;
            if (storedOtp != enteredOtp) return false;
            if ((DateTime.Now - generatedAt.Value)
                .TotalMinutes > 10) return false;
            return true;
        }

        // ============================================================
        // EMAIL VALIDATION
        // ============================================================
        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            return Regex.IsMatch(email,
                @"^[^\s]+\.[^\s]+$",
                RegexOptions.IgnoreCase);
        }

        // ============================================================
        // QR CODE — SINGLE SOURCE OF TRUTH
        //
        // Format: GYMHUB MEMBER|ID:GH-XXXXXX|NAME:Owami Mashiya|PLAN:BASIC
        //
        // Rules:
        //  - Pipe-delimited, no spaces around pipes
        //  - Field keys uppercase with colon, no space after colon
        //  - PLAN always uppercase
        //  - This exact format is parsed by AdminController.VerifyQr
        //  - Never change this format without updating VerifyQr too
        // ============================================================

        /// <summary>
        /// Builds the canonical QR data string for a member.
        /// Used by GenerateQrCodeBytes, VerifyQr, and dashboard JS.
        /// </summary>
        public static string BuildQrContent(string memberNumber,
            string fullName, string plan, string email)
        {
            return $"GYMHUB MEMBER|ID:{memberNumber}|NAME:{fullName}" +
                   $"|PLAN:{plan.ToUpper()}|EMAIL:{email.ToLower()}";
        }

        /// <summary>
        /// Generates a QR code PNG as a byte array.
        /// ECCLevel.H = highest error correction — best for physical scanning.
        /// Pixel size 10 = 10px per module — good balance of size and scannability.
        /// </summary>
        public static byte[] GenerateQrCodeBytes(string memberNumber,
            string fullName, string plan, string email)
        {
            string content = BuildQrContent(memberNumber, fullName,
                                            plan, email);
            using (var qrGenerator = new QRCodeGenerator())
            using (var qrData = qrGenerator.CreateQrCode(content,
                                         QRCodeGenerator.ECCLevel.H))
            using (var qrCode = new PngByteQRCode(qrData))
            {
                return qrCode.GetGraphic(10);
            }
        }

        /// <summary>
        /// Generates a QR code and returns it as a base64 PNG string
        /// ready to embed in an img src attribute.
        /// </summary>
        public static string GenerateQrBase64(string memberNumber,
            string fullName, string plan, string email)
        {
            byte[] bytes = GenerateQrCodeBytes(memberNumber,
                                               fullName, plan, email);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Parses a QR data string back into its components.
        /// Returns null if the format is invalid.
        /// </summary>
        public static QrParseResult ParseQrContent(string qrData)
        {
            if (string.IsNullOrWhiteSpace(qrData)) return null;

            var parts = qrData.Split('|');
            if (parts.Length < 2) return null;
            if (!parts[0].Equals("GYMHUB MEMBER",
                    StringComparison.OrdinalIgnoreCase)) return null;

            var result = new QrParseResult();

            foreach (var part in parts)
            {
                var trimmed = part.Trim();

                if (trimmed.StartsWith("ID:",
                        StringComparison.OrdinalIgnoreCase))
                    result.MemberNumber = trimmed.Substring(3).Trim();

                else if (trimmed.StartsWith("NAME:",
                        StringComparison.OrdinalIgnoreCase))
                    result.FullName = trimmed.Substring(5).Trim();

                else if (trimmed.StartsWith("PLAN:",
                        StringComparison.OrdinalIgnoreCase))
                    result.Plan = trimmed.Substring(5).Trim();

                else if (trimmed.StartsWith("EMAIL:",
                        StringComparison.OrdinalIgnoreCase))
                    result.Email = trimmed.Substring(6).Trim();
            }

            return string.IsNullOrWhiteSpace(result.MemberNumber)
                ? null : result;
        }
    }

    /// <summary>Result object from parsing a QR code string.</summary>
    public class QrParseResult
    {
        public string MemberNumber { get; set; }
        public string FullName { get; set; }
        public string Plan { get; set; }
        public string Email { get; set; }
    }
}