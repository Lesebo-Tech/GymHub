using System;
using System.Collections.Generic;

namespace SwiftyS26.Models
{
    public class MealDashboardViewModel
    {
        public string GoalType { get; set; }

        public decimal TotalCalories { get; set; }
        public decimal TotalProtein { get; set; }
        public decimal TotalCarbs { get; set; }
        public decimal TotalFat { get; set; }
        public decimal TotalFibre { get; set; }

        public decimal TargetCalories { get; set; }
        public decimal TargetProtein { get; set; }
        public decimal TargetCarbs { get; set; }
        public decimal TargetFat { get; set; }
        public decimal TargetFibre { get; set; }

        public int DailyScore { get; set; }

        public string DailyVerdict { get; set; }
        public string DailyMessage { get; set; }

        public List<MealLog> TodaysMeals { get; set; }
            = new List<MealLog>();

        public List<MealLog> RecentMeals { get; set; }
            = new List<MealLog>();

        public List<DailyNutritionPoint> SevenDayTrend { get; set; }
            = new List<DailyNutritionPoint>();
    }

    public class DailyNutritionPoint
    {
        public DateTime Date { get; set; }

        public decimal Calories { get; set; }
        public decimal Protein { get; set; }
        public decimal Carbs { get; set; }
        public decimal Fat { get; set; }
        public decimal Fibre { get; set; }
    }
}