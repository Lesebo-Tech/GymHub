using System.Collections.Generic;

namespace SwiftyS26.Models
{
    public class MealItemViewModel
    {
        public int? FoodId { get; set; }

        public string FoodName { get; set; }

        public bool WasAiDetected { get; set; }

        public double? AiConfidence { get; set; }

        // New
        public bool NeedsConfirmation { get; set; }

        // New - shows how well the vision label matched our food DB
        public int MatchScore { get; set; }

        // New - original label returned by vision
        public string DetectedLabel { get; set; }

        public List<FoodPortionOption> PortionOptions { get; set; }
            = new List<FoodPortionOption>();

        public int? SelectedPortionId { get; set; }
    }


    public class FoodPortionOption
    {
        public int PortionId { get; set; }

        public string PortionName { get; set; }

        public int WeightGrams { get; set; }

        public decimal Calories { get; set; }

        public decimal Protein { get; set; }

        public decimal Carbs { get; set; }

        public decimal Fat { get; set; }

        public decimal Fibre { get; set; }
    }


    public class MealReviewViewModel
    {
        public string PhotoUrl { get; set; }

        public string MealType { get; set; }

        public List<MealItemViewModel> Items { get; set; }
            = new List<MealItemViewModel>();

        public List<Food> AllFoods { get; set; }
            = new List<Food>();

        public string AiNotice { get; set; }

        public List<DetectedFoodLabel> DetectedTags { get; set; }
            = new List<DetectedFoodLabel>();
    }
}