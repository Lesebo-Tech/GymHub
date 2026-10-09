using System;

namespace SwiftyS26.Models
{
    public static class PromotionHelper
    {
        // ============================================================
        // THE ONE HARD RULE, correctly flagged from day one: a
        // promotion's end date must be strictly BEFORE the product's
        // expiry date. Discounting something you can't legally/safely
        // still sell by the time the promotion ends is meaningless —
        // this is checked server-side regardless of who's creating it.
        // ============================================================
        public static string ValidatePromotion(Product product, DateTime startDate, DateTime endDate, decimal discountPercent)
        {
            if (product == null)
                return "Product not found.";

            if (endDate <= startDate)
                return "End date must be after the start date.";

            if (discountPercent <= 0 || discountPercent > 90)
                return "Discount must be between 1% and 90%.";

            if (product.ExpiryDate.HasValue && endDate.Date >= product.ExpiryDate.Value.Date)
                return $"Promotion end date must be before this product's expiry date ({product.ExpiryDate.Value:dd MMM yyyy}).";

            return null; // valid
        }
    }
}