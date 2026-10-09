namespace SwiftyS26.Models
{
    public class MealTotals
    {
        public decimal Calories { get; set; }
        public decimal Protein { get; set; }
        public decimal Carbs { get; set; }
        public decimal Fat { get; set; }
        public decimal Fibre { get; set; }
    }

    public class GoalVerdictResult
    {
        public string Verdict { get; set; }        // "On Track" / "Off Track" / "Neutral"
        public string Message { get; set; }
    }

    public static class NutritionVerdictService
    {
        // Roughly splits a member's daily TDEE into a "per-meal" budget,
        // assuming ~4 meals a day, to judge whether THIS meal fits their goal
        private const int AssumedMealsPerDay = 4;

        public static GoalVerdictResult EvaluateMeal(MealTotals totals, FitnessProfile profile)
        {
            if (profile == null)
            {
                return new GoalVerdictResult
                {
                    Verdict = "Neutral",
                    Message = "Complete your Fitness Goals profile to get personalised feedback on your meals."
                };
            }

            decimal perMealCalorieBudget = profile.TDEE / AssumedMealsPerDay;
            decimal proteinTarget = profile.WeightKg * 1.6m / AssumedMealsPerDay; // ~1.6g/kg/day is a common target

            switch (profile.GoalType)
            {
                case "Weight Loss":
                    return EvaluateWeightLoss(totals, perMealCalorieBudget, proteinTarget);

                case "Muscle Gain":
                    return EvaluateMuscleGain(totals, perMealCalorieBudget, proteinTarget);

                case "Endurance":
                    return EvaluateEndurance(totals, perMealCalorieBudget);

                case "Maintenance":
                default:
                    return EvaluateMaintenance(totals, perMealCalorieBudget);
            }
        }

        private static GoalVerdictResult EvaluateWeightLoss(MealTotals totals, decimal calorieBudget, decimal proteinTarget)
        {
            if (totals.Calories > calorieBudget * 1.3m)
            {
                return new GoalVerdictResult
                {
                    Verdict = "Off Track",
                    Message = string.Format("This meal is higher in calories than ideal for your Weight Loss goal (about {0} kcal over your per-meal target). Consider a lighter portion next time.", (int)(totals.Calories - calorieBudget))
                };
            }

            if (totals.Protein < proteinTarget * 0.6m)
            {
                return new GoalVerdictResult
                {
                    Verdict = "Off Track",
                    Message = "This meal is lower in protein than ideal for Weight Loss — protein helps preserve muscle while you're in a calorie deficit."
                };
            }

            return new GoalVerdictResult
            {
                Verdict = "On Track",
                Message = "Good choice for your Weight Loss goal — reasonable calories with decent protein."
            };
        }

        private static GoalVerdictResult EvaluateMuscleGain(MealTotals totals, decimal calorieBudget, decimal proteinTarget)
        {
            if (totals.Protein < proteinTarget * 0.7m)
            {
                return new GoalVerdictResult
                {
                    Verdict = "Off Track",
                    Message = "This meal is light on protein for a Muscle Gain goal — aim to include a stronger protein source to support muscle growth."
                };
            }

            if (totals.Calories < calorieBudget * 0.6m)
            {
                return new GoalVerdictResult
                {
                    Verdict = "Off Track",
                    Message = "This meal may be too small to support your Muscle Gain goal — you likely need more calories to fuel growth."
                };
            }

            return new GoalVerdictResult
            {
                Verdict = "On Track",
                Message = "Solid meal for Muscle Gain — good protein and enough calories to support growth."
            };
        }

        private static GoalVerdictResult EvaluateEndurance(MealTotals totals, decimal calorieBudget)
        {
            if (totals.Carbs < 30)
            {
                return new GoalVerdictResult
                {
                    Verdict = "Off Track",
                    Message = "This meal is fairly low in carbs — endurance training relies heavily on carbs for energy, so consider adding more."
                };
            }

            return new GoalVerdictResult
            {
                Verdict = "On Track",
                Message = "Good carb intake for an Endurance goal — this should help fuel your training."
            };
        }

        private static GoalVerdictResult EvaluateMaintenance(MealTotals totals, decimal calorieBudget)
        {
            if (totals.Calories > calorieBudget * 1.4m)
            {
                return new GoalVerdictResult
                {
                    Verdict = "Off Track",
                    Message = "This meal is noticeably higher in calories than your usual per-meal budget for Maintenance."
                };
            }

            return new GoalVerdictResult
            {
                Verdict = "On Track",
                Message = "This meal fits well within your Maintenance goal."
            };
        }
    }
}