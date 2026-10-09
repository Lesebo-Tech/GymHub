using QRCoder;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace SwiftyS26.Models
{
    public static class ProcurementQrHelper
    {
        public const string PurchaseOrderPrefix = "GYMHUB-PO";
        public const string ProductPrefix = "GYMHUB-PRODUCT";

        public static string CreatePurchaseOrderContent(string reference, string token)
        {
            return PurchaseOrderPrefix + "|Ref:" + reference + "|Token:" + token;
        }

        public static string CreateProductContent(string sku, string token)
        {
            return ProductPrefix + "|SKU:" + sku + "|Token:" + token;
        }

        public static string GenerateBase64(string content)
        {
            using (var generator = new QRCodeGenerator())
            using (var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q))
            using (var code = new QRCode(data))
            using (Bitmap bitmap = code.GetGraphic(12))
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        public static string ExtractField(string qrData, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(qrData)) return null;
            var part = qrData.Split('|').FirstOrDefault(x =>
                x.StartsWith(fieldName + ":", StringComparison.OrdinalIgnoreCase));
            return part == null ? null : part.Substring(fieldName.Length + 1).Trim();
        }

        public static bool IsPurchaseOrderQr(string qrData)
        {
            return !string.IsNullOrWhiteSpace(qrData) &&
                qrData.StartsWith(PurchaseOrderPrefix + "|", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsProductQr(string qrData)
        {
            return !string.IsNullOrWhiteSpace(qrData) &&
                qrData.StartsWith(ProductPrefix + "|", StringComparison.OrdinalIgnoreCase);
        }
    }
}
