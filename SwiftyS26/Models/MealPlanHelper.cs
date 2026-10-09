using System;
using System.Collections.Generic;
using System.Linq;

namespace SwiftyS26.Models
{
    public static class MealPlanHelper
    {
        public static readonly string[] DietaryPreferences = new[] { "None", "Vegetarian", "Vegan", "Halal", "GlutenFree" };
        public static readonly string[] Allergens = new[] { "Nuts", "Dairy", "Shellfish", "Gluten" };

        // NOTE: BMR/TDEE calculation removed from here — FitnessProfile
        // already stores BMR/TDEE (computed by the UC09 flow), so the
        // meal planner reads profile.BMR / profile.TDEE directly
        // instead of recalculating with a possibly different formula.

        // Reuses the same goal categories as MealController.GetNutritionTargets
        // so a member's plan targets stay consistent with their daily
        // meal-scoring targets elsewhere in the app.
        public static MealTotals CalculateTargets(decimal weightKg, decimal tdee, string goalType)
        {
            decimal calories = tdee;
            string goal = (goalType ?? "").ToLower();

            if (goal.Contains("weight loss")) calories = tdee - 500m;
            else if (goal.Contains("muscle")) calories = tdee + 350m;
            else if (goal.Contains("endurance")) calories = tdee + 200m;

            decimal protein = goal.Contains("muscle") ? weightKg * 2.2m : weightKg * 1.8m;
            decimal fat = (calories * 0.25m) / 9m;
            decimal carbs = (calories - (protein * 4m) - (fat * 9m)) / 4m;

            return new MealTotals
            {
                Calories = Math.Round(calories),
                Protein = Math.Round(protein),
                Carbs = Math.Round(Math.Max(carbs, 0)),
                Fat = Math.Round(fat)
            };
        }

        // ============================================================
        // GENERATE 7-DAY PLAN — unchanged from before
        // ============================================================
        public static List<MealPlanItem> GeneratePlan(List<Recipe> library, string dietaryPreference, string allergiesCsv, int dailyCalorieTarget)
        {
            var allergyList = string.IsNullOrWhiteSpace(allergiesCsv)
                ? new List<string>()
                : allergiesCsv.Split(',').Select(a => a.Trim()).Where(a => a.Length > 0).ToList();

            var eligible = library.Where(r =>
                r.IsActive &&
                (string.IsNullOrEmpty(dietaryPreference) || dietaryPreference == "None" ||
                 (r.DietaryTags ?? "").Split(',').Select(t => t.Trim()).Contains(dietaryPreference)) &&
                !allergyList.Any(allergen => (r.Allergens ?? "").Split(',').Select(a => a.Trim()).Contains(allergen))
            ).ToList();

            var mealSlots = new (string MealTime, decimal Share)[]
            {
                ("Breakfast", 0.25m), ("Lunch", 0.30m), ("Dinner", 0.30m), ("Snack", 0.15m)
            };

            var rng = new Random();
            var result = new List<MealPlanItem>();

            for (int day = 1; day <= 7; day++)
            {
                foreach (var slot in mealSlots)
                {
                    int slotTarget = (int)(dailyCalorieTarget * slot.Share);

                    var candidates = eligible
                        .Where(r => r.MealTime == slot.MealTime)
                        .OrderBy(r => Math.Abs(r.Calories - slotTarget))
                        .Take(4)
                        .ToList();

                    if (!candidates.Any())
                        continue;

                    var chosen = candidates[rng.Next(candidates.Count)];

                    result.Add(new MealPlanItem
                    {
                        DayNumber = day,
                        RecipeId = chosen.RecipeId,
                        MealTime = slot.MealTime
                    });
                }
            }

            return result;
        }
    }
}