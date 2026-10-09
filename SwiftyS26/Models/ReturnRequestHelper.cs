using System;
using System.Data.Entity;
using System.Linq;
using System.Security.Cryptography;
using System.Text;


namespace SwiftyS26.Models
{
    public static class ReturnRequestHelper
    {
        // ============================================================
        // VALID RETURN REASONS
        // ============================================================

        public static readonly string[] ValidReasons =
        {
            "Defective",
            "WrongItem",
            "ChangedMind",
            "Other"
        };

        // ============================================================
        // REQUESTED RESOLUTIONS
        // ============================================================

        public static readonly string[] ValidRequestedResolutions =
        {
            "Refund",
            "Exchange"
        };

        public static readonly string[] ValidExchangeTypes =
        {
            "SameProduct",
            "DifferentProduct"
        };

        // ============================================================
        // WORKFLOW STATUSES
        // ============================================================

        public static readonly string[] ValidStatuses =
 {
            "Pending",
            "StaffReviewed",
            "PendingAdminApproval",
            "Approved",
            "Rejected",
            "RefundProcessing",
            "Refunded",
            "ExchangeApproved",
            "AwaitingExchangePayment",
            "ReadyForExchange",
            "ExchangeCompleted",
            "ExchangeExpired",
            "Cancelled"
};

        // Requests that have not reached a final outcome.
        public static readonly string[] OpenStatuses =
{
            "Pending",
            "StaffReviewed",
            "PendingAdminApproval",
            "RefundProcessing",
            "ExchangeApproved",
            "AwaitingExchangePayment",
            "ReadyForExchange"
};

        public static readonly string[] ClosedStatuses =
{
            "Approved",
            "Rejected",
            "Refunded",
            "ExchangeCompleted",
            "ExchangeExpired",
            "Cancelled"
        };

        // ============================================================
        // STAFF INSPECTION OPTIONS
        // ============================================================

        public static readonly string[] ValidItemConditions =
        {
            "Unopened",
            "LikeNew",
            "Used",
            "Damaged",
            "SeverelyDamaged"
        };

        public static readonly string[] ValidPackagingConditions =
        {
            "OriginalPackaging",
            "OpenedPackaging",
            "Missing",
            "Damaged",
            "NotApplicable"
        };

        public static readonly string[] ValidFaultVerifications =
        {
            "Verified",
            "PartiallyVerified",
            "NotVerified"
        };

        public static readonly string[] ValidFaultCauses =
        {
            "ManufacturingDefect",
            "ProductFailure",
            "CustomerDamage",
            "NormalWear",
            "WrongItem",
            "UnableToDetermine",
            "Other"
        };

        public static readonly string[] ValidStaffRecommendations =
        {
            "Approve",
            "Reject",
            "Escalate"
        };

        public static readonly string[] ValidItemDispositions =
        {
            "Restock",
            "Quarantine",
            "ReturnToSupplier",
            "Dispose",
            "AdminDecision"
        };

        // ============================================================
        // VALIDATION HELPERS
        // ============================================================

        public static bool IsValidReason(string reason)
        {
            return !string.IsNullOrWhiteSpace(reason) &&
                   ValidReasons.Contains(reason);
        }

        public static bool IsValidRequestedResolution(string resolution)
        {
            return !string.IsNullOrWhiteSpace(resolution) &&
                   ValidRequestedResolutions.Contains(resolution);
        }

        public static bool IsValidExchangeType(string exchangeType)
        {
            return !string.IsNullOrWhiteSpace(exchangeType) &&
                   ValidExchangeTypes.Contains(exchangeType);
        }

        public static bool IsValidStatus(string status)
        {
            return !string.IsNullOrWhiteSpace(status) &&
                   ValidStatuses.Contains(status);
        }

        public static bool IsOpenStatus(string status)
        {
            return !string.IsNullOrWhiteSpace(status) &&
                   OpenStatuses.Contains(status);
        }

        public static bool IsValidItemCondition(string condition)
        {
            return !string.IsNullOrWhiteSpace(condition) &&
                   ValidItemConditions.Contains(condition);
        }

        public static bool IsValidPackagingCondition(string condition)
        {
            return !string.IsNullOrWhiteSpace(condition) &&
                   ValidPackagingConditions.Contains(condition);
        }

        public static bool IsValidFaultVerification(string verification)
        {
            return !string.IsNullOrWhiteSpace(verification) &&
                   ValidFaultVerifications.Contains(verification);
        }

        public static bool IsValidFaultCause(string faultCause)
        {
            return !string.IsNullOrWhiteSpace(faultCause) &&
                   ValidFaultCauses.Contains(faultCause);
        }

        public static bool IsValidStaffRecommendation(
            string recommendation)
        {
            return !string.IsNullOrWhiteSpace(recommendation) &&
                   ValidStaffRecommendations.Contains(recommendation);
        }

        public static bool IsValidItemDisposition(string disposition)
        {
            return !string.IsNullOrWhiteSpace(disposition) &&
                   ValidItemDispositions.Contains(disposition);
        }

        // ============================================================
        // REFERENCE NUMBER
        // ============================================================

        public static string GenerateReferenceNumber(GymHubDB db)
        {
            var random = new Random();
            string referenceNumber;
            int attempts = 0;

            do
            {
                referenceNumber =
                    "RET-" +
                    DateTime.Now.Year +
                    "-" +
                    random.Next(1000, 9999);

                attempts++;
            }
            while (
                attempts < 15 &&
                db.ReturnRequests.Any(
                    r => r.ReferenceNumber == referenceNumber));

            return referenceNumber;
        }

        // ============================================================
        // PRORATED RETURN CREDIT / REFUND CALCULATION
        // ============================================================

        public static decimal CalculateRefundAmount(
            StoreOrder order,
            StoreOrderItem item,
            int returnQuantity)
        {
            if (order == null ||
                item == null ||
                order.Subtotal <= 0 ||
                returnQuantity <= 0)
            {
                return 0m;
            }

            decimal returnedItemAmount =
                item.UnitPriceAtPurchase * returnQuantity;

            decimal proportion =
                returnedItemAmount / order.Subtotal;

            decimal discountShare =
                order.DiscountAmount * proportion;

            decimal refund =
                returnedItemAmount - discountShare;

            return Math.Round(
                Math.Max(refund, 0m),
                2,
                MidpointRounding.AwayFromZero);
        }

        // ============================================================
        // EXCHANGE FINANCIAL CALCULATIONS
        // ============================================================

        public static decimal CalculateExchangeTotal(
            decimal replacementUnitPrice,
            int exchangeQuantity)
        {
            if (replacementUnitPrice < 0 || exchangeQuantity < 1)
                return 0m;

            return Math.Round(
                replacementUnitPrice * exchangeQuantity,
                2,
                MidpointRounding.AwayFromZero);
        }

        // Positive result:
        // The member must pay GymHub.
        //
        // Negative result:
        // GymHub owes the member a partial refund.
        //
        // Zero:
        // Equal-value exchange.
        public static decimal CalculateExchangePriceDifference(
            decimal returnCredit,
            decimal exchangeTotal)
        {
            return Math.Round(
                exchangeTotal - returnCredit,
                2,
                MidpointRounding.AwayFromZero);
        }
        // ============================================================
        // SECURE RETURN QR
        // ============================================================

        // Generates a cryptographically secure 256-bit token.
        // URL-safe Base64 avoids characters that are inconvenient
        // when scanning, logging or transporting QR content.
        public static string GenerateSecureQrToken()
        {
            byte[] tokenBytes = new byte[32];

            using (var generator =
                RandomNumberGenerator.Create())
            {
                generator.GetBytes(tokenBytes);
            }

            return Convert
                .ToBase64String(tokenBytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }


        // Stores only this SHA-256 hash in the database.
        // The original token remains inside the generated QR.
        public static string HashQrToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            using (var sha256 = SHA256.Create())
            {
                byte[] tokenBytes =
                    Encoding.UTF8.GetBytes(token);

                byte[] hashBytes =
                    sha256.ComputeHash(tokenBytes);

                var result = new StringBuilder(
                    hashBytes.Length * 2);

                foreach (byte value in hashBytes)
                {
                    result.Append(
                        value.ToString("x2"));
                }

                return result.ToString();
            }
        }


        // Creates the text encoded inside the QR.
        // Do not include member names, email addresses or payment data.
        public static string GenerateReturnQrContent(
            string orderReference,
            int returnRequestId,
            string rawToken)
        {
            if (string.IsNullOrWhiteSpace(orderReference))
            {
                throw new ArgumentException(
                    "Order reference is required.",
                    nameof(orderReference));
            }

            if (returnRequestId < 1)
            {
                throw new ArgumentException(
                    "Return request ID is invalid.",
                    nameof(returnRequestId));
            }

            if (string.IsNullOrWhiteSpace(rawToken))
            {
                throw new ArgumentException(
                    "QR token is required.",
                    nameof(rawToken));
            }

            return
                $"GYMHUB-RETURN" +
                $"|Order:{orderReference.Trim()}" +
                $"|ReturnId:{returnRequestId}" +
                $"|Token:{rawToken}";
        }


        // Reuses the QRCoder functionality that already exists in
        // StoreOrderHelper.
        public static string GenerateReturnQrBase64(
            string qrContent)
        {
            if (string.IsNullOrWhiteSpace(qrContent))
            {
                throw new ArgumentException(
                    "QR content is required.",
                    nameof(qrContent));
            }

            return StoreOrderHelper
                .GenerateOrderQrBase64(qrContent);
        }


        // Extracts a value from QR text such as:
        //
        // GYMHUB-RETURN|Order:ORD-2026-5556|ReturnId:42|Token:abc
        public static string ExtractReturnQrField(
            string qrData,
            string key)
        {
            if (string.IsNullOrWhiteSpace(qrData) ||
                string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            string[] parts = qrData.Split('|');

            foreach (string part in parts)
            {
                string trimmed =
                    (part ?? string.Empty).Trim();

                if (trimmed.StartsWith(
                    key,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed
                        .Substring(key.Length)
                        .Trim();
                }
            }

            return null;
        }


        // Basic format check before database access.
        public static bool IsReturnQrFormat(string qrData)
        {
            if (string.IsNullOrWhiteSpace(qrData))
                return false;

            // Prevent unexpectedly large scanner input.
            if (qrData.Length > 1000)
                return false;

            string[] parts = qrData.Split('|');

            if (parts.Length < 4)
                return false;

            if (!string.Equals(
                parts[0].Trim(),
                "GYMHUB-RETURN",
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string orderReference =
                ExtractReturnQrField(
                    qrData,
                    "Order:");

            string returnId =
                ExtractReturnQrField(
                    qrData,
                    "ReturnId:");

            string token =
                ExtractReturnQrField(
                    qrData,
                    "Token:");

            int parsedReturnId;

            return
                !string.IsNullOrWhiteSpace(orderReference) &&
                int.TryParse(returnId, out parsedReturnId) &&
                parsedReturnId > 0 &&
                !string.IsNullOrWhiteSpace(token);
        }


        // Compares a scanned raw token with the database hash.
        // The raw token is hashed before comparison.
        public static bool VerifyReturnQrToken(
            string scannedRawToken,
            string storedTokenHash)
        {
            if (string.IsNullOrWhiteSpace(scannedRawToken) ||
                string.IsNullOrWhiteSpace(storedTokenHash))
            {
                return false;
            }

            string scannedHash =
                HashQrToken(scannedRawToken);

            if (string.IsNullOrWhiteSpace(scannedHash) ||
                scannedHash.Length != storedTokenHash.Length)
            {
                return false;
            }

            // Constant-time comparison helps avoid leaking information
            // about the stored hash through comparison timing.
            int difference = 0;

            for (int index = 0;
                 index < scannedHash.Length;
                 index++)
            {
                difference |=
                    scannedHash[index] ^
                    storedTokenHash[index];
            }

            return difference == 0;
        }

        public static decimal CalculateAdditionalPayment(
            decimal exchangePriceDifference)
        {
            return exchangePriceDifference > 0
                ? exchangePriceDifference
                : 0m;
        }

        public static decimal CalculateExchangeRefund(
            decimal exchangePriceDifference)
        {
            return exchangePriceDifference < 0
                ? Math.Abs(exchangePriceDifference)
                : 0m;
        }
        // ============================================================
        // RELEASE EXPIRED EXCHANGE RESERVATIONS
        //
        // Runs lazily when the member's My Orders page or the staff
        // Returns dashboard opens.
        //
        // Replacement inventory was deducted when Admin approved the
        // exchange. If the member does not complete it within the
        // reservation period, the inventory must be restored.
        // ============================================================
        public static void ReleaseExpiredExchangeReservations(
            GymHubDB db)
        {
            if (db == null)
                return;

            DateTime now = DateTime.Now;

            var expiredRequests = db.ReturnRequests
                .Include(r => r.Member)
                .Include(r => r.ExchangeProduct)
                .Where(r =>
                    (r.Status == "AwaitingExchangePayment" ||
                     r.Status == "ReadyForExchange") &&
                    r.ExchangeReservationExpiresAt.HasValue &&
                    r.ExchangeReservationExpiresAt.Value <= now &&
                    r.ExchangeReservationReleasedAt == null &&
                    r.ExchangeQuantityReserved.HasValue &&
                    r.ExchangeQuantityReserved.Value > 0)
                .ToList();

            if (!expiredRequests.Any())
                return;

            var completedExpirations =
                new System.Collections.Generic.List<ReturnRequest>();

            using (var transaction =
                db.Database.BeginTransaction())
            {
                try
                {
                    foreach (var request in expiredRequests)
                    {
                        int reservedQuantity =
                            request.ExchangeQuantityReserved.Value;

                        if (request.ExchangeProduct != null)
                        {
                            request.ExchangeProduct.QuantityOnHand +=
                                reservedQuantity;

                            db.Entry(request.ExchangeProduct).State =
                                EntityState.Modified;
                        }

                        request.ExchangeQuantityReserved = 0;

                        request.ExchangeReservationReleasedAt = now;

                        request.ExchangeCancellationReason =
                            "The replacement reservation expired because " +
                            "the exchange was not completed within five days.";

                        request.Status = "ExchangeExpired";

                        db.Entry(request).State =
                            EntityState.Modified;

                        completedExpirations.Add(request);
                    }

                    db.SaveChanges();
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();

                    System.Diagnostics.Debug.WriteLine(
                        $"[EXCHANGE EXPIRY ERROR] {ex}");

                    return;
                }
            }

            // Send notifications after the inventory transaction has
            // completed successfully.
            foreach (var request in completedExpirations)
            {
                try
                {
                    string productName =
                        request.ExchangeProduct != null
                            ? request.ExchangeProduct.Name
                            : "the replacement product";

                    new NotificationService(db).Notify(
                        "Member",
                        request.MemberId,
                        "Exchange Reservation Expired",
                        $"Your exchange {request.ReferenceNumber} expired " +
                        $"because it was not completed within the reservation " +
                        $"period. The reserved {productName} stock has been " +
                        $"released. Please contact GymHub if you still need " +
                        $"assistance.",
                        "Warning",
                        "/Store/MyOrders");

                    new NotificationService(db).NotifyAdmin(
                        "Exchange Reservation Expired",
                        $"Exchange {request.ReferenceNumber} expired. " +
                        $"Its reserved replacement stock was released.",
                        "Warning",
                        "/StoreAdmin/Returns");

                    new NotificationService(db).NotifyAllStoreStaff(
                        "Exchange Reservation Expired",
                        $"Exchange {request.ReferenceNumber} expired. " +
                        $"Its reserved replacement stock was released.",
                        "Warning",
                        "/StoreAdmin/Returns");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[EXCHANGE EXPIRY NOTIFICATION ERROR] " +
                        $"{ex.Message}");
                }
            }
        }
    }
}