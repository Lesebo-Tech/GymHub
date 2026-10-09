using QRCoder;
using System;
using System.Linq;

namespace SwiftyS26.Models
{
    // ── ADD to StoreOrderHelper.cs, or a new file ─────────────
        public class TopProductViewModel
        {
            public Product Product { get; set; }
            public int TotalQuantity { get; set; }
            public decimal TotalEarning { get; set; }
        }

        public class StatusBreakdownViewModel
        {
            public string Status { get; set; }
            public int Count { get; set; }
        }
    public class CategoryRevenueViewModel
    {
        public string Category { get; set; }
        public decimal Revenue { get; set; }
    }

    public static class StoreHelper
    {
        // Open decision from the planning doc — defaulting to 10%.
        // Confirm with your team before final submission.
        public const decimal PremiumDiscountPercent = 10m;

        public static bool IsPremiumMember(MemberForm member) =>
            member != null && member.Plan != null &&
            member.Plan.Trim().Equals("Premium", StringComparison.OrdinalIgnoreCase);

        public static decimal CalculateDiscount(decimal subtotal, MemberForm member) =>
            IsPremiumMember(member) ? Math.Round(subtotal * (PremiumDiscountPercent / 100m), 2) : 0m;

        public static string GenerateReferenceNumber(GymHubDB db)
        {
            var rng = new Random();
            string refNumber;
            int attempts = 0;
            do
            {
                refNumber = "ORD-" + DateTime.Now.Year + "-" + rng.Next(1000, 9999);
                attempts++;
            }
            while (attempts < 15 && db.StoreOrders.Any(o => o.ReferenceNumber == refNumber));

            return refNumber;
        }
        public static string GenerateOrderQrBase64(string content)
        {
            using (var qrGenerator = new QRCodeGenerator())
            using (var qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q))
            using (var qrCode = new PngByteQRCode(qrCodeData))
            {
                byte[] qrBytes = qrCode.GetGraphic(20);
                return Convert.ToBase64String(qrBytes);
            }
        }

    }
}