using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;


namespace SwiftyS26.Models
{
    public static class FaultCategoryHelper
    {
        public static readonly List<string> ValidCategories = new List<string>
        {
            "Mechanical",
            "Electrical / Power",
            "Display / Electronics",
            "Upholstery / Padding",
            "Safety Mechanism",
            "Hygiene / Cleanliness",
            "Other"
        };

        public static bool IsValidCategory(string category)
        {
            return !string.IsNullOrWhiteSpace(category) && ValidCategories.Contains(category);
        }
    }
}