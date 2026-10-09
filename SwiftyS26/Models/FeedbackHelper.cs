namespace SwiftyS26.Models
{
    public static class FeedbackHelper
    {
        public static readonly string[] ValidCategories =
        {
            "Trainer",
            "Equipment",
            "Facilities",
            "Classes"
        };

        public static bool IsValidCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return false;
            foreach (var c in ValidCategories)
                if (string.Equals(c, category, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        public static bool IsValidRating(int rating)
        {
            return rating >= 1 && rating <= 5;
        }
    }

    // ── NEW: add this class ──────────────────────────────────────────
    public class CategoryAverageStat
    {
        public string Category { get; set; }
        public decimal Average { get; set; }
        public int Count { get; set; }
    }
}