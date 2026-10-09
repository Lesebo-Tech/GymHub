using System.Collections.Generic;
using System.Linq;

namespace SwiftyS26.Models
{
    public static class MealMatchingService
    {
        // Minimum confidence before we bother trying to match a label at all
        private const double MinConfidence = 0.10;

        public static List<MealItemViewModel> MatchDetectedLabels(
            List<DetectedFoodLabel> detectedLabels, List<Food> allFoods)
        {
            var matchedItems = new List<MealItemViewModel>();

            var usableLabels = detectedLabels
                .Where(l => l.Confidence >= MinConfidence)
                .OrderByDescending(l => l.Confidence)
                .Take(3) // only consider the model's top 3 guesses
                .ToList();

            foreach (var detected in usableLabels)
            {
                var matchedFood = FindBestFoodMatch(detected.Label, allFoods);

                if (matchedFood != null)
                {
                    matchedItems.Add(BuildItemFromFood(matchedFood, detected));
                }
            }

            return matchedItems;
        }

        private static Food FindBestFoodMatch(string detectedLabel, List<Food> allFoods)
        {
            foreach (var food in allFoods)
            {
                if (string.IsNullOrWhiteSpace(food.VisionLabelAliases))
                    continue;

                var aliases = food.VisionLabelAliases
                    .Split(',')
                    .Select(a => a.Trim().ToLower());

                foreach (var alias in aliases)
                {
                    if (string.IsNullOrWhiteSpace(alias))
                        continue;

                    // Loose match: label contains alias OR alias contains label
                    // (handles "grilled chicken" matching alias "chicken" and vice versa)
                    if (detectedLabel.Contains(alias) || alias.Contains(detectedLabel))
                    {
                        return food;
                    }
                }
            }

            return null;
        }

        private static MealItemViewModel BuildItemFromFood(Food food, DetectedFoodLabel detected)
        {
            var item = new MealItemViewModel
            {
                FoodId = food.FoodId,
                FoodName = food.FoodName,
                WasAiDetected = true,
                AiConfidence = detected.Confidence,
                PortionOptions = food.Portions.Select(p => new FoodPortionOption
                {
                    PortionId = p.PortionId,
                    PortionName = p.PortionName,
                    WeightGrams = p.WeightGrams,
                    Calories = p.Calories,
                    Protein = p.Protein,
                    Carbs = p.Carbs,
                    Fat = p.Fat,
                    Fibre = p.Fibre
                }).ToList()
            };

            // Default to the first (smallest) portion — member adjusts on the review screen
            if (item.PortionOptions.Any())
            {
                item.SelectedPortionId = item.PortionOptions.First().PortionId;
            }

            return item;
        }
    }
}