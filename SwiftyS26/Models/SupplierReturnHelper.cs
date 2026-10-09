using System;
using System.Linq;

namespace SwiftyS26.Models
{
    public static class SupplierReturnHelper
    {
        public static readonly string[] ValidReasons = new[] { "Damaged", "Missing" };
        public static readonly string[] ValidResolutions = new[] { "Credited", "Replaced", "WrittenOff" };

        public static string GenerateReferenceNumber(GymHubDB db)
        {
            var rng = new Random();
            string refNumber;
            int attempts = 0;
            do
            {
                refNumber = "SR-" + DateTime.Now.Year + "-" + rng.Next(1000, 9999);
                attempts++;
            }
            while (attempts < 15 && db.SupplierReturns.Any(r => r.ReferenceNumber == refNumber));

            return refNumber;
        }
    }
}