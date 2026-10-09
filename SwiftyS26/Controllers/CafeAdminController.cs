using SwiftyS26.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SwiftyS26.Controllers
{
    public class CafeAdminController : Controller
    {
        private GymHubDB db = new GymHubDB();

        // ── Auth helper ──────────────────────────────────────────────
        private bool IsAdminLoggedIn() => Session["LoggedInAdmin"] != null;

        private ActionResult RequireAdmin()
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");
            return null;
        }

        // ============================================================
        // MAIN DASHBOARD VIEW
        // GET: /CafeAdmin
        // ============================================================
        public ActionResult Index()
        {
            var auth = RequireAdmin(); if (auth != null) return auth;

            ViewBag.AdminName = Session["LoggedInAdminName"];
            ViewBag.AdminRole = Session["LoggedInAdminRole"];
            return View();
        }

        // ============================================================
        // PRODUCTS — GET ALL
        // GET: /CafeAdmin/GetProducts
        // ============================================================
        public ActionResult GetProducts()
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            var products = db.CafeProducts
                .OrderBy(p => p.Category)
                .ThenBy(p => p.Name)
                .ToList()
                .Select(p => new
                {
                    cafeProductId = p.CafeProductId,
                    name = p.Name,
                    description = p.Description,
                    category = p.Category,
                    pointsCost = p.PointsCost,
                    randValue = p.RandValue.ToString("F2"),
                    isActive = p.IsActive,
                    inStock = p.InStock,
                    imagePath = p.ImagePath,
                    emoji = p.Emoji,
                    createdAt = p.CreatedAt.ToString("dd MMM yyyy")
                }).ToList();

            return Json(new
            {
                success = true,
                products,
                totalCount = products.Count,
                activeCount = products.Count(p => p.isActive),
                inStockCount = products.Count(p => p.inStock)
            }, JsonRequestBehavior.AllowGet);
        }

        // ============================================================
        // PRODUCTS — ADD
        // POST: /CafeAdmin/AddProduct
        // ============================================================
        [HttpPost]
        public ActionResult AddProduct(string name, string description, string category,
            int pointsCost, decimal randValue, bool isActive, bool inStock,
            string emoji, HttpPostedFileBase imageFile)
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            if (string.IsNullOrWhiteSpace(name))
                return Json(new { success = false, message = "Product name is required." });

            if (string.IsNullOrWhiteSpace(category))
                return Json(new { success = false, message = "Category is required." });

            if (pointsCost <= 0)
                return Json(new { success = false, message = "Points cost must be greater than zero." });

            string imagePath = SaveImage(imageFile, null, out string imgError);
            if (imgError != null) return Json(new { success = false, message = imgError });

            var product = new CafeProduct
            {
                Name = name.Trim(),
                Description = description?.Trim(),
                Category = category.Trim(),
                PointsCost = pointsCost,
                RandValue = randValue,
                IsActive = isActive,
                InStock = inStock,
                Emoji = emoji?.Trim(),
                ImagePath = imagePath,
                CreatedAt = DateTime.Now,
                LastUpdated = DateTime.Now
            };

            db.CafeProducts.Add(product);
            db.SaveChanges();

            return Json(new
            {
                success = true,
                message = $"✅ '{product.Name}' added successfully.",
                cafeProductId = product.CafeProductId,
                imagePath = product.ImagePath
            });
        }

        // ============================================================
        // PRODUCTS — EDIT
        // POST: /CafeAdmin/EditProduct
        // ============================================================
        [HttpPost]
        public ActionResult EditProduct(int cafeProductId, string name, string description,
            string category, int pointsCost, decimal randValue, bool isActive, bool inStock,
            string emoji, HttpPostedFileBase imageFile)
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            var product = db.CafeProducts.Find(cafeProductId);
            if (product == null)
                return Json(new { success = false, message = "Product not found." });

            if (string.IsNullOrWhiteSpace(name))
                return Json(new { success = false, message = "Product name is required." });

            if (pointsCost <= 0)
                return Json(new { success = false, message = "Points cost must be greater than zero." });

            if (imageFile != null && imageFile.ContentLength > 0)
            {
                string newPath = SaveImage(imageFile, product.ImagePath, out string imgError);
                if (imgError != null) return Json(new { success = false, message = imgError });
                product.ImagePath = newPath;
            }

            product.Name = name.Trim();
            product.Description = description?.Trim();
            product.Category = category.Trim();
            product.PointsCost = pointsCost;
            product.RandValue = randValue;
            product.IsActive = isActive;
            product.InStock = inStock;
            product.Emoji = emoji?.Trim();
            product.LastUpdated = DateTime.Now;

            db.Entry(product).State = EntityState.Modified;
            db.SaveChanges();

            return Json(new
            {
                success = true,
                message = $"✅ '{product.Name}' updated successfully.",
                imagePath = product.ImagePath
            });
        }

        // ============================================================
        // PRODUCTS — TOGGLE (active / stock)
        // POST: /CafeAdmin/ToggleProduct
        // ============================================================
        [HttpPost]
        public ActionResult ToggleProduct(int cafeProductId, string toggleType)
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            var product = db.CafeProducts.Find(cafeProductId);
            if (product == null)
                return Json(new { success = false, message = "Product not found." });

            if (toggleType == "active")
            {
                product.IsActive = !product.IsActive;
                product.LastUpdated = DateTime.Now;
                db.Entry(product).State = EntityState.Modified;
                db.SaveChanges();
                return Json(new
                {
                    success = true,
                    message = $"'{product.Name}' is now {(product.IsActive ? "visible to members" : "hidden from members")}.",
                    isActive = product.IsActive
                });
            }

            if (toggleType == "stock")
            {
                product.InStock = !product.InStock;
                product.LastUpdated = DateTime.Now;
                db.Entry(product).State = EntityState.Modified;
                db.SaveChanges();
                return Json(new
                {
                    success = true,
                    message = $"'{product.Name}' is now marked as {(product.InStock ? "In Stock" : "Out of Stock")}.",
                    inStock = product.InStock
                });
            }

            return Json(new { success = false, message = "Invalid toggle type." });
        }

        // ============================================================
        // PRODUCTS — DELETE
        // POST: /CafeAdmin/DeleteProduct
        // ============================================================
        [HttpPost]
        public ActionResult DeleteProduct(int cafeProductId)
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            var product = db.CafeProducts.Find(cafeProductId);
            if (product == null)
                return Json(new { success = false, message = "Product not found." });

            bool hasRedemptions = db.CafeRedemptions.Any(r => r.CafeProductId == cafeProductId);
            if (hasRedemptions)
                return Json(new
                {
                    success = false,
                    message = "Cannot delete — this product has redemption history. Deactivate it instead."
                });

            if (!string.IsNullOrWhiteSpace(product.ImagePath))
            {
                string oldPath = Server.MapPath(product.ImagePath);
                if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
            }

            string productName = product.Name;
            db.CafeProducts.Remove(product);
            db.SaveChanges();

            return Json(new { success = true, message = $"'{productName}' deleted successfully." });
        }

        // ============================================================
        // ORDERS — GET ALL
        // GET: /CafeAdmin/GetOrders
        // ============================================================
        public ActionResult GetOrders(string status = "all", string from = null, string to = null)
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            var query = db.CafeRedemptions.Include("Member").Include("Product").AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && status != "all")
                query = query.Where(r => r.Status == status);

            if (DateTime.TryParse(from, out DateTime fromDate))
                query = query.Where(r => r.RedeemedAt >= fromDate);

            if (DateTime.TryParse(to, out DateTime toDate))
            {
                toDate = toDate.Date.AddDays(1).AddTicks(-1);
                query = query.Where(r => r.RedeemedAt <= toDate);
            }

            var orders = query.OrderByDescending(r => r.RedeemedAt).ToList();

            var data = orders.Select(r => new
            {
                redemptionId = r.CafeRedemptionId,
                memberName = r.Member != null ? r.Member.FullName : "—",
                memberNumber = r.Member != null ? r.Member.MemberNumber : "—",
                productName = r.Product != null ? r.Product.Name : "—",
                category = r.Product != null ? r.Product.Category : "—",
                pointsSpent = r.PointsSpent,
                randValue = r.RandValue.ToString("F2"),
                status = r.Status,
                redeemedAt = r.RedeemedAt.ToString("dd MMM yyyy HH:mm"),
                collectedAt = r.CollectedAt.HasValue ? r.CollectedAt.Value.ToString("dd MMM yyyy HH:mm") : null,
                notes = r.Notes
            }).ToList();

            decimal totalRevenue = orders.Where(r => r.Status == "Collected").Sum(r => (decimal?)r.RandValue) ?? 0;
            int totalPoints = orders.Where(r => r.Status == "Collected").Sum(r => (int?)r.PointsSpent) ?? 0;

            return Json(new
            {
                success = true,
                orders = data,
                totalCount = orders.Count,
                pendingCount = orders.Count(r => r.Status == "Pending"),
                readyCount = orders.Count(r => r.Status == "Ready"),
                collectedCount = orders.Count(r => r.Status == "Collected"),
                cancelledCount = orders.Count(r => r.Status == "Cancelled"),
                totalRevenue = totalRevenue.ToString("F2"),
                totalPoints
            }, JsonRequestBehavior.AllowGet);
        }

        // ============================================================
        // ORDERS — UPDATE STATUS
        // POST: /CafeAdmin/UpdateOrderStatus
        // ============================================================
        [HttpPost]
        public ActionResult UpdateOrderStatus(int redemptionId, string newStatus, string notes)
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            var order = db.CafeRedemptions.Include("Member").Include("Product")
                          .FirstOrDefault(r => r.CafeRedemptionId == redemptionId);

            if (order == null)
                return Json(new { success = false, message = "Order not found." });

            var validStatuses = new[] { "Pending", "Ready", "Collected", "Cancelled" };
            if (!validStatuses.Contains(newStatus))
                return Json(new { success = false, message = "Invalid status." });

            order.Status = newStatus;
            if (newStatus == "Collected") order.CollectedAt = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(notes)) order.Notes = notes.Trim();

            db.SaveChanges();

            if (newStatus == "Cancelled" && order.Member != null)
            {
                var loyalty = db.LoyaltyAccounts.FirstOrDefault(l => l.MemberId == order.MemberId);
                if (loyalty != null)
                {
                    loyalty.CurrentPoints += order.PointsSpent;
                    loyalty.LastUpdated = DateTime.Now;

                    db.PointsTransactions.Add(new PointsTransaction
                    {
                        MemberId = order.MemberId,
                        TransactionType = "Refunded",
                        Points = order.PointsSpent,
                        Description = $"Café order cancelled — {order.Product?.Name ?? "item"} refunded",
                        CreatedAt = DateTime.Now
                    });
                    db.SaveChanges();
                }
            }

            return Json(new
            {
                success = true,
                message = $"Order status updated to {newStatus}.",
                newStatus,
                memberName = order.Member?.FullName ?? "—"
            });
        }

        public ActionResult CafeAdmin()
        {

            return View();
        }

        // ============================================================
        // REVENUE SUMMARY
        // GET: /CafeAdmin/GetRevenueSummary
        // ============================================================
        public ActionResult GetRevenueSummary()
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            var now = DateTime.Now;
            var allOrders = db.CafeRedemptions
                              .Include("Product")
                              .Where(r => r.Status == "Collected")
                              .ToList();

            var thisMonth = allOrders.Where(r =>
                r.RedeemedAt.Month == now.Month &&
                r.RedeemedAt.Year == now.Year).ToList();

            var lastMonth = allOrders.Where(r =>
                r.RedeemedAt.Month == now.AddMonths(-1).Month &&
                r.RedeemedAt.Year == now.AddMonths(-1).Year).ToList();

            var topProducts = thisMonth
                .GroupBy(r => r.Product?.Name ?? "Unknown")
                .Select(g => new
                {
                    productName = g.Key,
                    orderCount = g.Count(),
                    revenue = g.Sum(r => r.RandValue).ToString("F2"),
                    points = g.Sum(r => r.PointsSpent)
                })
                .OrderByDescending(x => x.orderCount)
                .Take(5)
                .ToList();

            var byCategory = allOrders
                .GroupBy(r => r.Product?.Category ?? "Other")
                .Select(g => new
                {
                    category = g.Key,
                    revenue = g.Sum(r => r.RandValue).ToString("F2"),
                    orderCount = g.Count()
                })
                .OrderByDescending(x => x.orderCount)
                .ToList();

            return Json(new
            {
                success = true,
                allTimeRevenue = allOrders.Sum(r => (decimal?)r.RandValue)?.ToString("F2") ?? "0.00",
                allTimeOrders = allOrders.Count,
                allTimePoints = allOrders.Sum(r => (int?)r.PointsSpent) ?? 0,
                thisMonthRevenue = thisMonth.Sum(r => (decimal?)r.RandValue)?.ToString("F2") ?? "0.00",
                thisMonthOrders = thisMonth.Count,
                lastMonthRevenue = lastMonth.Sum(r => (decimal?)r.RandValue)?.ToString("F2") ?? "0.00",
                lastMonthOrders = lastMonth.Count,
                topProducts,
                byCategory
            }, JsonRequestBehavior.AllowGet);
        }

        // ============================================================
        // PRIVATE HELPER — Save uploaded image
        // ============================================================
        private string SaveImage(HttpPostedFileBase file, string oldPath, out string error)
        {
            error = null;
            if (file == null || file.ContentLength == 0) return null;

            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = System.IO.Path.GetExtension(file.FileName).ToLower();

            if (!allowed.Contains(ext)) { error = "Only JPG, PNG or WEBP images are allowed."; return null; }
            if (file.ContentLength > 2 * 1024 * 1024) { error = "Image must be under 2MB."; return null; }

            if (!string.IsNullOrWhiteSpace(oldPath))
            {
                string op = Server.MapPath(oldPath);
                if (System.IO.File.Exists(op)) System.IO.File.Delete(op);
            }

            string folder = Server.MapPath("~/Content/CafeImages/");
            if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);

            string fileName = $"{Guid.NewGuid()}{ext}";
            file.SaveAs(System.IO.Path.Combine(folder, fileName));
            return $"/Content/CafeImages/{fileName}";
        }

        // ============================================================
        // COLLECTIONS — VERIFY CODE
        // POST: /CafeAdmin/VerifyCollectionCode
        // ============================================================
        [HttpPost]
        public ActionResult VerifyCollectionCode(string code)
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            if (string.IsNullOrWhiteSpace(code))
                return Json(new { success = false, message = "Please enter a collection code." });

            code = code.Trim().ToUpper();

            var order = db.CafeRedemptions
                .Include("Member")
                .Include("Product")
                .FirstOrDefault(r => r.CollectionCode == code);

            if (order == null)
                return Json(new { success = false, message = "❌ Invalid code. No order found." });

            // ── Already collected ──
            if (order.Status == "Collected")
                return Json(new
                {
                    success = false,
                    alreadyCollected = true,
                    message = "❌ This order has already been collected.",
                    memberName = order.Member?.FullName ?? "—",
                    productName = order.Product?.Name ?? "—",
                    collectedAt = order.CollectedAt.HasValue
                        ? order.CollectedAt.Value.ToString("dd MMM yyyy HH:mm") : "—"
                });

            // ── Cancelled ──
            if (order.Status == "Cancelled")
                return Json(new
                {
                    success = false,
                    message = "❌ This order was cancelled.",
                    memberName = order.Member?.FullName ?? "—",
                    productName = order.Product?.Name ?? "—"
                });

            // ── Check expiry — QR valid for 30 minutes ──
            if ((DateTime.Now - order.RedeemedAt).TotalMinutes > 30)
                return Json(new
                {
                    success = false,
                    expired = true,
                    message = "⏰ This order QR has expired (30 min limit). Member must re-redeem.",
                    memberName = order.Member?.FullName ?? "—",
                    productName = order.Product?.Name ?? "—"
                });

            // ── Valid — return order details for admin to confirm ──
            return Json(new
            {
                success = true,
                redemptionId = order.CafeRedemptionId,
                memberName = order.Member?.FullName ?? "—",
                memberNumber = order.Member?.MemberNumber ?? "—",
                productName = order.Product?.Name ?? "—",
                productEmoji = order.Product?.Emoji ?? "☕",
                category = order.Product?.Category ?? "—",
                pointsSpent = order.PointsSpent,
                randValue = order.RandValue.ToString("F2"),
                status = order.Status,
                redeemedAt = order.RedeemedAt.ToString("dd MMM yyyy HH:mm"),
                collectionCode = order.CollectionCode,
                message = "✅ Valid order — ready to confirm collection."
            });
        }

        // ============================================================
        // COLLECTIONS — CONFIRM COLLECTION
        // POST: /CafeAdmin/ConfirmCollection
        // ============================================================
        [HttpPost]
        public ActionResult ConfirmCollection(int redemptionId, string adminNotes)
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            var order = db.CafeRedemptions
                .Include("Member")
                .Include("Product")
                .FirstOrDefault(r => r.CafeRedemptionId == redemptionId);

            if (order == null)
                return Json(new { success = false, message = "Order not found." });

            if (order.Status == "Collected")
                return Json(new { success = false, message = "Already collected." });

            if (order.Status == "Cancelled")
                return Json(new { success = false, message = "Order was cancelled — cannot collect." });

            // ── Final expiry check ──
            if ((DateTime.Now - order.RedeemedAt).TotalMinutes > 30)
                return Json(new
                {
                    success = false,
                    message = "QR has expired. Member must re-redeem the product."
                });

            // ── Confirm ──
            order.Status = "Collected";
            order.CollectedAt = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(adminNotes))
                order.Notes = adminNotes.Trim();

            db.SaveChanges();

            return Json(new
            {
                success = true,
                message = $"✅ Collection confirmed for {order.Member?.FullName ?? "member"}!",
                memberName = order.Member?.FullName ?? "—",
                productName = order.Product?.Name ?? "—",
                collectedAt = order.CollectedAt.Value.ToString("dd MMM yyyy HH:mm")
            });
        }

        // ============================================================
        // COLLECTIONS — GET TODAY'S COLLECTIONS
        // GET: /CafeAdmin/GetCollections
        // ============================================================
        public ActionResult GetCollections()
        {
            if (!IsAdminLoggedIn())
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            var today = DateTime.Today;

            var collections = db.CafeRedemptions
                .Include("Member")
                .Include("Product")
                .Where(r => r.RedeemedAt >= today)
                .OrderByDescending(r => r.RedeemedAt)
                .ToList();

            var data = collections.Select(r => new
            {
                redemptionId = r.CafeRedemptionId,
                memberName = r.Member != null ? r.Member.FullName : "—",
                memberNumber = r.Member != null ? r.Member.MemberNumber : "—",
                productName = r.Product != null ? r.Product.Name : "—",
                productEmoji = r.Product != null ? r.Product.Emoji : "☕",
                pointsSpent = r.PointsSpent,
                randValue = r.RandValue.ToString("F2"),
                collectionCode = r.CollectionCode,
                status = r.Status,
                redeemedAt = r.RedeemedAt.ToString("HH:mm"),
                collectedAt = r.CollectedAt.HasValue
                    ? r.CollectedAt.Value.ToString("HH:mm") : null
            }).ToList();

            return Json(new
            {
                success = true,
                collections = data,
                totalToday = collections.Count,
                pendingCount = collections.Count(r => r.Status == "Pending"),
                collectedCount = collections.Count(r => r.Status == "Collected"),
                cancelledCount = collections.Count(r => r.Status == "Cancelled"),
                revenueToday = collections
                    .Where(r => r.Status == "Collected")
                    .Sum(r => (decimal?)r.RandValue)?.ToString("F2") ?? "0.00"
            }, JsonRequestBehavior.AllowGet);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}