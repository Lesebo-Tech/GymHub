using SwiftyS26.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace SwiftyS26.Controllers
{
    public class MealController : Controller
    {
        private readonly GymHubDB db = new GymHubDB();

        // =========================================================
        // INDEX / NUTRITION DASHBOARD
        // =========================================================

        public ActionResult Index()
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            int memberId = (int)Session["LoggedInMember"];

            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);
            DateTime sevenDaysAgo = today.AddDays(-6);

            var profile = db.FitnessProfiles
                .FirstOrDefault(p => p.MemberId == memberId);

            var todaysMeals = db.MealLogs
                .Where(m =>
                    m.MemberId == memberId &&
                    m.LoggedAt >= today &&
                    m.LoggedAt < tomorrow)
                .OrderByDescending(m => m.LoggedAt)
                .ToList();

            var recentMeals = db.MealLogs
                .Where(m => m.MemberId == memberId)
                .OrderByDescending(m => m.LoggedAt)
                .Take(5)
                .ToList();

            var weeklyMeals = db.MealLogs
                .Where(m =>
                    m.MemberId == memberId &&
                    m.LoggedAt >= sevenDaysAgo)
                .ToList();

            decimal totalCalories =
                todaysMeals.Sum(m => m.TotalCalories);

            decimal totalProtein =
                todaysMeals.Sum(m => m.TotalProtein);

            decimal totalCarbs =
                todaysMeals.Sum(m => m.TotalCarbs);

            decimal totalFat =
                todaysMeals.Sum(m => m.TotalFat);

            decimal totalFibre =
                todaysMeals.Sum(m => m.TotalFibre);

            var targets = GetNutritionTargets(profile);

            var totals = new MealTotals
            {
                Calories = totalCalories,
                Protein = totalProtein,
                Carbs = totalCarbs,
                Fat = totalFat,
                Fibre = totalFibre
            };

            var dailyAnalysis =
                NutritionVerdictService.EvaluateMeal(
                    totals,
                    profile
                );

            int dailyScore =
                CalculateDailyScore(
                    totals,
                    targets
                );

            var sevenDayTrend = Enumerable
                .Range(0, 7)
                .Select(i =>
                {
                    DateTime date =
                        sevenDaysAgo.AddDays(i);

                    var dayMeals = weeklyMeals
                        .Where(m =>
                            m.LoggedAt.Date == date.Date)
                        .ToList();

                    return new DailyNutritionPoint
                    {
                        Date = date,

                        Calories =
                            dayMeals.Sum(m =>
                                m.TotalCalories),

                        Protein =
                            dayMeals.Sum(m =>
                                m.TotalProtein),

                        Carbs =
                            dayMeals.Sum(m =>
                                m.TotalCarbs),

                        Fat =
                            dayMeals.Sum(m =>
                                m.TotalFat),

                        Fibre =
                            dayMeals.Sum(m =>
                                m.TotalFibre)
                    };
                })
                .ToList();

            var model =
                new MealDashboardViewModel
                {
                    GoalType =
                        profile != null
                            ? profile.GoalType
                            : "Not Set",

                    TotalCalories =
                        totalCalories,

                    TotalProtein =
                        totalProtein,

                    TotalCarbs =
                        totalCarbs,

                    TotalFat =
                        totalFat,

                    TotalFibre =
                        totalFibre,

                    TargetCalories =
                        targets.Calories,

                    TargetProtein =
                        targets.Protein,

                    TargetCarbs =
                        targets.Carbs,

                    TargetFat =
                        targets.Fat,

                    TargetFibre =
                        targets.Fibre,

                    DailyScore =
                        dailyScore,

                    DailyVerdict =
                        dailyAnalysis.Verdict,

                    DailyMessage =
                        dailyAnalysis.Message,

                    TodaysMeals =
                        todaysMeals,

                    RecentMeals =
                        recentMeals,

                    SevenDayTrend =
                        sevenDayTrend
                };

            return View(model);
        }


        // =========================================================
        // UPLOAD
        // =========================================================

        [HttpGet]
        public ActionResult Upload()
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            // Fine while developing.
            // Later move this into a database initializer.
            EnsureNutritionSeedData();

            return View();
        }


        // =========================================================
        // SCAN IMAGE
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Scan(
            HttpPostedFileBase mealPhoto,
            string mealType)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            EnsureNutritionSeedData();

            // -------------------------
            // Validate image
            // -------------------------

            if (mealPhoto == null ||
                mealPhoto.ContentLength == 0)
            {
                TempData["Error"] =
                    "Please choose a photo of your meal.";

                return RedirectToAction("Upload");
            }

            string[] allowedExtensions =
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp"
            };

            string extension =
                Path.GetExtension(mealPhoto.FileName);

            extension =
                string.IsNullOrWhiteSpace(extension)
                    ? ""
                    : extension.ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                TempData["Error"] =
                    "Only JPG, JPEG, PNG and WEBP meal photos are allowed.";

                return RedirectToAction("Upload");
            }

            const int maxFileSize =
                5 * 1024 * 1024;

            if (mealPhoto.ContentLength >
                maxFileSize)
            {
                TempData["Error"] =
                    "The meal photo must be 5 MB or smaller.";

                return RedirectToAction("Upload");
            }

            string[] allowedMealTypes =
            {
                "Breakfast",
                "Lunch",
                "Dinner",
                "Snack"
            };

            if (string.IsNullOrWhiteSpace(mealType) ||
                !allowedMealTypes.Contains(mealType))
            {
                mealType = "Meal";
            }


            // -------------------------
            // Save uploaded image
            // -------------------------

            string uploadFolder =
                Server.MapPath(
                    "~/Content/meal-photos"
                );

            Directory.CreateDirectory(
                uploadFolder
            );

            string fileName =
                Guid.NewGuid()
                    .ToString("N")
                + extension;

            string savePath =
                Path.Combine(
                    uploadFolder,
                    fileName
                );

            mealPhoto.SaveAs(savePath);

            string photoUrl =
                "/Content/meal-photos/"
                + fileName;


            // -------------------------
            // Convert image to bytes
            // -------------------------

            byte[] imageBytes;

            using (var ms =
                new MemoryStream())
            {
                if (mealPhoto.InputStream.CanSeek)
                    mealPhoto.InputStream.Position = 0;

                mealPhoto.InputStream
                    .CopyTo(ms);

                imageBytes =
                    ms.ToArray();
            }


            // -------------------------
            // Computer vision
            // -------------------------

            var detectedLabels =
                await VisionService
                    .DetectFoodLabels(
                        imageBytes
                    );

            if (detectedLabels == null)
            {
                detectedLabels =
                    new List<DetectedFoodLabel>();
            }


            // -------------------------
            // Load local food database
            // -------------------------

            var allFoods =
                db.Foods
                    .Include(f =>
                        f.Portions)
                    .ToList();


            // =====================================================
            // Labels that are completely useless
            // =====================================================

            var ignoredLabels =
                new HashSet<string>(
                    new[]
                    {
                        "plate",
                        "tableware",
                        "dishware",
                        "serveware",
                        "bowl",
                        "container",

                        "fork",
                        "spoon",
                        "knife",
                        "cutlery",

                        "glass",
                        "cup",
                        "vase",

                        "table",
                        "indoor",
                        "restaurant",
                        "room",

                        "recipe",
                        "cuisine"
                    },
                    StringComparer.OrdinalIgnoreCase
                );


            // =====================================================
            // Generic labels:
            // Useful to display,
            // but NOT safe enough for automatic food matching.
            // =====================================================

            var genericLabels =
                new HashSet<string>(
                    new[]
                    {
                        "food",
                        "meal",
                        "dish",
                        "ingredient",
                        "produce",

                        "fruit",
                        "vegetable",
                        "vegetables",

                        "meat",
                        "dairy",

                        "drink",
                        "beverage",
                        "soft drink",
                        "non-alcoholic beverage",

                        "juice",
                        "smoothie",
                        "health shake",
                        "shake"
                    },
                    StringComparer.OrdinalIgnoreCase
                );


            // =====================================================
            // Keep meaningful labels for display
            // =====================================================

            var displayTags =
                detectedLabels
                    .Where(l =>
                        l != null &&
                        !string.IsNullOrWhiteSpace(
                            l.Label
                        ) &&
                        l.Confidence >= 0.20 &&
                        !ignoredLabels.Contains(
                            l.Label.Trim()
                        ))
                    .GroupBy(l =>
                        NormalizeFoodText(
                            l.Label
                        ))
                    .Select(g =>
                        g.OrderByDescending(
                            x => x.Confidence
                        ).First())
                    .OrderByDescending(l =>
                        l.Confidence)
                    .Take(20)
                    .ToList();


            // =====================================================
            // Only specific food labels get auto-matched
            // =====================================================

            var automaticMatchTags =
                displayTags
                    .Where(l =>
                        !genericLabels.Contains(
                            NormalizeFoodText(
                                l.Label
                            )
                        ))
                    .ToList();


            // =====================================================
            // Match MULTIPLE foods
            // =====================================================

            var detectedItems =
                MatchDetectedFoods(
                    automaticMatchTags,
                    allFoods
                );


            // =====================================================
            // Build review page
            // =====================================================

            var reviewModel =
                new MealReviewViewModel
                {
                    PhotoUrl =
                        photoUrl,

                    MealType =
                        mealType,

                    Items =
                        detectedItems,

                    AllFoods =
                        allFoods,

                    DetectedTags =
                        displayTags,

                    AiNotice =
                        BuildAiNotice(
                            detectedItems
                        )
                };

            return View(
                "Review",
                reviewModel
            );
        }


        // =========================================================
        // FOOD SEARCH
        // =========================================================

        [HttpGet]
        public ActionResult SearchFoods(
            string term)
        {
            if (Session["LoggedInMember"] == null)
            {
                return Json(
                    new
                    {
                        success = false
                    },
                    JsonRequestBehavior.AllowGet
                );
            }

            if (string.IsNullOrWhiteSpace(term) ||
                term.Trim().Length < 2)
            {
                return Json(
                    new
                    {
                        success = true,
                        foods =
                            new object[0]
                    },
                    JsonRequestBehavior.AllowGet
                );
            }

            string lowered =
                term.Trim()
                    .ToLower();

            var matches =
                db.Foods
                    .Where(f =>
                        f.FoodName
                            .ToLower()
                            .Contains(lowered) ||

                        (
                            f.VisionLabelAliases
                                != null &&

                            f.VisionLabelAliases
                                .ToLower()
                                .Contains(lowered)
                        ))
                    .OrderBy(f =>
                        f.FoodName)
                    .Take(12)
                    .Select(f =>
                        new
                        {
                            foodId =
                                f.FoodId,

                            foodName =
                                f.FoodName,

                            category =
                                f.Category
                        })
                    .ToList();

            return Json(
                new
                {
                    success = true,
                    foods = matches
                },
                JsonRequestBehavior.AllowGet
            );
        }


        // =========================================================
        // GET FOOD PORTIONS
        // =========================================================

        [HttpGet]
        public ActionResult GetPortions(
            int foodId)
        {
            if (Session["LoggedInMember"] == null)
            {
                return Json(
                    new
                    {
                        success = false
                    },
                    JsonRequestBehavior.AllowGet
                );
            }

            var portions =
                db.FoodPortions
                    .Where(p =>
                        p.FoodId == foodId)
                    .OrderBy(p =>
                        p.WeightGrams)
                    .Select(p =>
                        new
                        {
                            portionId =
                                p.PortionId,

                            portionName =
                                p.PortionName,

                            weightGrams =
                                p.WeightGrams,

                            calories =
                                p.Calories,

                            protein =
                                p.Protein,

                            carbs =
                                p.Carbs,

                            fat =
                                p.Fat,

                            fibre =
                                p.Fibre
                        })
                    .ToList();

            return Json(
                new
                {
                    success = true,
                    portions = portions
                },
                JsonRequestBehavior.AllowGet
            );
        }


        // =========================================================
        // MATCH TAG WHEN USER CLICKS LABEL
        // =========================================================

        [HttpGet]
        public ActionResult MatchTag(
            string label)
        {
            if (Session["LoggedInMember"] == null)
            {
                return Json(
                    new
                    {
                        success = false
                    },
                    JsonRequestBehavior.AllowGet
                );
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                return Json(
                    new
                    {
                        success = false,
                        matched = false
                    },
                    JsonRequestBehavior.AllowGet
                );
            }

            var allFoods =
                db.Foods
                    .Include(f =>
                        f.Portions)
                    .ToList();

            var scored =
                allFoods
                    .Select(food =>
                        new
                        {
                            Food = food,

                            Score =
                                GetFoodMatchScore(
                                    food,
                                    NormalizeFoodText(
                                        label
                                    )
                                )
                        })
                    .Where(x =>
                        x.Score >= 55)
                    .OrderByDescending(x =>
                        x.Score)
                    .FirstOrDefault();

            if (scored == null)
            {
                return Json(
                    new
                    {
                        success = true,
                        matched = false
                    },
                    JsonRequestBehavior.AllowGet
                );
            }

            var portions =
                scored.Food.Portions
                    .OrderBy(p =>
                        p.WeightGrams)
                    .Select(p =>
                        new
                        {
                            portionId =
                                p.PortionId,

                            portionName =
                                p.PortionName,

                            weightGrams =
                                p.WeightGrams,

                            calories =
                                p.Calories,

                            protein =
                                p.Protein,

                            carbs =
                                p.Carbs,

                            fat =
                                p.Fat,

                            fibre =
                                p.Fibre
                        })
                    .ToList();

            return Json(
                new
                {
                    success = true,
                    matched = true,

                    foodId =
                        scored.Food.FoodId,

                    foodName =
                        scored.Food.FoodName,

                    matchScore =
                        scored.Score,

                    portions =
                        portions
                },
                JsonRequestBehavior.AllowGet
            );
        }


        // =========================================================
        // SAVE MEAL
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Save(
            string photoUrl,
            string mealType,
            List<int> foodIds,
            List<int> portionIds,
            List<int> quantities)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            int currentMemberId =
                (int)Session["LoggedInMember"];

            if (foodIds == null ||
                portionIds == null ||
                quantities == null ||

                foodIds.Count == 0 ||

                foodIds.Count !=
                portionIds.Count ||

                foodIds.Count !=
                quantities.Count)
            {
                TempData["Error"] =
                    "Please add at least one food item before saving.";

                return RedirectToAction(
                    "Upload"
                );
            }

            var profile =
                db.FitnessProfiles
                    .FirstOrDefault(p =>
                        p.MemberId ==
                        currentMemberId
                    );

            var mealLog =
                new MealLog
                {
                    MemberId =
                        currentMemberId,

                    MealType =
                        string.IsNullOrWhiteSpace(
                            mealType
                        )
                            ? "Meal"
                            : mealType,

                    PhotoUrl =
                        photoUrl,

                    LoggedAt =
                        DateTime.Now,

                    GoalTypeAtLogTime =
                        profile != null
                            ? profile.GoalType
                            : null,

                    Items =
                        new List<MealLogItem>()
                };

            var totals =
                new MealTotals();

            for (int i = 0;
                i < foodIds.Count;
                i++)
            {
                int foodId =
                    foodIds[i];

                int portionId =
                    portionIds[i];

                int qty =
                    Math.Max(
                        1,
                        Math.Min(
                            20,
                            quantities[i]
                        )
                    );

                var food =
                    db.Foods
                        .Find(foodId);

                var portion =
                    db.FoodPortions
                        .FirstOrDefault(
                            p =>
                                p.PortionId ==
                                portionId &&

                                p.FoodId ==
                                foodId
                        );

                if (food == null ||
                    portion == null)
                {
                    continue;
                }

                decimal itemCalories =
                    portion.Calories * qty;

                decimal itemProtein =
                    portion.Protein * qty;

                decimal itemCarbs =
                    portion.Carbs * qty;

                decimal itemFat =
                    portion.Fat * qty;

                decimal itemFibre =
                    portion.Fibre * qty;

                mealLog.Items.Add(
                    new MealLogItem
                    {
                        FoodId =
                            food.FoodId,

                        FoodName =
                            qty > 1
                                ? string.Format(
                                    "{0} (x{1})",
                                    food.FoodName,
                                    qty
                                )
                                : food.FoodName,

                        PortionName =
                            portion.PortionName,

                        WeightGrams =
                            portion.WeightGrams
                            * qty,

                        Calories =
                            itemCalories,

                        Protein =
                            itemProtein,

                        Carbs =
                            itemCarbs,

                        Fat =
                            itemFat,

                        Fibre =
                            itemFibre
                    }
                );

                totals.Calories +=
                    itemCalories;

                totals.Protein +=
                    itemProtein;

                totals.Carbs +=
                    itemCarbs;

                totals.Fat +=
                    itemFat;

                totals.Fibre +=
                    itemFibre;
            }

            if (!mealLog.Items.Any())
            {
                TempData["Error"] =
                    "No valid food portions were submitted.";

                return RedirectToAction(
                    "Upload"
                );
            }

            mealLog.TotalCalories =
                totals.Calories;

            mealLog.TotalProtein =
                totals.Protein;

            mealLog.TotalCarbs =
                totals.Carbs;

            mealLog.TotalFat =
                totals.Fat;

            mealLog.TotalFibre =
                totals.Fibre;

            var verdict =
                NutritionVerdictService
                    .EvaluateMeal(
                        totals,
                        profile
                    );

            mealLog.GoalVerdict =
                verdict.Verdict;

            mealLog.GoalVerdictMessage =
                verdict.Message;

            db.MealLogs.Add(
                mealLog
            );

            db.SaveChanges();

            TempData["Success"] =
                "Meal logged successfully!";

            return RedirectToAction(
                "Summary",
                new
                {
                    id =
                        mealLog.MealLogId
                }
            );
        }


        // =========================================================
        // SUMMARY
        // =========================================================

        [HttpGet]
        public ActionResult Summary(
            int id)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            int memberId =
                (int)Session["LoggedInMember"];

            var mealLog =
                db.MealLogs
                    .Include(m =>
                        m.Items)
                    .FirstOrDefault(m =>
                        m.MealLogId == id &&
                        m.MemberId == memberId
                    );

            if (mealLog == null)
            {
                TempData["Error"] =
                    "Meal not found.";

                return RedirectToAction(
                    "Index"
                );
            }

            return View(mealLog);
        }


        // =========================================================
        // MULTIPLE FOOD MATCHING
        // =========================================================

        private List<MealItemViewModel>
            MatchDetectedFoods(
                IEnumerable<DetectedFoodLabel> labels,
                List<Food> allFoods)
        {
            var result =
                new List<MealItemViewModel>();

            if (labels == null ||
                allFoods == null)
            {
                return result;
            }

            var processedLabels =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase
                );

            foreach (var tag in labels
                .Where(x => x != null)
                .OrderByDescending(
                    x => x.Confidence))
            {
                string label =
                    NormalizeFoodText(
                        tag.Label
                    );

                if (string.IsNullOrWhiteSpace(
                    label))
                {
                    continue;
                }

                // Prevent strawberry and strawberries
                // from effectively being processed twice.
                if (processedLabels.Contains(
                    label))
                {
                    continue;
                }

                processedLabels.Add(
                    label
                );

                if (tag.Confidence < 0.20)
                    continue;

                var bestMatch =
                    allFoods
                        .Select(food =>
                            new
                            {
                                Food =
                                    food,

                                Score =
                                    GetFoodMatchScore(
                                        food,
                                        label
                                    )
                            })
                        .Where(x =>
                            x.Score >= 55)
                        .OrderByDescending(x =>
                            x.Score)
                        .FirstOrDefault();

                if (bestMatch == null)
                    continue;

                // Do not add same database food twice.
                if (result.Any(x =>
                    x.FoodId ==
                    bestMatch.Food.FoodId))
                {
                    continue;
                }

                var portions =
                    bestMatch.Food.Portions
                        .OrderBy(p =>
                            p.WeightGrams)
                        .Select(p =>
                            new FoodPortionOption
                            {
                                PortionId =
                                    p.PortionId,

                                PortionName =
                                    p.PortionName,

                                WeightGrams =
                                    p.WeightGrams,

                                Calories =
                                    p.Calories,

                                Protein =
                                    p.Protein,

                                Carbs =
                                    p.Carbs,

                                Fat =
                                    p.Fat,

                                Fibre =
                                    p.Fibre
                            })
                        .ToList();

                if (!portions.Any())
                    continue;

                int selectedPortionId =
                    GetBestDefaultPortion(
                        bestMatch.Food,
                        portions
                    );

                int specificity =
                    GetLabelSpecificity(
                        label
                    );

                double finalScore =
                    CalculateFinalMatchScore(
                        tag.Confidence,
                        bestMatch.Score,
                        specificity
                    );

                bool needsConfirmation =
                    tag.Confidence < 0.65 ||
                    bestMatch.Score < 80 ||
                    finalScore < 75;

                result.Add(
                    new MealItemViewModel
                    {
                        FoodId =
                            bestMatch.Food.FoodId,

                        FoodName =
                            bestMatch.Food.FoodName,

                        WasAiDetected =
                            true,

                        AiConfidence =
                            tag.Confidence,

                        NeedsConfirmation =
                            needsConfirmation,

                        MatchScore =
                            bestMatch.Score,

                        DetectedLabel =
                            tag.Label,

                        PortionOptions =
                            portions,

                        SelectedPortionId =
                            selectedPortionId
                    }
                );

                // Don't flood the page.
                if (result.Count >= 6)
                    break;
            }

            return result;
        }


        // =========================================================
        // FOOD MATCH SCORE
        // =========================================================

        private int GetFoodMatchScore(
            Food food,
            string detectedLabel)
        {
            if (food == null ||
                string.IsNullOrWhiteSpace(
                    detectedLabel))
            {
                return 0;
            }

            string foodName =
                NormalizeFoodText(
                    food.FoodName
                );

            int score = 0;

            // Exact database name.
            if (foodName ==
                detectedLabel)
            {
                score = 100;
            }

            // Example:
            // detected = grilled chicken breast
            // db       = chicken breast
            else if (
                detectedLabel.Contains(
                    foodName))
            {
                score = 92;
            }

            else if (
                foodName.Contains(
                    detectedLabel))
            {
                score = 88;
            }

            var aliases =
                GetAliases(food);

            foreach (string aliasValue
                in aliases)
            {
                string alias =
                    NormalizeFoodText(
                        aliasValue
                    );

                if (string.IsNullOrWhiteSpace(
                    alias))
                {
                    continue;
                }

                if (alias ==
                    detectedLabel)
                {
                    score =
                        Math.Max(
                            score,
                            98
                        );
                }

                else if (
                    detectedLabel
                        .Contains(alias))
                {
                    score =
                        Math.Max(
                            score,
                            90
                        );
                }

                else if (
                    alias.Contains(
                        detectedLabel))
                {
                    score =
                        Math.Max(
                            score,
                            85
                        );
                }

                else
                {
                    int similarity =
                        GetWordSimilarity(
                            alias,
                            detectedLabel
                        );

                    score =
                        Math.Max(
                            score,
                            similarity
                        );
                }
            }

            return score;
        }


        // =========================================================
        // LABEL SPECIFICITY
        // =========================================================

        private int GetLabelSpecificity(
            string label)
        {
            string normalized =
                NormalizeFoodText(
                    label
                );

            string[] generic =
            {
                "food",
                "meal",
                "dish",
                "fruit",
                "vegetable",
                "meat",
                "dairy",
                "drink",
                "beverage",
                "juice",
                "smoothie",
                "shake",
                "health shake"
            };

            if (generic.Contains(
                normalized))
            {
                return 20;
            }

            return 100;
        }


        // =========================================================
        // FINAL SCORE
        // =========================================================

        private double
            CalculateFinalMatchScore(
                double visionConfidence,
                int databaseMatch,
                int specificity)
        {
            double visionScore =
                visionConfidence * 100.0;

            return
                (visionScore * 0.35)
                +
                (databaseMatch * 0.45)
                +
                (specificity * 0.20);
        }


        // =========================================================
        // ALIASES
        // =========================================================

        private IEnumerable<string>
            GetAliases(Food food)
        {
            if (food == null ||
                string.IsNullOrWhiteSpace(
                    food.VisionLabelAliases))
            {
                return Enumerable
                    .Empty<string>();
            }

            return food
                .VisionLabelAliases
                .Split(',')
                .Select(a =>
                    a.Trim())
                .Where(a =>
                    !string.IsNullOrWhiteSpace(
                        a
                    ));
        }


        // =========================================================
        // NORMALIZE FOOD TEXT
        // =========================================================

        private string NormalizeFoodText(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                value))
            {
                return "";
            }

            string result =
                value
                    .Trim()
                    .ToLowerInvariant()
                    .Replace("-", " ")
                    .Replace("_", " ");

            while (result.Contains("  "))
            {
                result =
                    result.Replace(
                        "  ",
                        " "
                    );
            }

            // Handle common plural endings.

            if (result.EndsWith("ies") &&
                result.Length > 4)
            {
                result =
                    result.Substring(
                        0,
                        result.Length - 3
                    )
                    + "y";
            }

            else if (
                result.EndsWith("s") &&
                !result.EndsWith("ss") &&
                result.Length > 3)
            {
                result =
                    result.Substring(
                        0,
                        result.Length - 1
                    );
            }

            return result;
        }


        // =========================================================
        // WORD SIMILARITY
        // =========================================================

        private int GetWordSimilarity(
            string first,
            string second)
        {
            var firstWords =
                first
                    .Split(' ')
                    .Where(x =>
                        x.Length > 2)
                    .Distinct()
                    .ToList();

            var secondWords =
                second
                    .Split(' ')
                    .Where(x =>
                        x.Length > 2)
                    .Distinct()
                    .ToList();

            if (!firstWords.Any() ||
                !secondWords.Any())
            {
                return 0;
            }

            int matchingWords =
                firstWords.Count(word =>
                    secondWords.Contains(
                        word
                    ));

            double ratio =
                (double)matchingWords /
                Math.Max(
                    firstWords.Count,
                    secondWords.Count
                );

            if (ratio >= 0.75)
                return 80;

            if (ratio >= 0.50)
                return 70;

            if (ratio > 0)
                return 55;

            return 0;
        }


        // =========================================================
        // DEFAULT PORTION
        // =========================================================

        private int GetBestDefaultPortion(
            Food food,
            List<FoodPortionOption> portions)
        {
            if (portions == null ||
                portions.Count == 0)
            {
                return 0;
            }

            string name =
                food == null
                    ? ""
                    : NormalizeFoodText(
                        food.FoodName
                    );

            // Fruit defaults

            if (name == "banana")
            {
                var portion =
                    FindPortionByText(
                        portions,
                        "medium banana"
                    );

                if (portion != null)
                    return portion.PortionId;
            }

            if (name == "apple")
            {
                var portion =
                    FindPortionByText(
                        portions,
                        "medium apple"
                    );

                if (portion != null)
                    return portion.PortionId;
            }

            if (name == "orange")
            {
                var portion =
                    FindPortionByText(
                        portions,
                        "medium orange"
                    );

                if (portion != null)
                    return portion.PortionId;
            }

            // Egg

            if (name == "egg")
            {
                var portion =
                    FindPortionByText(
                        portions,
                        "1 large egg"
                    );

                if (portion != null)
                    return portion.PortionId;
            }

            // Rice

            if (name.Contains("rice"))
            {
                var portion =
                    FindPortionByText(
                        portions,
                        "1 cup"
                    );

                if (portion != null)
                    return portion.PortionId;
            }

            // Chicken / beef / fish

            if (name.Contains("chicken") ||
                name.Contains("beef") ||
                name.Contains("steak") ||
                name.Contains("salmon") ||
                name.Contains("hake"))
            {
                var closest =
                    portions
                        .OrderBy(p =>
                            Math.Abs(
                                p.WeightGrams -
                                150
                            ))
                        .First();

                return closest.PortionId;
            }

            // Smoothie

            if (name.Contains("smoothie") ||
                name.Contains("shake"))
            {
                var portion =
                    portions
                        .OrderBy(p =>
                            Math.Abs(
                                p.WeightGrams -
                                350
                            ))
                        .First();

                return portion.PortionId;
            }

            // General default:
            // closest to about 120 g.

            var preferred =
                portions
                    .Where(p =>
                        p.WeightGrams >= 80 &&
                        p.WeightGrams <= 180)
                    .OrderBy(p =>
                        Math.Abs(
                            p.WeightGrams -
                            120
                        ))
                    .FirstOrDefault();

            if (preferred != null)
                return preferred.PortionId;

            return portions
                .OrderBy(p =>
                    Math.Abs(
                        p.WeightGrams -
                        100
                    ))
                .First()
                .PortionId;
        }


        private FoodPortionOption
            FindPortionByText(
                List<FoodPortionOption> portions,
                string search)
        {
            return portions
                .FirstOrDefault(p =>
                    p.PortionName != null &&
                    p.PortionName.IndexOf(
                        search,
                        StringComparison
                            .OrdinalIgnoreCase
                    ) >= 0
                );
        }


        // =========================================================
        // AI NOTICE
        // =========================================================

        private string BuildAiNotice(
            List<MealItemViewModel> items)
        {
            if (items == null ||
                items.Count == 0)
            {
                return
                    "No foods could be matched automatically. " +
                    "Use the detected labels or search for foods manually.";
            }

            if (items.Any(x =>
                x.NeedsConfirmation))
            {
                return
                    items.Count +
                    " food item(s) were detected. " +
                    "Some predictions are uncertain, so please review the foods and portions.";
            }

            return
                items.Count +
                " food item(s) were automatically detected. " +
                "Please confirm the portions before saving.";
        }


        // =========================================================
        // DAILY NUTRITION TARGETS
        // =========================================================

        private MealTotals
            GetNutritionTargets(
                FitnessProfile profile)
        {
            var targets =
                new MealTotals
                {
                    Calories = 2200m,
                    Protein = 120m,
                    Carbs = 250m,
                    Fat = 70m,
                    Fibre = 30m
                };

            if (profile == null ||
                string.IsNullOrWhiteSpace(
                    profile.GoalType))
            {
                return targets;
            }

            string goal =
                profile.GoalType
                    .ToLowerInvariant();

            if (goal.Contains(
                "weight loss"))
            {
                targets.Calories = 1800m;
                targets.Protein = 130m;
                targets.Carbs = 180m;
                targets.Fat = 60m;
                targets.Fibre = 30m;
            }

            else if (
                goal.Contains("muscle"))
            {
                targets.Calories = 2600m;
                targets.Protein = 160m;
                targets.Carbs = 300m;
                targets.Fat = 80m;
                targets.Fibre = 30m;
            }

            else if (
                goal.Contains("endurance"))
            {
                targets.Calories = 2500m;
                targets.Protein = 130m;
                targets.Carbs = 330m;
                targets.Fat = 75m;
                targets.Fibre = 30m;
            }

            return targets;
        }


        // =========================================================
        // DAILY SCORE
        // =========================================================

        private int CalculateDailyScore(
            MealTotals totals,
            MealTotals targets)
        {
            int score = 0;

            if (targets.Calories > 0)
            {
                decimal ratio =
                    totals.Calories /
                    targets.Calories;

                if (ratio >= 0.75m &&
                    ratio <= 1.10m)
                {
                    score += 30;
                }

                else if (
                    ratio >= 0.50m &&
                    ratio <= 1.25m)
                {
                    score += 20;
                }

                else
                {
                    score += 10;
                }
            }

            if (totals.Protein >=
                targets.Protein * 0.80m)
            {
                score += 25;
            }

            else if (
                totals.Protein >=
                targets.Protein * 0.50m)
            {
                score += 15;
            }

            if (totals.Fibre >=
                targets.Fibre * 0.80m)
            {
                score += 20;
            }

            else if (
                totals.Fibre >=
                targets.Fibre * 0.50m)
            {
                score += 10;
            }

            if (totals.Carbs > 0)
            {
                score += 15;
            }

            if (totals.Fat <=
                targets.Fat * 1.20m)
            {
                score += 10;
            }

            return Math.Min(
                score,
                100
            );
        }


        // =========================================================
        // FOOD SEED DATA
        // =========================================================

        private void EnsureNutritionSeedData()
        {
            // =====================================================
            // PROTEIN
            // =====================================================

            AddOrUpdateFood(
                "Chicken Breast",
                "Protein",
                "chicken,chicken breast,grilled chicken,roasted chicken,cooked chicken,poultry",
                165m, 31m, 0m, 3.6m, 0m,
                new PortionSeed("100 g", 100),
                new PortionSeed("Small serving", 120),
                new PortionSeed("Medium serving", 150),
                new PortionSeed("Large serving", 200)
            );

            AddOrUpdateFood(
                "Chicken Thigh",
                "Protein",
                "chicken thigh,chicken thighs,grilled chicken thigh,roasted chicken thigh",
                209m, 26m, 0m, 10.9m, 0m,
                new PortionSeed("1 small thigh", 80),
                new PortionSeed("1 medium thigh", 120),
                new PortionSeed("2 thighs", 240)
            );

            AddOrUpdateFood(
                "Chicken Drumstick",
                "Protein",
                "chicken drumstick,chicken drumsticks,drumstick,drumsticks,chicken leg",
                175m, 28m, 0m, 6m, 0m,
                new PortionSeed("1 drumstick", 75),
                new PortionSeed("2 drumsticks", 150),
                new PortionSeed("100 g", 100)
            );

            AddOrUpdateFood(
                "Beef",
                "Protein",
                "beef,cooked beef,roast beef,beef meat,red meat",
                250m, 26m, 0m, 15m, 0m,
                new PortionSeed("100 g", 100),
                new PortionSeed("150 g", 150),
                new PortionSeed("200 g", 200)
            );

            AddOrUpdateFood(
                "Beef Mince",
                "Protein",
                "beef mince,minced beef,ground beef,minced meat,ground meat",
                250m, 26m, 0m, 20m, 0m,
                new PortionSeed("100 g", 100),
                new PortionSeed("150 g", 150),
                new PortionSeed("1 cup", 200)
            );

            AddOrUpdateFood(
                "Steak",
                "Protein",
                "steak,beef steak,grilled steak,sirloin steak,fillet steak",
                271m, 26m, 0m, 19m, 0m,
                new PortionSeed("Small steak", 150),
                new PortionSeed("Medium steak", 200),
                new PortionSeed("Large steak", 300)
            );

            AddOrUpdateFood(
                "Pork Chop",
                "Protein",
                "pork,pork chop,pork chops,grilled pork,roast pork",
                231m, 25m, 0m, 14m, 0m,
                new PortionSeed("Small chop", 120),
                new PortionSeed("Medium chop", 170),
                new PortionSeed("Large chop", 220)
            );

            AddOrUpdateFood(
                "Sausage",
                "Protein",
                "sausage,sausages,beef sausage,pork sausage,grilled sausage",
                301m, 12m, 2m, 27m, 0m,
                new PortionSeed("1 sausage", 70),
                new PortionSeed("2 sausages", 140),
                new PortionSeed("100 g", 100)
            );

            AddOrUpdateFood(
                "Boerewors",
                "Protein",
                "boerewors,wors,beef wors,grilled boerewors,sausage coil",
                320m, 15m, 2m, 28m, 0m,
                new PortionSeed("Small piece", 80),
                new PortionSeed("Medium piece", 120),
                new PortionSeed("Large piece", 180)
            );

            AddOrUpdateFood(
                "Egg",
                "Protein",
                "egg,eggs,boiled egg,boiled eggs,cooked egg",
                155m, 13m, 1.1m, 11m, 0m,
                new PortionSeed("1 large egg", 50),
                new PortionSeed("2 large eggs", 100),
                new PortionSeed("3 large eggs", 150)
            );

            AddOrUpdateFood(
                "Scrambled Egg",
                "Protein",
                "scrambled egg,scrambled eggs,egg scramble",
                149m, 10m, 1.6m, 11m, 0m,
                new PortionSeed("1 egg serving", 60),
                new PortionSeed("2 egg serving", 120),
                new PortionSeed("3 egg serving", 180)
            );

            AddOrUpdateFood(
                "Fried Egg",
                "Protein",
                "fried egg,fried eggs,sunny side egg,sunny side up",
                196m, 14m, 0.8m, 15m, 0m,
                new PortionSeed("1 fried egg", 50),
                new PortionSeed("2 fried eggs", 100)
            );

            AddOrUpdateFood(
                "Salmon",
                "Protein",
                "salmon,salmon fillet,grilled salmon,cooked salmon",
                208m, 20m, 0m, 13m, 0m,
                new PortionSeed("100 g", 100),
                new PortionSeed("Small fillet", 120),
                new PortionSeed("Medium fillet", 170),
                new PortionSeed("Large fillet", 220)
            );

            AddOrUpdateFood(
                "Tuna",
                "Protein",
                "tuna,tuna fish,canned tuna,tinned tuna",
                132m, 29m, 0m, 1m, 0m,
                new PortionSeed("1/2 cup", 85),
                new PortionSeed("1 cup", 170),
                new PortionSeed("100 g", 100)
            );

            AddOrUpdateFood(
                "Hake",
                "Protein",
                "hake,hake fish,hake fillet,grilled hake,cooked hake,white fish",
                90m, 20m, 0m, 1m, 0m,
                new PortionSeed("100 g", 100),
                new PortionSeed("Small fillet", 130),
                new PortionSeed("Large fillet", 200)
            );

            AddOrUpdateFood(
                "Sardines",
                "Protein",
                "sardine,sardines,canned sardines,tinned sardines",
                208m, 25m, 0m, 11m, 0m,
                new PortionSeed("1/2 tin", 60),
                new PortionSeed("1 tin", 120),
                new PortionSeed("100 g", 100)
            );


            // =====================================================
            // CARBOHYDRATES
            // =====================================================

            AddOrUpdateFood(
                "White Rice",
                "Carbohydrate",
                "rice,white rice,cooked rice,steamed rice,boiled rice,rice bowl",
                130m, 2.7m, 28m, 0.3m, 0.4m,
                new PortionSeed("1/2 cup", 80),
                new PortionSeed("100 g", 100),
                new PortionSeed("1 cup", 160),
                new PortionSeed("Large serving", 240)
            );

            AddOrUpdateFood(
                "Brown Rice",
                "Carbohydrate",
                "brown rice,whole grain rice,cooked brown rice",
                123m, 2.7m, 25.6m, 1m, 1.6m,
                new PortionSeed("1/2 cup", 90),
                new PortionSeed("1 cup", 180),
                new PortionSeed("100 g", 100)
            );

            AddOrUpdateFood(
                "Basmati Rice",
                "Carbohydrate",
                "basmati rice,basmati,cooked basmati rice,long grain rice",
                121m, 3.5m, 25m, 0.4m, 0.6m,
                new PortionSeed("1/2 cup", 80),
                new PortionSeed("1 cup", 160),
                new PortionSeed("100 g", 100)
            );

            AddOrUpdateFood(
                "Fried Rice",
                "Carbohydrate",
                "fried rice,egg fried rice,vegetable fried rice",
                168m, 4m, 28m, 5m, 1.3m,
                new PortionSeed("1/2 cup", 100),
                new PortionSeed("1 cup", 200),
                new PortionSeed("Large serving", 300)
            );

            AddOrUpdateFood(
                "Pap",
                "Carbohydrate",
                "pap,maize meal,maize porridge,mielie pap,cornmeal porridge",
                120m, 2.5m, 25m, 1m, 1.5m,
                new PortionSeed("Small serving", 120),
                new PortionSeed("1 cup", 240),
                new PortionSeed("Large serving", 300)
            );

            AddOrUpdateFood(
                "Samp",
                "Carbohydrate",
                "samp,cooked samp,maize kernels,crushed maize",
                119m, 3.2m, 25m, 1m, 2.5m,
                new PortionSeed("1/2 cup", 90),
                new PortionSeed("1 cup", 180),
                new PortionSeed("Large serving", 270)
            );

            AddOrUpdateFood(
                "Pasta",
                "Carbohydrate",
                "pasta,cooked pasta,italian pasta",
                157m, 5.8m, 30.9m, 0.9m, 1.8m,
                new PortionSeed("1/2 cup", 70),
                new PortionSeed("1 cup", 140),
                new PortionSeed("Large serving", 210)
            );

            AddOrUpdateFood(
                "Spaghetti",
                "Carbohydrate",
                "spaghetti,cooked spaghetti,pasta noodles",
                158m, 5.8m, 31m, 0.9m, 1.8m,
                new PortionSeed("Small serving", 100),
                new PortionSeed("Medium serving", 180),
                new PortionSeed("Large serving", 250)
            );

            AddOrUpdateFood(
                "Noodles",
                "Carbohydrate",
                "noodles,cooked noodles,egg noodles,instant noodles",
                138m, 4.5m, 25m, 2m, 1.2m,
                new PortionSeed("1 cup", 160),
                new PortionSeed("Medium bowl", 250),
                new PortionSeed("Large bowl", 350)
            );

            AddOrUpdateFood(
                "Potato",
                "Carbohydrate",
                "potato,potatoes,boiled potato,baked potato,cooked potato",
                87m, 1.9m, 20m, 0.1m, 1.8m,
                new PortionSeed("Small potato", 130),
                new PortionSeed("Medium potato", 170),
                new PortionSeed("Large potato", 250)
            );

            AddOrUpdateFood(
                "Mashed Potato",
                "Carbohydrate",
                "mashed potato,mashed potatoes,potato mash,mash",
                113m, 2m, 17m, 4m, 1.5m,
                new PortionSeed("1/2 cup", 105),
                new PortionSeed("1 cup", 210),
                new PortionSeed("Large serving", 300)
            );

            AddOrUpdateFood(
                "Sweet Potato",
                "Carbohydrate",
                "sweet potato,sweet potatoes,roasted sweet potato,cooked sweet potato",
                90m, 2m, 21m, 0.2m, 3.3m,
                new PortionSeed("Small", 100),
                new PortionSeed("Medium", 150),
                new PortionSeed("Large", 200)
            );

            AddOrUpdateFood(
                "Bread",
                "Carbohydrate",
                "bread,bread slice,sliced bread,toast,toasted bread",
                265m, 9m, 49m, 3.2m, 2.7m,
                new PortionSeed("1 slice", 30),
                new PortionSeed("2 slices", 60),
                new PortionSeed("3 slices", 90)
            );

            AddOrUpdateFood(
                "Oats",
                "Carbohydrate",
                "oats,oatmeal,porridge,oat porridge,cooked oats",
                71m, 2.5m, 12m, 1.5m, 1.7m,
                new PortionSeed("1/2 cup cooked", 120),
                new PortionSeed("1 cup cooked", 240),
                new PortionSeed("Large bowl", 350)
            );


            // =====================================================
            // VEGETABLES
            // =====================================================

            AddOrUpdateFood(
                "Broccoli",
                "Vegetable",
                "broccoli,broccoli florets,cooked broccoli,steamed broccoli",
                35m, 2.4m, 7.2m, 0.4m, 3.3m,
                new PortionSeed("1/2 cup", 78),
                new PortionSeed("1 cup", 156),
                new PortionSeed("100 g", 100)
            );

            AddOrUpdateFood(
                "Carrot",
                "Vegetable",
                "carrot,carrots,baby carrot,baby carrots,cooked carrot,raw carrot",
                41m, 0.9m, 9.6m, 0.2m, 2.8m,
                new PortionSeed("1 medium carrot", 61),
                new PortionSeed("1/2 cup", 64),
                new PortionSeed("1 cup", 128)
            );

            AddOrUpdateFood(
                "Spinach",
                "Vegetable",
                "spinach,baby spinach,cooked spinach,leafy greens",
                23m, 2.9m, 3.6m, 0.4m, 2.2m,
                new PortionSeed("1 cup raw", 30),
                new PortionSeed("1/2 cup cooked", 90),
                new PortionSeed("100 g", 100)
            );

            AddOrUpdateFood(
                "Cabbage",
                "Vegetable",
                "cabbage,green cabbage,cooked cabbage,shredded cabbage",
                25m, 1.3m, 5.8m, 0.1m, 2.5m,
                new PortionSeed("1/2 cup", 45),
                new PortionSeed("1 cup", 90),
                new PortionSeed("100 g", 100)
            );

            AddOrUpdateFood(
                "Green Beans",
                "Vegetable",
                "green beans,string beans,cooked green beans",
                35m, 1.9m, 7.9m, 0.3m, 3.2m,
                new PortionSeed("1/2 cup", 62),
                new PortionSeed("1 cup", 125),
                new PortionSeed("100 g", 100)
            );

            AddOrUpdateFood(
                "Peas",
                "Vegetable",
                "peas,green peas,garden peas,cooked peas",
                84m, 5.4m, 15m, 0.4m, 5.5m,
                new PortionSeed("1/2 cup", 80),
                new PortionSeed("1 cup", 160),
                new PortionSeed("100 g", 100)
            );

            AddOrUpdateFood(
                "Cauliflower",
                "Vegetable",
                "cauliflower,cauliflower florets,cooked cauliflower",
                25m, 1.9m, 5m, 0.3m, 2m,
                new PortionSeed("1/2 cup", 50),
                new PortionSeed("1 cup", 100)
            );

            AddOrUpdateFood(
                "Tomato",
                "Vegetable",
                "tomato,tomatoes,cherry tomato,cherry tomatoes,sliced tomato",
                18m, 0.9m, 3.9m, 0.2m, 1.2m,
                new PortionSeed("Small tomato", 90),
                new PortionSeed("Medium tomato", 123),
                new PortionSeed("Large tomato", 180)
            );

            AddOrUpdateFood(
                "Cucumber",
                "Vegetable",
                "cucumber,cucumbers,sliced cucumber",
                15m, 0.7m, 3.6m, 0.1m, 0.5m,
                new PortionSeed("1/2 cup", 52),
                new PortionSeed("1 cup", 104),
                new PortionSeed("100 g", 100)
            );

            AddOrUpdateFood(
                "Butternut",
                "Vegetable",
                "butternut,butternut squash,squash,roasted butternut,cooked butternut",
                45m, 1m, 12m, 0.1m, 2m,
                new PortionSeed("1/2 cup", 100),
                new PortionSeed("1 cup", 200)
            );

            AddOrUpdateFood(
                "Pumpkin",
                "Vegetable",
                "pumpkin,cooked pumpkin,roasted pumpkin,pumpkin cubes",
                26m, 1m, 6.5m, 0.1m, 0.5m,
                new PortionSeed("1/2 cup", 120),
                new PortionSeed("1 cup", 240)
            );


            // =====================================================
            // FRUIT
            // =====================================================

            AddOrUpdateFood(
                "Banana",
                "Fruit",
                "banana,bananas,banana fruit,ripe banana",
                89m, 1.1m, 23m, 0.3m, 2.6m,
                new PortionSeed("Small banana", 100),
                new PortionSeed("Medium banana", 118),
                new PortionSeed("Large banana", 136)
            );

            AddOrUpdateFood(
                "Apple",
                "Fruit",
                "apple,apples,red apple,green apple",
                52m, 0.3m, 14m, 0.2m, 2.4m,
                new PortionSeed("Small apple", 150),
                new PortionSeed("Medium apple", 182),
                new PortionSeed("Large apple", 220)
            );

            AddOrUpdateFood(
                "Orange",
                "Fruit",
                "orange,oranges,orange fruit,citrus",
                47m, 0.9m, 12m, 0.1m, 2.4m,
                new PortionSeed("Small orange", 100),
                new PortionSeed("Medium orange", 130),
                new PortionSeed("Large orange", 180)
            );

            AddOrUpdateFood(
                "Strawberry",
                "Fruit",
                "strawberry,strawberries,fresh strawberry,fresh strawberries,berry",
                32m, 0.7m, 7.7m, 0.3m, 2m,
                new PortionSeed("1 strawberry", 12),
                new PortionSeed("5 strawberries", 60),
                new PortionSeed("1/2 cup", 76),
                new PortionSeed("1 cup", 152)
            );

            AddOrUpdateFood(
                "Blueberry",
                "Fruit",
                "blueberry,blueberries,fresh blueberries,blue berries",
                57m, 0.7m, 14.5m, 0.3m, 2.4m,
                new PortionSeed("1/2 cup", 74),
                new PortionSeed("1 cup", 148)
            );

            AddOrUpdateFood(
                "Grapes",
                "Fruit",
                "grape,grapes,green grapes,red grapes",
                69m, 0.7m, 18m, 0.2m, 0.9m,
                new PortionSeed("10 grapes", 50),
                new PortionSeed("1/2 cup", 75),
                new PortionSeed("1 cup", 150)
            );

            AddOrUpdateFood(
                "Mango",
                "Fruit",
                "mango,mangoes,mango fruit,mango slices",
                60m, 0.8m, 15m, 0.4m, 1.6m,
                new PortionSeed("1/2 mango", 100),
                new PortionSeed("1 cup", 165),
                new PortionSeed("Whole mango", 250)
            );

            AddOrUpdateFood(
                "Pineapple",
                "Fruit",
                "pineapple,pineapple slices,pineapple chunks,fresh pineapple",
                50m, 0.5m, 13m, 0.1m, 1.4m,
                new PortionSeed("1 slice", 84),
                new PortionSeed("1/2 cup", 82),
                new PortionSeed("1 cup", 165)
            );

            AddOrUpdateFood(
                "Watermelon",
                "Fruit",
                "watermelon,water melon,watermelon slice,melon",
                30m, 0.6m, 7.6m, 0.2m, 0.4m,
                new PortionSeed("Small slice", 150),
                new PortionSeed("1 cup", 152),
                new PortionSeed("Large slice", 300)
            );

            AddOrUpdateFood(
                "Avocado",
                "Fruit",
                "avocado,avocados,avo,sliced avocado",
                160m, 2m, 8.5m, 14.7m, 6.7m,
                new PortionSeed("1/4 avocado", 50),
                new PortionSeed("1/2 avocado", 100),
                new PortionSeed("Whole avocado", 200)
            );


            // =====================================================
            // DAIRY
            // =====================================================

            AddOrUpdateFood(
                "Milk",
                "Dairy",
                "milk,cow milk,fresh milk,whole milk",
                61m, 3.2m, 4.8m, 3.3m, 0m,
                new PortionSeed("1/2 cup", 125),
                new PortionSeed("1 cup", 250),
                new PortionSeed("Large glass", 350)
            );

            AddOrUpdateFood(
                "Low Fat Milk",
                "Dairy",
                "low fat milk,low-fat milk,semi skimmed milk",
                50m, 3.4m, 4.8m, 2m, 0m,
                new PortionSeed("1/2 cup", 125),
                new PortionSeed("1 cup", 250),
                new PortionSeed("Large glass", 350)
            );

            AddOrUpdateFood(
                "Skim Milk",
                "Dairy",
                "skim milk,skimmed milk,fat free milk,nonfat milk",
                34m, 3.4m, 5m, 0.1m, 0m,
                new PortionSeed("1/2 cup", 125),
                new PortionSeed("1 cup", 250)
            );

            AddOrUpdateFood(
                "Greek Yogurt",
                "Dairy",
                "greek yogurt,greek yoghurt,yogurt,yoghurt,plain greek yogurt",
                97m, 9m, 3.9m, 5m, 0m,
                new PortionSeed("Small tub", 100),
                new PortionSeed("1/2 cup", 120),
                new PortionSeed("1 cup", 240)
            );

            AddOrUpdateFood(
                "Plain Yogurt",
                "Dairy",
                "plain yogurt,plain yoghurt,natural yogurt,natural yoghurt",
                61m, 3.5m, 4.7m, 3.3m, 0m,
                new PortionSeed("Small tub", 100),
                new PortionSeed("1/2 cup", 125),
                new PortionSeed("1 cup", 250)
            );

            AddOrUpdateFood(
                "Cheese",
                "Dairy",
                "cheese,cheddar,cheddar cheese,cheese slice,sliced cheese",
                402m, 25m, 1.3m, 33m, 0m,
                new PortionSeed("1 slice", 20),
                new PortionSeed("30 g", 30),
                new PortionSeed("2 slices", 40)
            );

            AddOrUpdateFood(
                "Cottage Cheese",
                "Dairy",
                "cottage cheese,soft cheese,curd cheese",
                98m, 11m, 3.4m, 4.3m, 0m,
                new PortionSeed("1/4 cup", 55),
                new PortionSeed("1/2 cup", 110),
                new PortionSeed("1 cup", 220)
            );


            // =====================================================
            // LEGUMES
            // =====================================================

            AddOrUpdateFood(
                "Beans",
                "Legume",
                "beans,cooked beans,red beans,kidney beans,black beans",
                127m, 8.7m, 22.8m, 0.5m, 6.4m,
                new PortionSeed("1/2 cup", 90),
                new PortionSeed("1 cup", 180)
            );

            AddOrUpdateFood(
                "Baked Beans",
                "Legume",
                "baked beans,beans in tomato sauce,tinned baked beans",
                94m, 4.8m, 17m, 0.4m, 4m,
                new PortionSeed("1/2 cup", 125),
                new PortionSeed("1 cup", 250)
            );

            AddOrUpdateFood(
                "Lentils",
                "Legume",
                "lentil,lentils,cooked lentils,brown lentils,green lentils",
                116m, 9m, 20m, 0.4m, 7.9m,
                new PortionSeed("1/2 cup", 100),
                new PortionSeed("1 cup", 200)
            );


            // =====================================================
            // NUTS / EXTRAS
            // =====================================================

            AddOrUpdateFood(
                "Peanut Butter",
                "Fat/Protein",
                "peanut butter,peanut spread,nut butter",
                588m, 25m, 20m, 50m, 6m,
                new PortionSeed("1 teaspoon", 5),
                new PortionSeed("1 tablespoon", 16),
                new PortionSeed("2 tablespoons", 32)
            );

            AddOrUpdateFood(
                "Almonds",
                "Nuts",
                "almond,almonds,almond nuts",
                579m, 21m, 22m, 50m, 12.5m,
                new PortionSeed("10 almonds", 12),
                new PortionSeed("Small handful", 28),
                new PortionSeed("Large handful", 40)
            );

            AddOrUpdateFood(
                "Cashews",
                "Nuts",
                "cashew,cashews,cashew nuts",
                553m, 18m, 30m, 44m, 3.3m,
                new PortionSeed("10 cashews", 15),
                new PortionSeed("Small handful", 28),
                new PortionSeed("Large handful", 40)
            );

            AddOrUpdateFood(
                "Honey",
                "Sweetener",
                "honey,raw honey,honey syrup",
                304m, 0.3m, 82m, 0m, 0.2m,
                new PortionSeed("1 teaspoon", 7),
                new PortionSeed("1 tablespoon", 21),
                new PortionSeed("2 tablespoons", 42)
            );


            // =====================================================
            // COMPOSITE FOODS / DRINKS
            // =====================================================

            AddOrUpdateFood(
                "Strawberry Smoothie",
                "Drink",
                "strawberry smoothie,strawberries smoothie,berry smoothie,strawberry shake",
                75m, 3m, 12m, 2m, 1m,
                new PortionSeed("Small glass", 250),
                new PortionSeed("Medium glass", 350),
                new PortionSeed("Large glass", 500)
            );

            AddOrUpdateFood(
                "Banana Smoothie",
                "Drink",
                "banana smoothie,banana shake,banana milkshake,banana health shake",
                90m, 3m, 16m, 2m, 1.2m,
                new PortionSeed("Small glass", 250),
                new PortionSeed("Medium glass", 350),
                new PortionSeed("Large glass", 500)
            );

            AddOrUpdateFood(
                "Protein Shake",
                "Drink",
                "protein shake,protein drink,whey shake,whey protein shake,fitness shake",
                70m, 10m, 4m, 1.5m, 0.5m,
                new PortionSeed("Small shaker", 250),
                new PortionSeed("Medium shaker", 350),
                new PortionSeed("Large shaker", 500)
            );

            AddOrUpdateFood(
                "Chicken Salad",
                "Meal",
                "chicken salad,grilled chicken salad,salad with chicken",
                120m, 12m, 5m, 6m, 2m,
                new PortionSeed("Small bowl", 200),
                new PortionSeed("Medium bowl", 300),
                new PortionSeed("Large bowl", 400)
            );

            AddOrUpdateFood(
                "Samp and Beans",
                "Meal",
                "samp and beans,samp beans,umngqusho,cooked samp and beans",
                130m, 6m, 24m, 1.5m, 5m,
                new PortionSeed("Small serving", 180),
                new PortionSeed("Medium serving", 250),
                new PortionSeed("Large serving", 350)
            );

            db.SaveChanges();
        }


        // =========================================================
        // ADD / UPDATE FOOD
        // =========================================================

        private void AddOrUpdateFood(
            string foodName,
            string category,
            string aliases,
            decimal caloriesPer100g,
            decimal proteinPer100g,
            decimal carbsPer100g,
            decimal fatPer100g,
            decimal fibrePer100g,
            params PortionSeed[] portions)
        {
            var food =
                db.Foods
                    .Include(f =>
                        f.Portions)
                    .FirstOrDefault(f =>
                        f.FoodName ==
                        foodName
                    );

            if (food == null)
            {
                food =
                    new Food
                    {
                        FoodName =
                            foodName,

                        Category =
                            category,

                        VisionLabelAliases =
                            aliases,

                        Portions =
                            new List<FoodPortion>()
                    };

                db.Foods.Add(
                    food
                );

                // Need ID before creating portions.
                db.SaveChanges();
            }
            else
            {
                food.Category =
                    category;

                // Always update aliases,
                // so new scanner aliases are applied.
                food.VisionLabelAliases =
                    aliases;
            }

            foreach (var seed
                in portions)
            {
                var existing =
                    db.FoodPortions
                        .FirstOrDefault(p =>
                            p.FoodId ==
                            food.FoodId &&

                            p.PortionName ==
                            seed.Name
                        );

                decimal factor =
                    seed.Grams / 100m;

                decimal calories =
                    Math.Round(
                        caloriesPer100g *
                        factor,
                        1
                    );

                decimal protein =
                    Math.Round(
                        proteinPer100g *
                        factor,
                        1
                    );

                decimal carbs =
                    Math.Round(
                        carbsPer100g *
                        factor,
                        1
                    );

                decimal fat =
                    Math.Round(
                        fatPer100g *
                        factor,
                        1
                    );

                decimal fibre =
                    Math.Round(
                        fibrePer100g *
                        factor,
                        1
                    );

                if (existing == null)
                {
                    db.FoodPortions.Add(
                        new FoodPortion
                        {
                            FoodId =
                                food.FoodId,

                            PortionName =
                                seed.Name,

                            WeightGrams =
                                seed.Grams,

                            Calories =
                                calories,

                            Protein =
                                protein,

                            Carbs =
                                carbs,

                            Fat =
                                fat,

                            Fibre =
                                fibre
                        }
                    );
                }
                else
                {
                    // Keep existing portions current.
                    existing.WeightGrams =
                        seed.Grams;

                    existing.Calories =
                        calories;

                    existing.Protein =
                        protein;

                    existing.Carbs =
                        carbs;

                    existing.Fat =
                        fat;

                    existing.Fibre =
                        fibre;
                }
            }
        }


        // =========================================================
        // PORTION SEED
        // =========================================================

        private class PortionSeed
        {
            public PortionSeed(
                string name,
                int grams)
            {
                Name =
                    name;

                Grams =
                    grams;
            }

            public string Name
            {
                get;
                private set;
            }

            public int Grams
            {
                get;
                private set;
            }
        }
        // ── ADD these actions to MealController.cs ──────────────────

        // GET: Meal/DietPlan
        // GET: Meal/DietPlan
        public ActionResult DietPlan()
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            int memberId = (int)Session["LoggedInMember"];
            var profile = db.FitnessProfiles.FirstOrDefault(p => p.MemberId == memberId);

            if (profile == null)
            {
                TempData["Error"] = "Please complete your fitness profile before generating a meal plan.";
                return RedirectToAction("RealActionName", "RealControllerName");
            }

            ViewBag.DietaryPreferences = MealPlanHelper.DietaryPreferences;
            ViewBag.Allergens = MealPlanHelper.Allergens;

            return View(profile);
        }

        // POST: Meal/GenerateDietPlan
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerateDietPlan(string dietaryPreference, string[] allergies)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            int memberId = (int)Session["LoggedInMember"];
            var profile = db.FitnessProfiles.FirstOrDefault(p => p.MemberId == memberId);

            if (profile == null)
            {
                TempData["Error"] = "Please complete your fitness profile before generating a meal plan.";
                return RedirectToAction("Profile", "Fitness"); // TODO: confirm real UC09 profile-setup route
            }

            string allergiesCsv = allergies != null ? string.Join(",", allergies) : "";
            profile.DietaryPreference = dietaryPreference;
            profile.Allergies = allergiesCsv;
            profile.LastUpdated = DateTime.Now;
            db.SaveChanges();

            var targets = MealPlanHelper.CalculateTargets(profile.WeightKg, profile.TDEE, profile.GoalType);

            var library = db.Recipes.Where(r => r.IsActive).ToList();
            var planItems = MealPlanHelper.GeneratePlan(library, dietaryPreference, allergiesCsv, (int)targets.Calories);

            if (!planItems.Any())
            {
                TempData["Error"] = "No matching recipes found for your dietary preference and allergies. Please contact staff to expand the recipe library.";
                return RedirectToAction("DietPlan");
            }

            var plan = new MealPlan
            {
                MemberId = memberId,
                TargetCalories = (int)targets.Calories,
                TargetProtein = targets.Protein,
                TargetCarbs = targets.Carbs,
                TargetFat = targets.Fat,
                GoalType = profile.GoalType,
                GeneratedAt = DateTime.Now
            };
            db.MealPlans.Add(plan);
            db.SaveChanges();

            foreach (var item in planItems)
            {
                item.MealPlanId = plan.MealPlanId;
                db.MealPlanItems.Add(item);
            }
            db.SaveChanges();

            return RedirectToAction("ViewPlan", new { id = plan.MealPlanId });
        }

        // GET: Meal/ViewPlan/5
        public ActionResult ViewPlan(int id)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            int memberId = (int)Session["LoggedInMember"];

            var plan = db.MealPlans
                .Include(p => p.Items.Select(i => i.Recipe))
                .FirstOrDefault(p => p.MealPlanId == id && p.MemberId == memberId);

            if (plan == null)
                return RedirectToAction("DietPlan");

            return View(plan);
        }

        // POST: Meal/AcceptPlan
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AcceptPlan(int mealPlanId)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            int memberId = (int)Session["LoggedInMember"];
            var plan = db.MealPlans.FirstOrDefault(p => p.MealPlanId == mealPlanId && p.MemberId == memberId);
            if (plan != null)
            {
                plan.IsAccepted = true;
                db.SaveChanges();
                TempData["Success"] = "Meal plan accepted!";
            }

            return RedirectToAction("ViewPlan", new { id = mealPlanId });
        }
        // GET: Meal/MyPlans
        public ActionResult MyPlans()
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            int memberId = (int)Session["LoggedInMember"];

            var plans = db.MealPlans
                .Include(p => p.Items)
                .Where(p => p.MemberId == memberId)
                .OrderByDescending(p => p.GeneratedAt)
                .ToList();

            return View(plans);
        }


        // =========================================================
        // DISPOSE
        // =========================================================

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }

            base.Dispose(
                disposing
            );
        }
    }
}