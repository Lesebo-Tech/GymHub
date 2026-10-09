using System;
using System.Linq;

namespace SwiftyS26.Models
{
    public static class PurchaseOrderHelper
    {
        // Open decision from the planning doc, confirmed with your team
        // at R2,000. Below this, StoreStaff can send directly — routine,
        // low-stakes, high-frequency. Above it, Admin must approve first —
        // same "spending authority ceiling" pattern used in real
        // procurement, and the same severity-based judgment principle
        // already used for locker fault triage.
        public const decimal ApprovalThreshold = 2000m;

        public static readonly string[] ValidStatuses = new[]
        {
            "Draft", "PendingAdminApproval", "Pending", "Confirmed",
            "Delivered", "Inspected", "Completed", "Rejected"
        };

        public static string GenerateReferenceNumber(GymHubDB db)
        {
            var rng = new Random();
            string refNumber;
            int attempts = 0;
            do
            {
                refNumber = "PO-" + DateTime.Now.Year + "-" + rng.Next(1000, 9999);
                attempts++;
            }
            while (attempts < 15 && db.PurchaseOrders.Any(p => p.ReferenceNumber == refNumber));

            return refNumber;
        }
    }
}