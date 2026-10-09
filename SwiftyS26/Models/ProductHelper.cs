using System.Linq;

namespace SwiftyS26.Models
{
    public static class ProductHelper
    {
        public static readonly string[] Categories = new[]
        {
            "Supplements", "Accessories", "Clothing", "Other"
        };

        public static bool IsValidCategory(string category) => Categories.Contains(category);
    }
}