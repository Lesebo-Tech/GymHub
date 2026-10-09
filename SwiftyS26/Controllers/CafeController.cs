using SwiftyS26.Models;
using System;
using System.Linq;
using System.Web.Mvc;

namespace SwiftyS26.Controllers
{
    // ============================================================
    // CafeController
    // RESPONSIBILITY: Member-facing café page only.
    //   - Show all products (with category filter)
    //   - Seed default products (one-time setup)
    //
    // NOT here:
    //   - RedeemProduct  → DashboardController (it's a wallet action)
    //   - Add/Edit/Delete/Toggle products → AdminController
    // ============================================================
    public class CafeController : Controller
    {
        private readonly GymHubDB db = new GymHubDB();

        private int? GetLoggedInMemberId() => Session["LoggedInMember"] as int?;
        private bool IsLoggedIn() => Session["LoggedInMember"] != null;

        // ============================================================
        // INDEX — Full café page (external page opened via "See All →")
        // GET: /Cafe
        // ============================================================
        public ActionResult Index()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Forms");

            int memberId = GetLoggedInMemberId().Value;

            var loyalty = db.LoyaltyAccounts
                .FirstOrDefault(l => l.MemberId == memberId);

            ViewBag.CurrentPoints = loyalty?.CurrentPoints ?? 0;
            ViewBag.MemberName = Session["LoggedInName"] as string ?? "Member";

            return View();
        }

        // ============================================================
        // GET PRODUCTS — Used by the café page JS to load all products
        // GET: /Cafe/GetProducts?category=Smoothie
        // GET: /Cafe/GetProducts  (returns all)
        // ============================================================
        public ActionResult GetProducts(string category = "")
        {
            if (!IsLoggedIn())
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            var query = db.CafeProducts.Where(p => p.IsActive);

            if (!string.IsNullOrWhiteSpace(category) && category != "All")
                query = query.Where(p => p.Category == category);

            var products = query
                .OrderBy(p => p.Category)
                .ThenBy(p => p.PointsCost)
                .ToList()
                .Select(p => new
                {
                    id = p.CafeProductId,
                    name = p.Name,
                    description = p.Description,
                    emoji = p.Emoji,
                    imagePath = p.ImagePath,
                    category = p.Category,
                    pointsCost = p.PointsCost,
                    randValue = p.RandValue.ToString("F2"),
                    inStock = p.InStock
                }).ToList();

            // Distinct categories for filter tabs
            var categories = db.CafeProducts
                .Where(p => p.IsActive)
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            return Json(new
            {
                success = true,
                products,
                categories,
                total = products.Count
            }, JsonRequestBehavior.AllowGet);
        }

        // ============================================================
        // SEED — Run once to populate default SA gym café products
        // GET: /Cafe/Seed
        // Safe to call multiple times — does nothing if products exist
        // ============================================================
        public ActionResult Seed()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Forms");

            if (db.CafeProducts.Any())
                return Content("Products already seeded. Nothing added.");

            var products = new[]
            {
                // ── Smoothies ──
                new CafeProduct { Name = "Berry Blast Smoothie",     Description = "Blueberries, raspberries, strawberries, banana, frozen yoghurt, honey",   Emoji = "🫐", Category = "Smoothie",   PointsCost = 250, RandValue = 45m  },
                new CafeProduct { Name = "Mango Tropical Smoothie",  Description = "Mango, pineapple, coconut milk, banana, honey",                            Emoji = "🥭", Category = "Smoothie",   PointsCost = 250, RandValue = 45m  },
                new CafeProduct { Name = "Green Power Smoothie",     Description = "Spinach, kale, apple, cucumber, lemon, ginger, coconut water",             Emoji = "🥬", Category = "Smoothie",   PointsCost = 280, RandValue = 50m  },

                // ── Shakes ──
                new CafeProduct { Name = "Protein Berry Shake",      Description = "Whey protein, strawberries, banana, almond milk, BCAA blend",              Emoji = "💪", Category = "Shake",      PointsCost = 350, RandValue = 65m  },
                new CafeProduct { Name = "Peanut Butter Shake",      Description = "Whey protein, peanut butter, banana, oat milk, honey",                    Emoji = "🥜", Category = "Shake",      PointsCost = 350, RandValue = 65m  },
                new CafeProduct { Name = "Chocolate Recovery Shake", Description = "Whey protein, cocoa, banana, milk, L-glutamine, electrolytes",             Emoji = "🍫", Category = "Shake",      PointsCost = 400, RandValue = 75m  },

                // ── Snacks ──
                new CafeProduct { Name = "Beef Biltong (50g)",       Description = "100% beef, lightly spiced. High protein, low carb SA favourite",           Emoji = "🥩", Category = "Snack",      PointsCost = 150, RandValue = 30m  },
                new CafeProduct { Name = "Mixed Nuts & Trail Mix",   Description = "Almonds, cashews, macadamia, raisins, sunflower seeds",                    Emoji = "🥜", Category = "Snack",      PointsCost = 120, RandValue = 22m  },
                new CafeProduct { Name = "Protein Bar",              Description = "20g protein, low sugar — chocolate or vanilla",                             Emoji = "🍫", Category = "Snack",      PointsCost = 180, RandValue = 35m  },
                new CafeProduct { Name = "Banana",                   Description = "Fresh banana — natural energy boost pre or post workout",                   Emoji = "🍌", Category = "Snack",      PointsCost = 50,  RandValue = 8m   },
                new CafeProduct { Name = "Acai Energy Ball (3pk)",   Description = "Dates, acai, oats, chia seeds, coconut flakes. Vegan & gluten free",       Emoji = "🟣", Category = "Snack",      PointsCost = 160, RandValue = 30m  },

                // ── Drinks ──
                new CafeProduct { Name = "Still Water (500ml)",      Description = "Chilled still water",                                                       Emoji = "💧", Category = "Drink",      PointsCost = 40,  RandValue = 10m  },
                new CafeProduct { Name = "Sparkling Water (500ml)",  Description = "Chilled sparkling water",                                                   Emoji = "🫧", Category = "Drink",      PointsCost = 50,  RandValue = 12m  },
                new CafeProduct { Name = "Americano Coffee",         Description = "Double shot espresso, hot or iced",                                         Emoji = "☕", Category = "Drink",      PointsCost = 120, RandValue = 25m  },
                new CafeProduct { Name = "Rooibos Tea",              Description = "SA favourite — hot or iced with honey",                                     Emoji = "🍵", Category = "Drink",      PointsCost = 80,  RandValue = 18m  },
                new CafeProduct { Name = "Energy Drink (250ml)",     Description = "Pre-workout energy boost, sugar-free options available",                    Emoji = "⚡", Category = "Drink",      PointsCost = 130, RandValue = 28m  },

                // ── Meals ──
                new CafeProduct { Name = "Chicken Protein Wrap",     Description = "Grilled chicken, avo, baby spinach, tomato, whole wheat wrap",              Emoji = "🌯", Category = "Meal",       PointsCost = 500, RandValue = 95m  },
                new CafeProduct { Name = "Grilled Chicken Bowl",     Description = "Brown rice, grilled chicken, broccoli, carrot, sesame dressing",            Emoji = "🍗", Category = "Meal",       PointsCost = 550, RandValue = 105m },
                new CafeProduct { Name = "Avo & Egg Breakfast",      Description = "Poached eggs, smashed avo, sourdough toast, tomato relish",                 Emoji = "🥑", Category = "Meal",       PointsCost = 600, RandValue = 115m },
                new CafeProduct { Name = "Superfood Salad Bowl",     Description = "Quinoa, roasted veg, avo, chia seeds, vegan nut butter dressing",           Emoji = "🥗", Category = "Meal",       PointsCost = 480, RandValue = 90m  },

                // ── Supplements ──
                new CafeProduct { Name = "Creatine Shot (5g)",       Description = "Pure creatine monohydrate sachet — mix with water",                         Emoji = "⚗️", Category = "Supplement", PointsCost = 100, RandValue = 20m  },
                new CafeProduct { Name = "BCAA Sachet",              Description = "Branch chain amino acids — watermelon or lemon flavour",                    Emoji = "🧪", Category = "Supplement", PointsCost = 120, RandValue = 25m  },
                new CafeProduct { Name = "Collagen Boost Shot",      Description = "Marine collagen, vitamin C, hyaluronic acid — 30ml shot",                  Emoji = "✨", Category = "Supplement", PointsCost = 200, RandValue = 40m  },
            };

            db.CafeProducts.AddRange(products);
            db.SaveChanges();

            return Content($"✅ {products.Length} café products seeded successfully. Visit /Cafe to see them.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}