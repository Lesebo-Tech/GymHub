using System;

namespace SwiftyS26.Models
{
    public static class ComplaintHelper
    {
        public static readonly string[] ValidCategories =
        {
            "Facility & Cleanliness",
            "Staff Conduct",
            "Billing & Payments",
            "Equipment",
            "Class / Trainer",
            "App / Website",
            "Other"
        };

        public static readonly string[] ValidStatuses =
        {
            "Submitted",
            "Under Investigation",
            "Resolved",
            "Closed"
        };

        public static bool IsValidCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return false;
            foreach (var c in ValidCategories)
                if (string.Equals(c, category, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        public static bool IsValidStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            foreach (var s in ValidStatuses)
                if (string.Equals(s, status, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        // e.g. CMP-20260730-4821
        public static string GenerateReferenceNumber()
        {
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            string randomPart = new Random(Guid.NewGuid().GetHashCode()).Next(1000, 9999).ToString();
            return $"CMP-{datePart}-{randomPart}";
        }

        // Allowed evidence file extensions for uploads
        public static readonly string[] AllowedEvidenceExtensions =
        {
            ".jpg", ".jpeg", ".png", ".pdf", ".docx"
        };

        public static bool IsAllowedEvidenceExtension(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;
            string ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            foreach (var allowed in AllowedEvidenceExtensions)
                if (ext == allowed) return true;
            return false;
        }
    }
}