using QRCoder;
using SwiftyS26.Services;
using System;
using System.Data.Entity;
using System.Linq;
using System.Security.Cryptography;

namespace SwiftyS26.Models
{
    public static class StoreOrderHelper
    {
        private const string PickupQrPrefix = "GYMHUB ORDER";
        private const string ReadyQrPrefix = "GYMHUB READY";

        public static string ExtractQrField(string qrData, string key)
        {
            if (string.IsNullOrWhiteSpace(qrData) ||
                string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var parts = qrData.Split('|');

            foreach (var part in parts)
            {
                var trimmed = part.Trim();

                if (trimmed.StartsWith(
                    key,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed.Substring(key.Length).Trim();
                }
            }

            return null;
        }

        public static string GenerateOrderQrContent(
            string referenceNumber,
            string memberNumber)
        {
            return string.Format(
                "{0} | Ref: {1} | MemberNumber: {2}",
                PickupQrPrefix,
                referenceNumber,
                memberNumber);
        }

        public static string GenerateReadyQrContent(
            string referenceNumber,
            string token)
        {
            if (string.IsNullOrWhiteSpace(referenceNumber))
            {
                throw new ArgumentException(
                    "An order reference is required.",
                    "referenceNumber");
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException(
                    "A readiness QR token is required.",
                    "token");
            }

            return string.Format(
                "{0} | Ref: {1} | Token: {2}",
                ReadyQrPrefix,
                referenceNumber,
                token);
        }

        public static string GenerateSecureReadyToken()
        {
            var bytes = new byte[32];

            using (var generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(bytes);
            }

            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }

        public static string GenerateOrderQrBase64(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException(
                    "QR content cannot be empty.",
                    "content");
            }

            using (var qrGenerator = new QRCodeGenerator())
            using (var qrCodeData = qrGenerator.CreateQrCode(
                content,
                QRCodeGenerator.ECCLevel.Q))
            using (var qrCode = new PngByteQRCode(qrCodeData))
            {
                byte[] qrBytes = qrCode.GetGraphic(20);
                return Convert.ToBase64String(qrBytes);
            }
        }

        public static string GenerateReadyQrBase64(StoreOrder order)
        {
            if (order == null)
            {
                throw new ArgumentNullException("order");
            }

            if (string.IsNullOrWhiteSpace(order.ReadyQrToken))
            {
                throw new InvalidOperationException(
                    "The order does not have a readiness QR token.");
            }

            string content = GenerateReadyQrContent(
                order.ReferenceNumber,
                order.ReadyQrToken);

            return GenerateOrderQrBase64(content);
        }

        public static bool IsReadyQrContent(string qrData)
        {
            return !string.IsNullOrWhiteSpace(qrData) &&
                   qrData.Trim().StartsWith(
                       ReadyQrPrefix,
                       StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPickupQrContent(string qrData)
        {
            return !string.IsNullOrWhiteSpace(qrData) &&
                   qrData.Trim().StartsWith(
                       PickupQrPrefix,
                       StringComparison.OrdinalIgnoreCase);
        }

        public static void CreateReadyQr(StoreOrder order)
        {
            if (order == null)
            {
                throw new ArgumentNullException("order");
            }

            if (order.Status != "Paid" &&
                order.Status != "Preparing")
            {
                throw new InvalidOperationException(
                    "Only a paid or preparing order can receive a readiness QR code.");
            }

            order.ReadyQrToken = GenerateSecureReadyToken();
            order.ReadyQrGeneratedAt = DateTime.Now;
            order.ReadyQrUsedAt = null;
        }

        public static void InvalidateReadyQr(StoreOrder order)
        {
            if (order == null)
            {
                throw new ArgumentNullException("order");
            }

            order.ReadyQrUsedAt = DateTime.Now;
            order.ReadyQrToken = null;
        }

        public static readonly string[] ValidStatuses =
        {
            "PendingPayment",
            "Paid",
            "Preparing",
            "ReadyForPickup",
            "Collected",
            "UncollectedPendingReview",
            "UncollectedRefundPending",
            "UncollectedRefunded",
            "UncollectedResolved",
            "Cancelled",
            "Refunded"
        };

        public static void CheckUncollectedOrders(GymHubDB db)
        {
            if (db == null)
            {
                throw new ArgumentNullException("db");
            }

            new UncollectedOrderService(db).ProcessDueOrders();
        }
    }
}