using Newtonsoft.Json.Linq;
using SwiftyS26.Models;
using SwiftyS26.Services;
using SwiftyS26.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;


namespace SwiftyS26.Controllers
{
    public class StoreAdminController : Controller
    {
        private GymHubDB db = new GymHubDB();

        private bool IsStoreStaffLoggedIn() => Session["LoggedInStoreStaff"] != null;
        private bool IsAdminLoggedIn() => Session["LoggedInAdmin"] != null;

        // Both roles can manage the catalogue and fulfil orders — Admin's
        // oversight here is "monitor and intervene," not a hard gate,
        // per the planning document.
        private bool IsAuthorized() => IsStoreStaffLoggedIn() || IsAdminLoggedIn();

        private void EnsureProductHasQr(Product product)
        {
            if (product == null ||
                (!string.IsNullOrWhiteSpace(product.SKU) &&
                 !string.IsNullOrWhiteSpace(product.ProductQrToken) &&
                 !string.IsNullOrWhiteSpace(product.ProductQrBase64)))
                return;

            product.SKU = string.IsNullOrWhiteSpace(product.SKU)
                ? "GH-P" + product.ProductId.ToString("D6")
                : product.SKU;
            product.ProductQrToken = Guid.NewGuid().ToString("N");
            product.ProductQrBase64 = ProcurementQrHelper.GenerateBase64(
                ProcurementQrHelper.CreateProductContent(product.SKU, product.ProductQrToken));
            product.ProductQrGeneratedAt = DateTime.Now;
        }

        private void EnsurePurchaseOrderHasReceivingQr(PurchaseOrder po)
        {
            if (po == null ||
                (!string.IsNullOrWhiteSpace(po.ReceivingQrToken) &&
                 !string.IsNullOrWhiteSpace(po.ReceivingQrBase64)))
                return;

            po.ReceivingQrToken = Guid.NewGuid().ToString("N");
            po.ReceivingQrBase64 = ProcurementQrHelper.GenerateBase64(
                ProcurementQrHelper.CreatePurchaseOrderContent(
                    po.ReferenceNumber, po.ReceivingQrToken));
            po.ReceivingQrGeneratedAt = DateTime.Now;
        }

        // ============================================================
        // PRODUCT MANAGEMENT — GET
        // ============================================================
        public ActionResult Index()
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            var products = db.Products
                .Include(p => p.Promotions)
                .OrderBy(p => p.Category)
                .ThenBy(p => p.Name)
                .ToList();

            ViewBag.LowStockCount = products.Count(p => p.StockStatus == "LowStock");
            ViewBag.OutOfStockCount = products.Count(p => p.StockStatus == "OutOfStock");
            ViewBag.NearExpiryCount = products.Count(p => p.StockStatus == "NearExpiry");
            ViewBag.Categories = ProductHelper.Categories;

            return View(products);
        }

        // ============================================================
        // ADD PRODUCT — POST
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddProduct(string name, string description, string category,
            decimal normalPrice, int quantityOnHand, int reorderLevel, string expiryDate,
            HttpPostedFileBase imageFile)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Product name is required.";
                return RedirectToAction("Index");
            }

            if (!ProductHelper.IsValidCategory(category))
            {
                TempData["Error"] = "Please select a valid category.";
                return RedirectToAction("Index");
            }

            DateTime? expiry = null;
            if (DateTime.TryParse(expiryDate, out DateTime parsedExpiry))
                expiry = parsedExpiry;

            string imagePath = null;
            if (imageFile != null && imageFile.ContentLength > 0)
            {
                var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var ext = System.IO.Path.GetExtension(imageFile.FileName).ToLower();
                if (allowedExt.Contains(ext))
                {
                    var folder = Server.MapPath("~/Content/uploads/products");
                    if (!System.IO.Directory.Exists(folder))
                        System.IO.Directory.CreateDirectory(folder);

                    var fileName = $"{Guid.NewGuid()}{ext}";
                    imageFile.SaveAs(System.IO.Path.Combine(folder, fileName));
                    imagePath = "/Content/uploads/products/" + fileName;
                }
            }

            var product = new Product
            {
                Name = name.Trim(),
                Description = description,
                Category = category,
                NormalPrice = normalPrice,
                QuantityOnHand = quantityOnHand,
                ReorderLevel = reorderLevel > 0 ? reorderLevel : 10,
                ExpiryDate = expiry,
                ImagePath = imagePath,
                IsActive = true
            };

            db.Products.Add(product);
            db.SaveChanges();

            EnsureProductHasQr(product);
            db.SaveChanges();

            TempData["Success"] = $"Product \"{product.Name}\" added.";
            return RedirectToAction("Index");
        }

        // ============================================================
        // EDIT PRODUCT — POST
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditProduct(int productId, string name, string description, string category,
    decimal normalPrice, int quantityOnHand, int reorderLevel, string expiryDate, bool? isActive,
    HttpPostedFileBase imageFile)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            var product = db.Products.Find(productId);
            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction("Index");
            }

            if (!ProductHelper.IsValidCategory(category))
            {
                TempData["Error"] = "Please select a valid category.";
                return RedirectToAction("Index");
            }

            product.Name = name.Trim();
            product.Description = description;
            product.Category = category;
            product.NormalPrice = normalPrice;
            product.QuantityOnHand = quantityOnHand;
            product.ReorderLevel = reorderLevel > 0 ? reorderLevel : 10;
            product.IsActive = isActive ?? false;

            if (DateTime.TryParse(expiryDate, out DateTime parsedExpiry))
                product.ExpiryDate = parsedExpiry;
            else
                product.ExpiryDate = null;

            if (imageFile != null && imageFile.ContentLength > 0)
            {
                var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var ext = System.IO.Path.GetExtension(imageFile.FileName).ToLower();
                if (allowedExt.Contains(ext))
                {
                    var folder = Server.MapPath("~/Content/uploads/products");
                    if (!System.IO.Directory.Exists(folder))
                        System.IO.Directory.CreateDirectory(folder);

                    var fileName = $"{Guid.NewGuid()}{ext}";
                    imageFile.SaveAs(System.IO.Path.Combine(folder, fileName));
                    product.ImagePath = "/Content/uploads/products/" + fileName;
                }
            }

            db.Entry(product).State = EntityState.Modified;
            db.SaveChanges();

            TempData["Success"] = $"Product \"{product.Name}\" updated.";
            return RedirectToAction("Index");
        }

        // ============================================================
        // DEACTIVATE/REACTIVATE PRODUCT — POST (soft delete, matching
        // Equipment's own pattern, since past orders reference this
        // product and must stay valid)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeactivateProduct(int id)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            var product = db.Products.Find(id);
            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction("Index");
            }

            product.IsActive = !product.IsActive;
            db.Entry(product).State = EntityState.Modified;
            db.SaveChanges();

            TempData["Success"] = $"Product \"{product.Name}\" {(product.IsActive ? "reactivated" : "deactivated")}.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult ProductQr(int id)
        {
            if (!IsAuthorized()) return RedirectToAction("Login", "Admin");
            var product = db.Products.Find(id);
            if (product == null) return HttpNotFound();

            EnsureProductHasQr(product);
            db.SaveChanges();
            return View(product);
        }

        [HttpGet]
        public ActionResult Orders()
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            StoreOrderHelper.CheckUncollectedOrders(db);

            var orders = db.StoreOrders
                .Include(o => o.Member)
                .Include(o => o.Items.Select(i => i.Product))
                .Where(o =>
                    o.Status == "Paid" ||
                    o.Status == "Preparing" ||
                    o.Status == "ReadyForPickup")
                .OrderBy(o => o.CreatedAt)
                .ToList();

            bool qrChangesMade = false;

            // Backfill internal readiness QR tokens for existing paid orders.
            foreach (var order in orders.Where(o =>
                o.Status == "Paid" ||
                o.Status == "Preparing"))
            {
                if (string.IsNullOrWhiteSpace(order.ReadyQrToken) ||
                    order.ReadyQrUsedAt.HasValue)
                {
                    StoreOrderHelper.CreateReadyQr(order);
                    db.Entry(order).State = EntityState.Modified;
                    qrChangesMade = true;
                }
            }

            if (qrChangesMade)
            {
                try
                {
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[READY QR GENERATION ERROR] " + ex);

                    TempData["Error"] =
                        "Some order preparation QR codes could not be generated.";
                }
            }

            // Generate images for display without saving large images to the database.
            var readyQrImages = orders
                .Where(o =>
                    (o.Status == "Paid" || o.Status == "Preparing") &&
                    !string.IsNullOrWhiteSpace(o.ReadyQrToken) &&
                    !o.ReadyQrUsedAt.HasValue)
                .ToDictionary(
                    o => o.StoreOrderId,
                    o => StoreOrderHelper.GenerateReadyQrBase64(o));

            ViewBag.ReadyQrImages = readyQrImages;

            ViewBag.PreparingCount = orders.Count(o =>
                o.Status == "Paid" ||
                o.Status == "Preparing");

            ViewBag.ReadyCount = orders.Count(o =>
                o.Status == "ReadyForPickup");

            ViewBag.ApproachingDeadlineCount = orders.Count(o =>
                o.Status == "ReadyForPickup" &&
                o.PickupDeadline.HasValue &&
                o.PickupDeadline.Value > DateTime.Now &&
                o.PickupDeadline.Value <= DateTime.Now.AddHours(24));

            ViewBag.UncollectedReviewCount = db.StoreOrders.Count(o =>
                o.Status == "UncollectedPendingReview");

            return View(orders);
        }

        // ============================================================
        // MARK READY AFTER VERIFIED PREPARATION QR
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkReady(int orderId)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            int? verifiedOrderId =
                Session["VerifiedReadyOrderId"] as int?;

            string verifiedToken =
                Session["VerifiedReadyQrToken"] as string;

            if (!verifiedOrderId.HasValue ||
                verifiedOrderId.Value != orderId ||
                string.IsNullOrWhiteSpace(verifiedToken))
            {
                TempData["Error"] =
                    "The preparation QR code must be scanned before marking the order ready.";

                return RedirectToAction("ScanReadyOrder");
            }

            var order = db.StoreOrders
                .Include(o => o.Member)
                .Include(o => o.Items.Select(i => i.Product))
                .FirstOrDefault(o => o.StoreOrderId == orderId);

            if (order == null)
            {
                ClearReadyQrVerification();

                TempData["Error"] = "Order not found.";
                return RedirectToAction("Orders");
            }

            if (order.Status != "Paid" &&
                order.Status != "Preparing")
            {
                ClearReadyQrVerification();

                TempData["Error"] =
                    "Only a paid or preparing order can be marked ready.";

                return RedirectToAction("Orders");
            }

            if (order.Member == null)
            {
                ClearReadyQrVerification();

                TempData["Error"] =
                    "The member linked to this order could not be found.";

                return RedirectToAction("Orders");
            }

            if (!order.IsPickupStockReserved)
            {
                ClearReadyQrVerification();

                TempData["Error"] =
                    "This order does not have an active stock reservation. " +
                    "Resolve the inventory issue before marking it ready.";

                return RedirectToAction("Orders");
            }

            if (order.ReadyQrUsedAt.HasValue ||
                string.IsNullOrWhiteSpace(order.ReadyQrToken) ||
                !string.Equals(
                    order.ReadyQrToken,
                    verifiedToken,
                    StringComparison.Ordinal))
            {
                ClearReadyQrVerification();

                TempData["Error"] =
                    "The QR verification is invalid or has already been used.";

                return RedirectToAction("ScanReadyOrder");
            }

            DateTime now = DateTime.Now;

            order.Status = "ReadyForPickup";
            order.ReadyAt = now;
            order.PickupDeadline = now.AddDays(5);

            order.FirstReminderSentAt = null;
            order.FinalReminderSentAt = null;

            order.IsPickupStockReserved = true;

            order.UncollectedAt = null;
            order.UncollectedResolutionStatus = null;
            order.UncollectedResolutionNotes = null;
            order.UncollectedResolvedBy = null;
            order.UncollectedResolvedAt = null;

            // Invalidate the internal preparation QR after successful use.
            StoreOrderHelper.InvalidateReadyQr(order);

            // Generate the separate member collection QR.
            string collectionQrContent =
                StoreOrderHelper.GenerateOrderQrContent(
                    order.ReferenceNumber,
                    order.Member.MemberNumber);

            order.OrderQrBase64 =
                StoreOrderHelper.GenerateOrderQrBase64(
                    collectionQrContent);

            string performedByRole =
                Session["LoggedInAdmin"] != null
                    ? "Admin"
                    : "StoreStaff";

            string performedBy =
                Session["LoggedInAdmin"]?.ToString() ??
                Session["LoggedInStoreStaff"]?.ToString() ??
                "Staff";

            db.StoreOrderCollectionEvents.Add(
                new StoreOrderCollectionEvent
                {
                    StoreOrderId = order.StoreOrderId,
                    EventType = "ReadyForPickup",
                    Description =
                        "The internal preparation QR was verified and the order " +
                        "was marked ready for pickup. Collection deadline: " +
                        order.PickupDeadline.Value.ToString(
                            "dd MMM yyyy HH:mm") +
                        ".",
                    PerformedByRole = performedByRole,
                    PerformedBy = performedBy,
                    CreatedAt = now
                });

            db.Entry(order).State = EntityState.Modified;

            try
            {
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[MARK READY DATABASE ERROR] " + ex);

                TempData["Error"] =
                    "The order could not be marked ready. No changes were saved.";

                return RedirectToAction(
                    "ConfirmOrderReady",
                    new { id = orderId });
            }

            ClearReadyQrVerification();

            try
            {
                new NotificationService(db).Notify(
                    "Member",
                    order.MemberId,
                    "Order Ready for Pickup",
                    "Your order " +
                    order.ReferenceNumber +
                    " is ready. Please collect it before " +
                    order.PickupDeadline.Value.ToString(
                        "dd MMM yyyy HH:mm") +
                    ".",
                    "Success",
                    "/Store/OrderConfirmation/" +
                    order.StoreOrderId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[READY NOTIFICATION ERROR] " + ex);
            }

            try
            {
                ProfessionalStoreEmailService.SendOrderReady(
                    order.Member.Email,
                    order.Member.FullName,
                    order.ReferenceNumber,
                    order.OrderQrBase64);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[READY EMAIL ERROR] " + ex);
            }

            TempData["Success"] =
                "Order " +
                order.ReferenceNumber +
                " was marked ready. " +
                order.Member.FullName +
                " has been notified.";

            return RedirectToAction("Orders");
        }

        // ============================================================
        // SCAN FOR PICKUP — GET, renders the camera scanner page
        // ============================================================
        public ActionResult ScanPickup()
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            return View();
        }
        // ============================================================
        // SCAN ORDER PREPARATION QR
        // ============================================================
        [HttpGet]
        public ActionResult ScanReadyOrder()
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            // Clear any result left by a previous scan.
            Session.Remove("VerifiedReadyOrderId");
            Session.Remove("VerifiedReadyQrToken");

            return View();
        }
        // ============================================================
        // VERIFY ORDER PREPARATION QR
        // The scan only verifies the order. It does not change status.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyReadyQr(string qrData)
        {
            if (!IsAuthorized())
            {
                return Json(new
                {
                    success = false,
                    sessionExpired = true,
                    message = "Your session has expired. Please sign in again."
                });
            }

            if (string.IsNullOrWhiteSpace(qrData))
            {
                return Json(new
                {
                    success = false,
                    message = "No QR code data was received."
                });
            }

            qrData = qrData.Trim();

            if (!StoreOrderHelper.IsReadyQrContent(qrData))
            {
                return Json(new
                {
                    success = false,
                    message =
                        "This is not a GymHub order preparation QR code."
                });
            }

            string referenceNumber =
                StoreOrderHelper.ExtractQrField(qrData, "Ref:");

            string token =
                StoreOrderHelper.ExtractQrField(qrData, "Token:");

            if (string.IsNullOrWhiteSpace(referenceNumber) ||
                string.IsNullOrWhiteSpace(token))
            {
                return Json(new
                {
                    success = false,
                    message =
                        "The QR code is incomplete or has an invalid format."
                });
            }

            var order = db.StoreOrders
                .Include(o => o.Member)
                .FirstOrDefault(o =>
                    o.ReferenceNumber == referenceNumber);

            if (order == null)
            {
                return Json(new
                {
                    success = false,
                    message = "No order matches this QR code."
                });
            }

            if (order.Status != "Paid" &&
                order.Status != "Preparing")
            {
                string message;

                if (order.Status == "ReadyForPickup")
                {
                    message =
                        "This order has already been marked ready for pickup.";
                }
                else if (order.Status == "Collected")
                {
                    message =
                        "This order has already been collected.";
                }
                else
                {
                    message =
                        "This order cannot be marked ready from its current status.";
                }

                return Json(new
                {
                    success = false,
                    message = message
                });
            }

            if (order.ReadyQrUsedAt.HasValue ||
                string.IsNullOrWhiteSpace(order.ReadyQrToken))
            {
                return Json(new
                {
                    success = false,
                    message =
                        "This preparation QR code is no longer active."
                });
            }

            bool tokenMatches = string.Equals(
                order.ReadyQrToken,
                token,
                StringComparison.Ordinal);

            if (!tokenMatches)
            {
                return Json(new
                {
                    success = false,
                    message =
                        "The preparation QR token could not be verified."
                });
            }

            if (!order.IsPickupStockReserved)
            {
                return Json(new
                {
                    success = false,
                    message =
                        "This order does not have an active stock reservation."
                });
            }

            // Keep the verification result securely in the current staff session.
            Session["VerifiedReadyOrderId"] = order.StoreOrderId;
            Session["VerifiedReadyQrToken"] = order.ReadyQrToken;

            return Json(new
            {
                success = true,
                message = "Order verified successfully.",
                referenceNumber = order.ReferenceNumber,
                memberName = order.Member != null
                    ? order.Member.FullName
                    : "Unknown member",
                redirectUrl = Url.Action(
                    "ConfirmOrderReady",
                    "StoreAdmin",
                    new { id = order.StoreOrderId })
            });
        }
        // ============================================================
        // CONFIRM ORDER READY
        // Shows the order after a successful QR verification.
        // ============================================================
        [HttpGet]
        public ActionResult ConfirmOrderReady(int id)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            int? verifiedOrderId =
                Session["VerifiedReadyOrderId"] as int?;

            string verifiedToken =
                Session["VerifiedReadyQrToken"] as string;

            if (!verifiedOrderId.HasValue ||
                verifiedOrderId.Value != id ||
                string.IsNullOrWhiteSpace(verifiedToken))
            {
                TempData["Error"] =
                    "Scan the order preparation QR code before opening the confirmation page.";

                return RedirectToAction("ScanReadyOrder");
            }

            var order = db.StoreOrders
                .Include(o => o.Member)
                .Include(o => o.Items.Select(i => i.Product))
                .FirstOrDefault(o => o.StoreOrderId == id);

            if (order == null)
            {
                ClearReadyQrVerification();

                TempData["Error"] = "Order not found.";
                return RedirectToAction("Orders");
            }

            if (order.Status != "Paid" &&
                order.Status != "Preparing")
            {
                ClearReadyQrVerification();

                TempData["Error"] =
                    "This order can no longer be marked ready.";

                return RedirectToAction("Orders");
            }

            if (order.ReadyQrUsedAt.HasValue ||
                string.IsNullOrWhiteSpace(order.ReadyQrToken) ||
                !string.Equals(
                    order.ReadyQrToken,
                    verifiedToken,
                    StringComparison.Ordinal))
            {
                ClearReadyQrVerification();

                TempData["Error"] =
                    "The QR verification is no longer valid. Please scan again.";

                return RedirectToAction("ScanReadyOrder");
            }

            if (!order.IsPickupStockReserved)
            {
                ClearReadyQrVerification();

                TempData["Error"] =
                    "This order does not have an active stock reservation.";

                return RedirectToAction("Orders");
            }

            return View(order);
        }

        // ============================================================
        // VERIFY PICKUP — POST (AJAX)
        //
        // Decodes the order-specific QR and cross-checks BOTH the order
        // reference and the member number it claims to belong to. This
        // resolves unambiguously even when a member has more than one
        // order ready at once — a member-identity-only QR could not.
        // ============================================================
        [HttpPost]
        public JsonResult VerifyPickup(string qrData)
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Session expired. Please log in again." });

            if (string.IsNullOrWhiteSpace(qrData))
                return Json(new { success = false, message = "No QR data received." });

            string reference = StoreOrderHelper.ExtractQrField(qrData, "Ref:");
            string memberNumber = StoreOrderHelper.ExtractQrField(qrData, "MemberNumber:");

            if (string.IsNullOrWhiteSpace(reference))
                return Json(new { success = false, message = "Unrecognised QR code. Please scan a valid GymHub pickup code." });

            var order = db.StoreOrders
                .Include(o => o.Member)
                .FirstOrDefault(o => o.ReferenceNumber == reference);

            if (order == null)
                return Json(new { success = false, message = $"No order found for reference {reference}." });

            // Defense in depth — the code must ALSO match the member it
            // claims to belong to, not just resolve to a valid order.
            if (order.Member == null || order.Member.MemberNumber != memberNumber)
                return Json(new { success = false, message = "This code does not match the order holder." });

            if (order.Status != "ReadyForPickup")
                return Json(new
                {
                    success = false,
                    memberName = order.Member.FullName,
                    message = $"Order {order.ReferenceNumber} is not currently ready for pickup (status: {order.Status})."
                });

            return Json(new
            {
                success = true,
                orderId = order.StoreOrderId,
                referenceNumber = order.ReferenceNumber,
                memberName = order.Member.FullName,
                itemCount = db.StoreOrderItems.Count(i => i.StoreOrderId == order.StoreOrderId)
            });
        }

        // ============================================================
        // COMPLETE STORE ORDER COLLECTION
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkCollected(
            int orderId,
            string signatureData)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            if (string.IsNullOrWhiteSpace(signatureData))
            {
                TempData["Error"] =
                    "A signature is required to confirm collection.";

                return RedirectToAction("ScanPickup");
            }

            var order = db.StoreOrders
                .Include(o => o.Member)
                .FirstOrDefault(o => o.StoreOrderId == orderId);

            if (order == null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToAction("ScanPickup");
            }

            if (order.Status != "ReadyForPickup")
            {
                TempData["Error"] =
                    "This order is not ready for pickup. " +
                    "The QR may not have been verified or the deadline may have expired.";

                return RedirectToAction("ScanPickup");
            }

            if (!order.IsPickupStockReserved)
            {
                TempData["Error"] =
                    "The stock reservation for this order is no longer active.";

                return RedirectToAction("ScanPickup");
            }

            // Prevent collection after the deadline even when the scheduled
            // check has not run yet.
            if (order.PickupDeadline.HasValue &&
                DateTime.Now >= order.PickupDeadline.Value)
            {
                StoreOrderHelper.CheckUncollectedOrders(db);

                TempData["Error"] =
                    "The pickup deadline has passed. " +
                    "The order has been sent for uncollected-order review.";

                return RedirectToAction("ScanPickup");
            }

            string signatureRelativePath;

            // ========================================================
            // VALIDATE AND SAVE SIGNATURE
            // ========================================================

            try
            {
                string base64Data =
                    signatureData.Contains(",")
                        ? signatureData.Substring(
                            signatureData.IndexOf(',') + 1)
                        : signatureData;

                byte[] bytes =
                    Convert.FromBase64String(base64Data);

                // Basic size protection: maximum approximately 2 MB.
                if (bytes.Length == 0 ||
                    bytes.Length > 2 * 1024 * 1024)
                {
                    TempData["Error"] =
                        "The signature image is invalid or too large.";

                    return RedirectToAction("ScanPickup");
                }

                string folder =
                    Server.MapPath(
                        "~/Content/uploads/signatures");

                if (!System.IO.Directory.Exists(folder))
                {
                    System.IO.Directory.CreateDirectory(folder);
                }

                string fileName =
                    Guid.NewGuid().ToString("N") + ".png";

                string absolutePath =
                    System.IO.Path.Combine(
                        folder,
                        fileName);

                System.IO.File.WriteAllBytes(
                    absolutePath,
                    bytes);

                signatureRelativePath =
                    "/Content/uploads/signatures/" +
                    fileName;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[SIGNATURE SAVE ERROR] " + ex);

                TempData["Error"] =
                    "The signature could not be saved. Please try again.";

                return RedirectToAction("ScanPickup");
            }

            int? staffId = null;

            if (Session["LoggedInStoreStaff"] != null)
            {
                staffId =
                    (int)Session["LoggedInStoreStaff"];
            }

            string performedByRole =
                Session["LoggedInAdmin"] != null
                    ? "Admin"
                    : "StoreStaff";

            string performedBy =
                Session["LoggedInAdmin"]?.ToString() ??
                Session["LoggedInStoreStaff"]?.ToString() ??
                "Staff";

            using (var transaction =
                db.Database.BeginTransaction())
            {
                try
                {
                    order.PickupSignaturePath =
                        signatureRelativePath;

                    order.Status = "Collected";
                    order.CollectedAt = DateTime.Now;
                    order.CollectedByStaffId = staffId;

                    // Stock was physically handed to the member.
                    // Do not return it to QuantityOnHand.
                    order.IsPickupStockReserved = false;

                    order.UncollectedResolutionStatus =
                        "Collected";

                    db.StoreOrderCollectionEvents.Add(
                        new StoreOrderCollectionEvent
                        {
                            StoreOrderId =
                                order.StoreOrderId,

                            EventType =
                                "Collected",

                            Description =
                                "Order collected after QR verification " +
                                "and signature capture.",

                            PerformedByRole =
                                performedByRole,

                            PerformedBy =
                                performedBy,

                            CreatedAt =
                                DateTime.Now
                        });

                    db.Entry(order).State =
                        EntityState.Modified;

                    db.SaveChanges();
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();

                    System.Diagnostics.Debug.WriteLine(
                        "[MARK COLLECTED DATABASE ERROR] " + ex);

                    // Remove the signature file if the database operation failed.
                    try
                    {
                        string filePath =
                            Server.MapPath(
                                "~" + signatureRelativePath);

                        if (System.IO.File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                        }
                    }
                    catch
                    {
                        // Do not hide the original database error.
                    }

                    TempData["Error"] =
                        "Collection could not be completed. No database changes were saved.";

                    return RedirectToAction("ScanPickup");
                }
            }

            try
            {
                new NotificationService(db).Notify(
                    "Member",
                    order.MemberId,
                    "Order Collected",
                    $"Order {order.ReferenceNumber} was successfully collected.",
                    "Success",
                    "/Store/OrderConfirmation/" +
                    order.StoreOrderId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[COLLECTION NOTIFICATION ERROR] " + ex);
            }

            TempData["Success"] =
                $"Order {order.ReferenceNumber} was marked as collected.";

            return RedirectToAction("Orders");
        }

        // GET: StoreAdmin/Returns — the review queue
        public ActionResult Returns()
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            ReturnRequestHelper.ReleaseExpiredExchangeReservations(db);

            // Keep the rest of the updated Returns action.

            var requests = db.ReturnRequests
                .Include(r => r.Member)
                .Include(r => r.StoreOrderItem.Product)
                .Include(r => r.StoreOrderItem.StoreOrder)
                .Include(r => r.ExchangeProduct)
                .Where(r =>
                    ReturnRequestHelper.OpenStatuses.Contains(
                        r.Status))
                .OrderBy(r => r.RequestedAt)
                .ToList();

            var history = db.ReturnRequests
                .Include(r => r.Member)
                .Include(r => r.StoreOrderItem.Product)
                .Include(r => r.StoreOrderItem.StoreOrder)
                .Include(r => r.ExchangeProduct)
                .Where(r =>
                    !ReturnRequestHelper.OpenStatuses.Contains(
                        r.Status))
                .OrderByDescending(r =>
                    r.ExchangeCompletedAt ??
                    r.RefundProcessedAt ??
                    r.DecidedAt ??
                    r.RequestedAt)
                .Take(50)
                .ToList();

            ViewBag.History = history;
            ViewBag.IsAdmin = IsAdminLoggedIn();

            return View(requests);
        }
        private void ClearReadyQrVerification()
        {
            Session.Remove("VerifiedReadyOrderId");
            Session.Remove("VerifiedReadyQrToken");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RegenerateReadyQr(int orderId)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            var order = db.StoreOrders
                .FirstOrDefault(o => o.StoreOrderId == orderId);

            if (order == null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToAction("Orders");
            }

            if (order.Status != "Paid" &&
                order.Status != "Preparing")
            {
                TempData["Error"] =
                    "Only a paid or preparing order can receive a new preparation QR code.";

                return RedirectToAction("Orders");
            }

            StoreOrderHelper.CreateReadyQr(order);
            db.Entry(order).State = EntityState.Modified;

            try
            {
                db.SaveChanges();

                TempData["Success"] =
                    "A new preparation QR code was generated for order " +
                    order.ReferenceNumber +
                    ".";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[REGENERATE READY QR ERROR] " + ex);

                TempData["Error"] =
                    "The preparation QR code could not be regenerated.";
            }

            return RedirectToAction("Orders");
        }

        // POST: StoreAdmin/ReviewReturn — StaffStaff (or Admin) leaves a
        // RECOMMENDATION. This does NOT decide the outcome — it just moves
        // the request into the admin's queue with staff's input attached.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReviewReturn(int returnRequestId, string recommendation, string reviewNotes)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            var request = db.ReturnRequests.Find(returnRequestId);
            if (request == null)
            {
                TempData["Error"] = "Return request not found.";
                return RedirectToAction("Returns");
            }

            if (request.Status != "Pending")
            {
                TempData["Error"] = "This request has already been reviewed.";
                return RedirectToAction("Returns");
            }

            request.Status = "PendingAdminApproval";
            request.StaffReviewedAt = DateTime.Now;
            request.StaffReviewNotes = reviewNotes;
            request.StaffRecommendation = recommendation;

            if (IsStoreStaffLoggedIn())
                request.StaffReviewedByStoreStaffId = (int)Session["LoggedInStoreStaff"];

            db.Entry(request).State = EntityState.Modified;
            db.SaveChanges();

            try
            {
                new NotificationService(db).NotifyAdmin(
                    "Return Ready for Your Decision",
                    $"Return {request.ReferenceNumber} reviewed by staff — recommendation: {recommendation}. Needs your approval.",
                    "Info",
                    "/StoreAdmin/Returns");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }

            TempData["Success"] = $"Return {request.ReferenceNumber} sent to admin for a decision.";
            return RedirectToAction("Returns");
        }

        // ============================================================
        // POST: StoreAdmin/ApproveReturn
        // ADMIN ONLY
        //
        // Refund:
        //   - Processes the Paystack refund.
        //   - Restocks only when Admin selects Restock.
        //
        // Exchange:
        //   - Reserves replacement inventory.
        //   - Same-product exchange has no price difference.
        //   - Different-product exchange uses the stored adjusted value.
        //   - Additional payment is handled before collection.
        //   - A cheaper exchange refund is handled when the exchange is
        //     completed.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ApproveReturn(
            int returnRequestId,
            string approvedResolution,
            string itemDisposition,
            string adminDecisionReason)
        {
            if (!IsAdminLoggedIn())
            {
                TempData["Error"] =
                    "Only an Admin can approve a return.";

                return RedirectToAction("Returns");
            }

            var request = db.ReturnRequests
                .Include(r => r.Member)
                .Include(r => r.StoreOrderItem.Product)
                .Include(r => r.StoreOrderItem.StoreOrder)
                .Include(r => r.ExchangeProduct)
                .FirstOrDefault(r =>
                    r.ReturnRequestId == returnRequestId);

            if (request == null)
            {
                TempData["Error"] = "Return request not found.";
                return RedirectToAction("Returns");
            }

            if (request.Status != "PendingAdminApproval" &&
                request.Status != "StaffReviewed")
            {
                TempData["Error"] =
                    "This return request is not awaiting a decision.";

                return RedirectToAction("Returns");
            }

            if (request.StoreOrderItem == null ||
                request.StoreOrderItem.StoreOrder == null)
            {
                TempData["Error"] =
                    "The original order information could not be found.";

                return RedirectToAction("Returns");
            }

            if (!ReturnRequestHelper.IsValidRequestedResolution(
                approvedResolution))
            {
                TempData["Error"] =
                    "Please select Refund or Exchange.";

                return RedirectToAction(
                    "ReviewReturnDecision",
                    new { id = returnRequestId });
            }

            if (!ReturnRequestHelper.IsValidItemDisposition(
                itemDisposition))
            {
                TempData["Error"] =
                    "Please select a valid item disposition.";

                return RedirectToAction(
                    "ReviewReturnDecision",
                    new { id = returnRequestId });
            }

            adminDecisionReason =
                (adminDecisionReason ?? string.Empty).Trim();

            if (adminDecisionReason.Length < 10)
            {
                TempData["Error"] =
                    "Please provide final decision notes of at least " +
                    "10 characters.";

                return RedirectToAction(
                    "ReviewReturnDecision",
                    new { id = returnRequestId });
            }

            if (adminDecisionReason.Length > 500)
            {
                TempData["Error"] =
                    "Final decision notes cannot exceed 500 characters.";

                return RedirectToAction(
                    "ReviewReturnDecision",
                    new { id = returnRequestId });
            }

            // Damaged items cannot enter sellable stock.
            if ((request.ItemCondition == "Damaged" ||
                 request.ItemCondition == "SeverelyDamaged") &&
                itemDisposition == "Restock")
            {
                TempData["Error"] =
                    "A damaged item cannot be returned to sellable stock.";

                return RedirectToAction(
                    "ReviewReturnDecision",
                    new { id = returnRequestId });
            }

            if (request.ProductMatchesPurchase == false &&
                itemDisposition == "Restock")
            {
                TempData["Error"] =
                    "An unmatched product cannot be returned to stock.";

                return RedirectToAction(
                    "ReviewReturnDecision",
                    new { id = returnRequestId });
            }

            var order = request.StoreOrderItem.StoreOrder;

            decimal returnCredit =
                request.ReturnCreditAmount ??
                ReturnRequestHelper.CalculateRefundAmount(
                    order,
                    request.StoreOrderItem,
                    request.Quantity);

            // ========================================================
            // APPROVE REFUND
            // ========================================================
            if (approvedResolution == "Refund")
            {
                if (string.IsNullOrWhiteSpace(
                    order.PaystackReference))
                {
                    TempData["Error"] =
                        "The original Paystack transaction reference " +
                        "could not be found.";

                    return RedirectToAction(
                        "ReviewReturnDecision",
                        new { id = returnRequestId });
                }

                if (returnCredit <= 0)
                {
                    TempData["Error"] =
                        "The refund amount must be greater than zero.";

                    return RedirectToAction(
                        "ReviewReturnDecision",
                        new { id = returnRequestId });
                }

                string secretKey =
                    WebConfigurationManager.AppSettings[
                        "Paystack:SecretKey"];

                if (string.IsNullOrWhiteSpace(secretKey))
                {
                    TempData["Error"] =
                        "Paystack is not configured.";

                    return RedirectToAction(
                        "ReviewReturnDecision",
                        new { id = returnRequestId });
                }

                string paystackRefundReference = null;

                try
                {
                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers
                                .AuthenticationHeaderValue(
                                    "Bearer",
                                    secretKey);

                        var payload = new JObject
                        {
                            ["transaction"] =
                                order.PaystackReference,

                            ["amount"] =
                                (int)Math.Round(
                                    returnCredit * 100,
                                    0,
                                    MidpointRounding.AwayFromZero)
                        };

                        var content =
                            new StringContent(
                                payload.ToString(),
                                System.Text.Encoding.UTF8,
                                "application/json");

                        var response = await client.PostAsync(
                            "https://api.paystack.co/refund",
                            content);

                        string responseBody =
                            await response.Content.ReadAsStringAsync();

                        JObject json;

                        try
                        {
                            json = JObject.Parse(responseBody);
                        }
                        catch
                        {
                            TempData["Error"] =
                                "Paystack returned an invalid response.";

                            return RedirectToAction(
                                "ReviewReturnDecision",
                                new { id = returnRequestId });
                        }

                        bool refundAccepted =
                            response.IsSuccessStatusCode &&
                            json["status"]?.Value<bool>() == true;

                        if (!refundAccepted)
                        {
                            string paystackMessage =
                                json["message"]?.ToString()
                                ?? "Unknown Paystack error.";

                            TempData["Error"] =
                                "Paystack refund failed: " +
                                paystackMessage;

                            return RedirectToAction(
                                "ReviewReturnDecision",
                                new { id = returnRequestId });
                        }

                        paystackRefundReference =
                            json["data"]?["id"]?.ToString();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[PAYSTACK REFUND ERROR] {ex}");

                    TempData["Error"] =
                        "The refund could not be processed. " +
                        "No inventory changes were saved.";

                    return RedirectToAction(
                        "ReviewReturnDecision",
                        new { id = returnRequestId });
                }

                using (var transaction =
                    db.Database.BeginTransaction())
                {
                    try
                    {
                        request.ApprovedResolution = "Refund";
                        request.AdminDecisionReason =
                            adminDecisionReason;

                        request.DecidedAt = DateTime.Now;
                        request.DecidedByAdminId = 1;

                        request.ReturnCreditAmount =
                            returnCredit;

                        request.RefundAmount =
                            returnCredit;

                        request.PaystackRefundReference =
                            paystackRefundReference;

                        request.RefundProcessedAt =
                            DateTime.Now;

                        request.ItemDisposition =
                            itemDisposition;

                        request.WasRestocked =
                            itemDisposition == "Restock";

                        request.Status = "Refunded";

                        if (request.WasRestocked &&
                            request.StoreOrderItem.Product != null)
                        {
                            request.StoreOrderItem.Product
                                .QuantityOnHand += request.Quantity;

                            db.Entry(
                                request.StoreOrderItem.Product)
                                .State = EntityState.Modified;
                        }

                        db.Entry(request).State =
                            EntityState.Modified;

                        db.SaveChanges();
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();

                        System.Diagnostics.Debug.WriteLine(
                            $"[REFUND SAVE ERROR] {ex}");

                        TempData["Error"] =
                            "Paystack accepted the refund, but GymHub " +
                            "could not finish updating the return record. " +
                            "Use the Paystack reference to reconcile it.";

                        return RedirectToAction("Returns");
                    }
                }

                try
                {
                    new NotificationService(db).Notify(
                        "Member",
                        request.MemberId,
                        "Return Approved — Refund Processed",
                        $"Your return {request.ReferenceNumber} was " +
                        $"approved. R{returnCredit:F2} has been refunded.",
                        "Success",
                        "/Store/MyOrders");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[NOTIFICATION ERROR] {ex.Message}");
                }

                try
                {
                    if (request.Member != null)
                    {
                        ProfessionalStoreEmailService.SendReturnApproved(
                            request.Member.Email,
                            request.Member.FullName,
                            request.ReferenceNumber,
                            returnCredit);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[EMAIL ERROR] {ex.Message}");
                }

                TempData["Success"] =
                    $"Return {request.ReferenceNumber} approved. " +
                    $"R{returnCredit:F2} refunded through Paystack.";

                return RedirectToAction("Returns");
            }

            // ========================================================
            // APPROVE EXCHANGE
            // ========================================================

            if (!request.ExchangeProductId.HasValue ||
                request.ExchangeProduct == null)
            {
                TempData["Error"] =
                    "This request does not have a valid replacement product.";

                return RedirectToAction(
                    "ReviewReturnDecision",
                    new { id = returnRequestId });
            }

            int exchangeQuantity =
                request.ExchangeQuantity ?? request.Quantity;

            if (exchangeQuantity < 1)
            {
                TempData["Error"] =
                    "The exchange quantity is invalid.";

                return RedirectToAction(
                    "ReviewReturnDecision",
                    new { id = returnRequestId });
            }

            if (!request.ExchangeProduct.IsActive)
            {
                TempData["Error"] =
                    "The replacement product is no longer active.";

                return RedirectToAction(
                    "ReviewReturnDecision",
                    new { id = returnRequestId });
            }

            if (request.ExchangeProduct.QuantityOnHand <
    exchangeQuantity)
            {
                TempData["Error"] =
                    $"Only {request.ExchangeProduct.QuantityOnHand} " +
                    $"{request.ExchangeProduct.Name} unit(s) are " +
                    $"currently available.";

                return RedirectToAction(
                    "ReviewReturnDecision",
                    new { id = returnRequestId });
            }

            decimal exchangeTotal;

            decimal exchangeDifference;

            if (request.ExchangeType == "SameProduct")
            {
                // Like-for-like exchange always carries forward the
                // original paid value.
                exchangeQuantity = request.Quantity;
                exchangeTotal = returnCredit;
                exchangeDifference = 0m;
            }
            else
            {
                exchangeTotal =
                    request.ExchangeTotalAmount ?? 0m;

                exchangeDifference =
                    ReturnRequestHelper
                        .CalculateExchangePriceDifference(
                            returnCredit,
                            exchangeTotal);
            }

            decimal additionalPayment =
                ReturnRequestHelper.CalculateAdditionalPayment(
                    exchangeDifference);

            decimal exchangeRefund =
                ReturnRequestHelper.CalculateExchangeRefund(
                    exchangeDifference);

            using (var transaction =
                db.Database.BeginTransaction())
            {
                try
                {
                    // Reserve the replacement by removing it from
                    // available inventory.
                    request.ExchangeProduct.QuantityOnHand -=
                        exchangeQuantity;

                    db.Entry(request.ExchangeProduct).State =
                        EntityState.Modified;

                    // The original product can only return to sellable
                    // stock when Admin explicitly selects Restock.
                    request.WasRestocked =
                        itemDisposition == "Restock";

                    if (request.WasRestocked &&
                        request.StoreOrderItem.Product != null)
                    {
                        request.StoreOrderItem.Product
                            .QuantityOnHand += request.Quantity;

                        db.Entry(
                            request.StoreOrderItem.Product)
                            .State = EntityState.Modified;
                    }

                    request.ApprovedResolution = "Exchange";
                    request.AdminDecisionReason =
                        adminDecisionReason;

                    request.DecidedAt = DateTime.Now;
                    request.DecidedByAdminId = 1;

                    request.ItemDisposition =
                        itemDisposition;

                    request.ReturnCreditAmount =
                        returnCredit;

                    request.ExchangeQuantity =
                        exchangeQuantity;

                    request.ExchangeQuantityReserved =
                        exchangeQuantity;

                    request.ExchangeTotalAmount =
                        exchangeTotal;

                    request.ExchangePriceDifference =
                        exchangeDifference;

                    request.AdditionalPaymentAmount =
                        additionalPayment;

                    request.ExchangeRefundAmount =
                        exchangeRefund;

                    request.ExchangeReservedAt =
                        DateTime.Now;

                    request.ExchangeReservationExpiresAt =
                        DateTime.Now.AddDays(5);

                    if (additionalPayment > 0)
                    {
                        request.Status =
                            "AwaitingExchangePayment";
                    }
                    else
                    {
                        request.Status =
                            "ReadyForExchange";
                    }

                    db.Entry(request).State =
                        EntityState.Modified;

                    db.SaveChanges();
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();

                    System.Diagnostics.Debug.WriteLine(
                        $"[EXCHANGE APPROVAL ERROR] {ex}");

                    TempData["Error"] =
                        "The exchange could not be approved. " +
                        "No inventory changes were saved.";

                    return RedirectToAction(
                        "ReviewReturnDecision",
                        new { id = returnRequestId });
                }
            }

            try
            {
                string exchangeMessage;

                if (additionalPayment > 0)
                {
                    exchangeMessage =
                        $"Your exchange {request.ReferenceNumber} was " +
                        $"approved. An additional payment of " +
                        $"R{additionalPayment:F2} is required before " +
                        $"the replacement can be handed over.";
                }
                else if (exchangeRefund > 0)
                {
                    exchangeMessage =
                        $"Your exchange {request.ReferenceNumber} was " +
                        $"approved. Your replacement has been reserved. " +
                        $"A difference refund of R{exchangeRefund:F2} " +
                        $"will be processed when the exchange is completed.";
                }
                else
                {
                    exchangeMessage =
                        $"Your exchange {request.ReferenceNumber} was " +
                        $"approved. Your replacement is ready for " +
                        $"in-store exchange.";
                }

                new NotificationService(db).Notify(
                    "Member",
                    request.MemberId,
                    "Product Exchange Approved",
                    exchangeMessage,
                    "Success",
                    "/Store/MyOrders");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[NOTIFICATION ERROR] {ex.Message}");
            }

            try
            {
                if (request.Member != null)
                {
                    if (additionalPayment > 0)
                    {
                        ProfessionalStoreEmailService
                            .SendExchangePaymentRequired(
                                request.Member.Email,
                                request.Member.FullName,
                                request.ReferenceNumber,
                                request.ExchangeProduct.Name,
                                additionalPayment,
                                request.ExchangeReservationExpiresAt
                                    ?? DateTime.Now.AddDays(5));
                    }
                    else
                    {
                        ProfessionalStoreEmailService.SendExchangeApproved(
                            request.Member.Email,
                            request.Member.FullName,
                            request.ReferenceNumber,
                            request.ExchangeProduct.Name,
                            exchangeQuantity,
                            additionalPayment,
                            exchangeRefund);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[EXCHANGE APPROVAL EMAIL ERROR] {ex}");
            }

            if (additionalPayment > 0)
            {
                TempData["Success"] =
                    $"Exchange {request.ReferenceNumber} approved. " +
                    $"Replacement stock reserved. Member must pay " +
                    $"R{additionalPayment:F2}.";
            }
            else
            {
                TempData["Success"] =
                    $"Exchange {request.ReferenceNumber} approved. " +
                    $"Replacement stock reserved and ready for exchange.";
            }

            return RedirectToAction("Returns");
        }
        // ============================================================
        // GET: StoreAdmin/CompleteExchange/5
        // Store Staff or Admin performs the final in-store handover.
        // ============================================================
        public ActionResult CompleteExchange(int id)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            var request = db.ReturnRequests
                .Include(r => r.Member)
                .Include(r => r.StoreOrderItem.Product)
                .Include(r => r.StoreOrderItem.StoreOrder)
                .Include(r => r.ExchangeProduct)
                .FirstOrDefault(r => r.ReturnRequestId == id);

            if (request == null)
            {
                TempData["Error"] = "Exchange request not found.";
                return RedirectToAction("Returns");
            }

            if (request.ApprovedResolution != "Exchange")
            {
                TempData["Error"] =
                    "This request was not approved as an exchange.";

                return RedirectToAction("Returns");
            }

            if (request.Status == "ExchangeCompleted")
            {
                TempData["Success"] =
                    "This exchange has already been completed.";

                return RedirectToAction("Returns");
            }

            if (request.Status == "AwaitingExchangePayment")
            {
                TempData["Error"] =
                    "The member must pay the outstanding exchange " +
                    "difference before the replacement can be handed over.";

                return RedirectToAction("Returns");
            }

            if (request.Status != "ReadyForExchange")
            {
                TempData["Error"] =
                    "This exchange is not ready for handover.";

                return RedirectToAction("Returns");
            }

            if (request.ExchangeReservationExpiresAt.HasValue &&
                DateTime.Now >
                request.ExchangeReservationExpiresAt.Value)
            {
                TempData["Error"] =
                    "The replacement reservation has expired. " +
                    "An Admin must review the exchange before handover.";

                return RedirectToAction("Returns");
            }

            if (request.ExchangeProduct == null)
            {
                TempData["Error"] =
                    "The replacement product could not be found.";

                return RedirectToAction("Returns");
            }

            return View(request);
        }


        // ============================================================
        // POST: StoreAdmin/CompleteExchange
        //
        // The replacement inventory was already deducted when Admin
        // approved the exchange. It must not be deducted again here.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CompleteExchange(
            int returnRequestId,
            bool memberIdentityVerified,
            bool returnedItemConfirmed,
            bool replacementHandedOver,
            string handoverNotes,
            string signatureData)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            var request = db.ReturnRequests
                .Include(r => r.Member)
                .Include(r => r.StoreOrderItem.Product)
                .Include(r => r.StoreOrderItem.StoreOrder)
                .Include(r => r.ExchangeProduct)
                .FirstOrDefault(r =>
                    r.ReturnRequestId == returnRequestId);

            if (request == null)
            {
                TempData["Error"] = "Exchange request not found.";
                return RedirectToAction("Returns");
            }

            if (request.ApprovedResolution != "Exchange")
            {
                TempData["Error"] =
                    "This request was not approved as an exchange.";

                return RedirectToAction("Returns");
            }

            if (request.Status == "ExchangeCompleted")
            {
                TempData["Success"] =
                    "This exchange has already been completed.";

                return RedirectToAction("Returns");
            }

            if (request.Status != "ReadyForExchange")
            {
                TempData["Error"] =
                    request.Status == "AwaitingExchangePayment"
                        ? "The exchange payment is still outstanding."
                        : "This exchange is not ready for handover.";

                return RedirectToAction("Returns");
            }

            if (request.ExchangeReservationExpiresAt.HasValue &&
                DateTime.Now >
                request.ExchangeReservationExpiresAt.Value)
            {
                TempData["Error"] =
                    "The replacement reservation has expired.";

                return RedirectToAction("Returns");
            }

            if (!memberIdentityVerified)
            {
                TempData["Error"] =
                    "The member's identity must be verified.";

                return RedirectToAction(
                    "CompleteExchange",
                    new { id = returnRequestId });
            }

            if (!returnedItemConfirmed)
            {
                TempData["Error"] =
                    "Confirm that the returned item is held by GymHub.";

                return RedirectToAction(
                    "CompleteExchange",
                    new { id = returnRequestId });
            }

            if (!replacementHandedOver)
            {
                TempData["Error"] =
                    "Confirm that the replacement product was handed over.";

                return RedirectToAction(
                    "CompleteExchange",
                    new { id = returnRequestId });
            }

            if (request.ExchangeProduct == null)
            {
                TempData["Error"] =
                    "The replacement product could not be found.";

                return RedirectToAction("Returns");
            }

            int reservedQuantity =
                request.ExchangeQuantityReserved ??
                request.ExchangeQuantity ??
                0;

            if (reservedQuantity < 1)
            {
                TempData["Error"] =
                    "No replacement quantity was reserved.";

                return RedirectToAction("Returns");
            }

            decimal additionalPayment =
                request.AdditionalPaymentAmount ?? 0m;

            if (additionalPayment > 0 &&
                !request.ExchangePaymentCompletedAt.HasValue)
            {
                TempData["Error"] =
                    "The required exchange payment has not been verified.";

                return RedirectToAction("Returns");
            }

            handoverNotes =
                (handoverNotes ?? string.Empty).Trim();

            if (handoverNotes.Length > 500)
            {
                TempData["Error"] =
                    "Handover notes cannot exceed 500 characters.";

                return RedirectToAction(
                    "CompleteExchange",
                    new { id = returnRequestId });
            }

            if (string.IsNullOrWhiteSpace(signatureData))
            {
                TempData["Error"] =
                    "The member's signature is required.";

                return RedirectToAction(
                    "CompleteExchange",
                    new { id = returnRequestId });
            }

            // ========================================================
            // VALIDATE AND SAVE SIGNATURE
            // ========================================================

            string signaturePhysicalPath = null;
            string signatureRelativePath = null;

            try
            {
                string base64Data =
                    signatureData.Contains(",")
                        ? signatureData.Split(',')[1]
                        : signatureData;

                byte[] signatureBytes =
                    Convert.FromBase64String(base64Data);

                // Prevent an unexpectedly large signature payload.
                if (signatureBytes.Length > 2 * 1024 * 1024)
                {
                    TempData["Error"] =
                        "The signature image is too large.";

                    return RedirectToAction(
                        "CompleteExchange",
                        new { id = returnRequestId });
                }

                string signatureFolder =
                    Server.MapPath(
                        "~/Content/uploads/exchange-signatures");

                if (!Directory.Exists(signatureFolder))
                    Directory.CreateDirectory(signatureFolder);

                string signatureFileName =
                    "exchange-" +
                    request.ReturnRequestId +
                    "-" +
                    Guid.NewGuid().ToString("N") +
                    ".png";

                signaturePhysicalPath =
                    Path.Combine(
                        signatureFolder,
                        signatureFileName);

                System.IO.File.WriteAllBytes(
                    signaturePhysicalPath,
                    signatureBytes);

                signatureRelativePath =
                    "/Content/uploads/exchange-signatures/" +
                    signatureFileName;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[EXCHANGE SIGNATURE ERROR] {ex}");

                TempData["Error"] =
                    "The member signature could not be saved.";

                return RedirectToAction(
                    "CompleteExchange",
                    new { id = returnRequestId });
            }

            // ========================================================
            // CHEAPER REPLACEMENT DIFFERENCE REFUND
            // ========================================================

            decimal exchangeRefund =
                request.ExchangeRefundAmount ?? 0m;

            string exchangeRefundReference = null;

            if (exchangeRefund > 0 &&
                string.IsNullOrWhiteSpace(
                    request.ExchangeRefundReference))
            {
                var originalOrder =
                    request.StoreOrderItem?.StoreOrder;

                if (originalOrder == null ||
                    string.IsNullOrWhiteSpace(
                        originalOrder.PaystackReference))
                {
                    DeleteExchangeSignatureFile(
                        signaturePhysicalPath);

                    TempData["Error"] =
                        "The original Paystack transaction could not " +
                        "be found for the difference refund.";

                    return RedirectToAction(
                        "CompleteExchange",
                        new { id = returnRequestId });
                }

                string secretKey =
                    WebConfigurationManager.AppSettings[
                        "Paystack:SecretKey"];

                if (string.IsNullOrWhiteSpace(secretKey))
                {
                    DeleteExchangeSignatureFile(
                        signaturePhysicalPath);

                    TempData["Error"] =
                        "Paystack is not configured.";

                    return RedirectToAction(
                        "CompleteExchange",
                        new { id = returnRequestId });
                }

                try
                {
                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers
                                .AuthenticationHeaderValue(
                                    "Bearer",
                                    secretKey);

                        var payload = new JObject
                        {
                            ["transaction"] =
                                originalOrder.PaystackReference,

                            ["amount"] =
                                (int)Math.Round(
                                    exchangeRefund * 100,
                                    0,
                                    MidpointRounding.AwayFromZero)
                        };

                        var content =
                            new StringContent(
                                payload.ToString(),
                                System.Text.Encoding.UTF8,
                                "application/json");

                        var response =
                            await client.PostAsync(
                                "https://api.paystack.co/refund",
                                content);

                        string responseBody =
                            await response.Content
                                .ReadAsStringAsync();

                        JObject json;

                        try
                        {
                            json = JObject.Parse(responseBody);
                        }
                        catch
                        {
                            DeleteExchangeSignatureFile(
                                signaturePhysicalPath);

                            TempData["Error"] =
                                "Paystack returned an invalid response.";

                            return RedirectToAction(
                                "CompleteExchange",
                                new { id = returnRequestId });
                        }

                        bool refundAccepted =
                            response.IsSuccessStatusCode &&
                            json["status"]?.Value<bool>() == true;

                        if (!refundAccepted)
                        {
                            DeleteExchangeSignatureFile(
                                signaturePhysicalPath);

                            TempData["Error"] =
                                "The exchange difference refund failed: " +
                                (json["message"]?.ToString()
                                 ?? "Unknown Paystack error.");

                            return RedirectToAction(
                                "CompleteExchange",
                                new { id = returnRequestId });
                        }

                        exchangeRefundReference =
                            json["data"]?["id"]?.ToString();
                    }
                }
                catch (Exception ex)
                {
                    DeleteExchangeSignatureFile(
                        signaturePhysicalPath);

                    System.Diagnostics.Debug.WriteLine(
                        $"[EXCHANGE REFUND ERROR] {ex}");

                    TempData["Error"] =
                        "The difference refund could not be processed. " +
                        "The exchange was not completed.";

                    return RedirectToAction(
                        "CompleteExchange",
                        new { id = returnRequestId });
                }
            }

            // ========================================================
            // COMPLETE EXCHANGE
            // ========================================================

            using (var transaction =
                db.Database.BeginTransaction())
            {
                try
                {
                    request.ExchangeSignaturePath =
                        signatureRelativePath;

                    request.ExchangeCompletedAt =
                        DateTime.Now;

                    request.ExchangeCompletedByStoreStaffId =
                        IsStoreStaffLoggedIn()
                            ? (int?)Session["LoggedInStoreStaff"]
                            : null;

                    if (!string.IsNullOrWhiteSpace(
                        exchangeRefundReference))
                    {
                        request.ExchangeRefundReference =
                            exchangeRefundReference;
                    }

                    request.Status = "ExchangeCompleted";

                    db.Entry(request).State =
                        EntityState.Modified;

                    // Do not deduct replacement stock here.
                    // It was reserved during Admin approval.

                    db.SaveChanges();
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();

                    System.Diagnostics.Debug.WriteLine(
                        $"[EXCHANGE COMPLETION ERROR] {ex}");

                    TempData["Error"] =
                        exchangeRefund > 0
                            ? "The refund was accepted, but GymHub could " +
                              "not finish saving the exchange. An Admin " +
                              "must reconcile the transaction."
                            : "The exchange could not be completed.";

                    return RedirectToAction(
                        "CompleteExchange",
                        new { id = returnRequestId });
                }
            }

            try
            {
                string memberMessage =
                    $"Your exchange {request.ReferenceNumber} has been " +
                    $"completed. You received " +
                    $"{reservedQuantity} × " +
                    $"{request.ExchangeProduct.Name}.";

                if (exchangeRefund > 0)
                {
                    memberMessage +=
                        $" A difference refund of " +
                        $"R{exchangeRefund:F2} was submitted.";
                }

                new NotificationService(db).Notify(
                    "Member",
                    request.MemberId,
                    "Exchange Completed",
                    memberMessage,
                    "Success",
                    "/Store/MyOrders");

                new NotificationService(db).NotifyAdmin(
                    "Exchange Completed",
                    $"Exchange {request.ReferenceNumber} was completed.",
                    "Success",
                    "/StoreAdmin/Returns");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[EXCHANGE NOTIFICATION ERROR] {ex.Message}");
            }

            try
            {
                if (request.Member != null &&
                    request.ExchangeProduct != null)
                {
                    ProfessionalStoreEmailService.SendExchangeCompleted(
                        request.Member.Email,
                        request.Member.FullName,
                        request.ReferenceNumber,
                        request.ExchangeProduct.Name,
                        reservedQuantity,
                        request.ExchangeCompletedAt ?? DateTime.Now);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[EXCHANGE COMPLETION EMAIL ERROR] {ex}");
            }

            TempData["Success"] =
                $"Exchange {request.ReferenceNumber} completed successfully.";

            return RedirectToAction("Returns");
        }
        private void DeleteExchangeSignatureFile(
    string physicalPath)
        {
            if (string.IsNullOrWhiteSpace(physicalPath))
                return;

            try
            {
                if (System.IO.File.Exists(physicalPath))
                    System.IO.File.Delete(physicalPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SIGNATURE CLEANUP ERROR] {ex.Message}");
            }
        }
        // ============================================================
        // GET: StoreAdmin/InspectReturn/5
        // Formal physical inspection performed by Store Staff or Admin.
        // ============================================================
        public ActionResult InspectReturn(int id)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            var request = db.ReturnRequests
                .Include(r => r.Member)
                .Include(r => r.StoreOrderItem.Product)
                .Include(r => r.StoreOrderItem.StoreOrder)
                .Include(r => r.ExchangeProduct)
                .FirstOrDefault(r => r.ReturnRequestId == id);

            if (request == null)
            {
                TempData["Error"] = "Return request not found.";
                return RedirectToAction("Returns");
            }

            if (request.Status != "Pending")
            {
                TempData["Error"] =
                    "This return request has already been inspected.";

                return RedirectToAction("Returns");
            }

            if (request.StoreOrderItem == null ||
                request.StoreOrderItem.StoreOrder == null)
            {
                TempData["Error"] =
                    "The original order information could not be found.";

                return RedirectToAction("Returns");
            }

            ViewBag.IsAdmin = IsAdminLoggedIn();
            ViewBag.IsStoreStaff = IsStoreStaffLoggedIn();

            return View(request);
        }


        // ============================================================
        // POST: StoreAdmin/InspectReturn
        // Records the inspection and sends it to Admin.
        // It does not refund, exchange or update inventory.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult InspectReturn(
            int returnRequestId,
            int quantityPresented,
            string itemCondition,
            string packagingCondition,
            bool? accessoriesComplete,
            bool? productMatchesPurchase,
            string faultVerification,
            string faultCause,
            string itemDisposition,
            string recommendation,
            string recommendedResolution,
            string reviewNotes,
            HttpPostedFileBase evidenceImage)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            var request = db.ReturnRequests
                .Include(r => r.Member)
                .Include(r => r.StoreOrderItem.Product)
                .Include(r => r.StoreOrderItem.StoreOrder)
                .Include(r => r.ExchangeProduct)
                .FirstOrDefault(r =>
                    r.ReturnRequestId == returnRequestId);

            if (request == null)
            {
                TempData["Error"] = "Return request not found.";
                return RedirectToAction("Returns");
            }

            if (request.Status != "Pending")
            {
                TempData["Error"] =
                    "This return request has already been inspected.";

                return RedirectToAction("Returns");
            }

            // The member must present every unit included in the request.
            if (quantityPresented != request.Quantity)
            {
                TempData["Error"] =
                    $"The member must present all {request.Quantity} " +
                    $"unit(s) included in the request.";

                return RedirectToAction(
                    "InspectReturn",
                    new { id = returnRequestId });
            }

            if (!ReturnRequestHelper.IsValidItemCondition(
                itemCondition))
            {
                TempData["Error"] =
                    "Please select a valid item condition.";

                return RedirectToAction(
                    "InspectReturn",
                    new { id = returnRequestId });
            }

            if (!ReturnRequestHelper.IsValidPackagingCondition(
                packagingCondition))
            {
                TempData["Error"] =
                    "Please select a valid packaging condition.";

                return RedirectToAction(
                    "InspectReturn",
                    new { id = returnRequestId });
            }

            if (!accessoriesComplete.HasValue)
            {
                TempData["Error"] =
                    "Please confirm whether all accessories are present.";

                return RedirectToAction(
                    "InspectReturn",
                    new { id = returnRequestId });
            }

            if (!productMatchesPurchase.HasValue)
            {
                TempData["Error"] =
                    "Please confirm whether the product matches the purchase.";

                return RedirectToAction(
                    "InspectReturn",
                    new { id = returnRequestId });
            }

            if (!ReturnRequestHelper.IsValidFaultVerification(
                faultVerification))
            {
                TempData["Error"] =
                    "Please select a valid fault-verification result.";

                return RedirectToAction(
                    "InspectReturn",
                    new { id = returnRequestId });
            }

            if (!ReturnRequestHelper.IsValidFaultCause(faultCause))
            {
                TempData["Error"] =
                    "Please select the most likely cause of the issue.";

                return RedirectToAction(
                    "InspectReturn",
                    new { id = returnRequestId });
            }

            if (!ReturnRequestHelper.IsValidItemDisposition(
                itemDisposition))
            {
                TempData["Error"] =
                    "Please select a valid item disposition.";

                return RedirectToAction(
                    "InspectReturn",
                    new { id = returnRequestId });
            }

            if (!ReturnRequestHelper.IsValidStaffRecommendation(
                recommendation))
            {
                TempData["Error"] =
                    "Please select a valid staff recommendation.";

                return RedirectToAction(
                    "InspectReturn",
                    new { id = returnRequestId });
            }

            reviewNotes = (reviewNotes ?? string.Empty).Trim();

            if (reviewNotes.Length < 10)
            {
                TempData["Error"] =
                    "Inspection notes must contain at least 10 characters.";

                return RedirectToAction(
                    "InspectReturn",
                    new { id = returnRequestId });
            }

            if (reviewNotes.Length > 500)
            {
                TempData["Error"] =
                    "Inspection notes cannot exceed 500 characters.";

                return RedirectToAction(
                    "InspectReturn",
                    new { id = returnRequestId });
            }

            // An approved recommendation must specify what resolution
            // staff recommends.
            if (recommendation == "Approve")
            {
                if (!ReturnRequestHelper.IsValidRequestedResolution(
                    recommendedResolution))
                {
                    TempData["Error"] =
                        "Please select whether you recommend a refund " +
                        "or an exchange.";

                    return RedirectToAction(
                        "InspectReturn",
                        new { id = returnRequestId });
                }

                if (recommendedResolution == "Exchange" &&
                    !request.ExchangeProductId.HasValue)
                {
                    TempData["Error"] =
                        "An exchange cannot be recommended because no " +
                        "replacement product was selected.";

                    return RedirectToAction(
                        "InspectReturn",
                        new { id = returnRequestId });
                }
            }
            else
            {
                recommendedResolution = null;
            }

            // A product that does not match the original purchase should
            // never be returned directly to sellable inventory.
            if (productMatchesPurchase == false &&
                itemDisposition == "Restock")
            {
                TempData["Error"] =
                    "An unmatched product cannot be returned to stock.";

                return RedirectToAction(
                    "InspectReturn",
                    new { id = returnRequestId });
            }

            // Damaged products should not be returned directly to
            // sellable inventory.
            if ((itemCondition == "Damaged" ||
                 itemCondition == "SeverelyDamaged") &&
                itemDisposition == "Restock")
            {
                TempData["Error"] =
                    "A damaged item cannot be returned directly to " +
                    "sellable stock.";

                return RedirectToAction(
                    "InspectReturn",
                    new { id = returnRequestId });
            }

            string evidencePath = null;

            if (evidenceImage != null &&
                evidenceImage.ContentLength > 0)
            {
                const int maximumFileSize = 5 * 1024 * 1024;

                if (evidenceImage.ContentLength > maximumFileSize)
                {
                    TempData["Error"] =
                        "The evidence image cannot exceed 5 MB.";

                    return RedirectToAction(
                        "InspectReturn",
                        new { id = returnRequestId });
                }

                string extension = Path.GetExtension(
                    evidenceImage.FileName);

                extension = string.IsNullOrWhiteSpace(extension)
                    ? string.Empty
                    : extension.ToLowerInvariant();

                string[] allowedExtensions =
                {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

                if (!allowedExtensions.Contains(extension))
                {
                    TempData["Error"] =
                        "Evidence must be a JPG, JPEG, PNG or WEBP image.";

                    return RedirectToAction(
                        "InspectReturn",
                        new { id = returnRequestId });
                }

                string evidenceFolder =
                    Server.MapPath(
                        "~/Content/uploads/return-inspections");

                if (!Directory.Exists(evidenceFolder))
                    Directory.CreateDirectory(evidenceFolder);

                string evidenceFileName =
                    "return-" +
                    request.ReturnRequestId +
                    "-" +
                    Guid.NewGuid().ToString("N") +
                    extension;

                string physicalPath =
                    Path.Combine(
                        evidenceFolder,
                        evidenceFileName);

                evidenceImage.SaveAs(physicalPath);

                evidencePath =
                    "/Content/uploads/return-inspections/" +
                    evidenceFileName;
            }

            request.ItemReceivedAt = DateTime.Now;
            request.QuantityPresented = quantityPresented;

            request.ItemCondition = itemCondition;
            request.PackagingCondition = packagingCondition;
            request.AccessoriesComplete = accessoriesComplete;
            request.ProductMatchesPurchase = productMatchesPurchase;

            request.FaultVerification = faultVerification;
            request.FaultCause = faultCause;

            request.ItemDisposition = itemDisposition;
            request.StaffRecommendation = recommendation;
            request.StaffRecommendedResolution =
                recommendedResolution;

            request.StaffReviewNotes = reviewNotes;
            request.StaffReviewedAt = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(evidencePath))
                request.InspectionEvidencePath = evidencePath;

            if (IsStoreStaffLoggedIn())
            {
                request.StaffReviewedByStoreStaffId =
                    (int)Session["LoggedInStoreStaff"];
            }

            // The inspection is only a recommendation.
            // Admin still makes the binding decision.
            request.Status = "PendingAdminApproval";

            db.Entry(request).State = EntityState.Modified;
            db.SaveChanges();

            try
            {
                string resolutionText =
                    string.IsNullOrWhiteSpace(recommendedResolution)
                        ? "No resolution recommended"
                        : recommendedResolution;

                new NotificationService(db).NotifyAdmin(
                    "Return Inspection Completed",
                    $"Return {request.ReferenceNumber} was inspected. " +
                    $"Recommendation: {recommendation}. " +
                    $"Resolution: {resolutionText}. " +
                    $"Admin review is required.",
                    "Info",
                    "/StoreAdmin/Returns");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[RETURN INSPECTION NOTIFICATION ERROR] " +
                    $"{ex.Message}");
            }

            try
            {
                if (request.Member != null)
                {
                    ProfessionalStoreEmailService
                        .SendReturnInspectionCompleted(
                            request.Member.Email,
                            request.Member.FullName,
                            request.ReferenceNumber,
                            request.StoreOrderItem?.Product?.Name
                                ?? "Store product",
                            recommendation);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[RETURN INSPECTION EMAIL ERROR] {ex}");
            }

            TempData["Success"] =
                $"Inspection for {request.ReferenceNumber} was saved " +
                $"and sent to Admin.";

            return RedirectToAction("Returns");
        }
        // ============================================================
        // GET: StoreAdmin/ReviewReturnDecision/5
        // ADMIN ONLY
        // ============================================================
        public ActionResult ReviewReturnDecision(int id)
        {
            if (!IsAdminLoggedIn())
            {
                TempData["Error"] =
                    "Only an Admin can make the final return decision.";

                return RedirectToAction("Returns");
            }

            var request = db.ReturnRequests
                .Include(r => r.Member)
                .Include(r => r.StoreOrderItem.Product)
                .Include(r => r.StoreOrderItem.StoreOrder)
                .Include(r => r.ExchangeProduct)
                .FirstOrDefault(r => r.ReturnRequestId == id);

            if (request == null)
            {
                TempData["Error"] = "Return request not found.";
                return RedirectToAction("Returns");
            }

            if (request.Status != "PendingAdminApproval" &&
                request.Status != "StaffReviewed")
            {
                TempData["Error"] =
                    "This return request is not awaiting an Admin decision.";

                return RedirectToAction("Returns");
            }

            return View(request);
        }
        // POST: StoreAdmin/RejectReturn — ADMIN ONLY, hard gate, reason required
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectReturn(int returnRequestId, string reason)
        {
            if (!IsAdminLoggedIn())
            {
                TempData["Error"] = "Only an admin can reject a return.";
                return RedirectToAction("Returns");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Please provide a reason for rejecting this return.";
                return RedirectToAction("Returns");
            }

            var request = db.ReturnRequests
                .Include(r => r.Member)
                .FirstOrDefault(r => r.ReturnRequestId == returnRequestId);

            if (request == null)
            {
                TempData["Error"] = "Return request not found.";
                return RedirectToAction("Returns");
            }

            if (request.Status != "PendingAdminApproval" && request.Status != "StaffReviewed")
            {
                TempData["Error"] = "This request is not awaiting a decision.";
                return RedirectToAction("Returns");
            }

            request.Status = "Rejected";
            request.DecidedAt = DateTime.Now;
            request.DecidedByAdminId = 1;
            request.AdminDecisionReason = reason.Trim();

            db.Entry(request).State = EntityState.Modified;
            db.SaveChanges();

            try
            {
                new NotificationService(db).Notify(
                    "Member", request.MemberId,
                    "Return Request Declined",
                    $"Your return {request.ReferenceNumber} was declined. Reason: {reason.Trim()}",
                    "Warning",
                    "/Store/MyOrders");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }

            try
            {
                ProfessionalStoreEmailService.SendReturnRejected(
                    request.Member.Email,
                    request.Member.FullName,
                    request.ReferenceNumber,
                    reason.Trim());
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[EMAIL ERROR] {ex.Message}"); }

            TempData["Success"] = $"Return {request.ReferenceNumber} rejected.";
            return RedirectToAction("Returns");
        }
        // ── ADD these actions to StoreAdminController.cs ──────────────

        // GET: StoreAdmin/Suppliers
        public ActionResult Suppliers()
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            var suppliers = db.Suppliers.OrderBy(s => s.Name).ToList();
            return View(suppliers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddSupplier(string name, string contactPerson, string email, string phoneNumber)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Supplier name is required.";
                return RedirectToAction("Suppliers");
            }

            db.Suppliers.Add(new Supplier
            {
                Name = name.Trim(),
                ContactPerson = contactPerson,
                Email = email,
                PhoneNumber = phoneNumber,
                IsActive = true
            });
            db.SaveChanges();

            TempData["Success"] = $"Supplier \"{name}\" added.";
            return RedirectToAction("Suppliers");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeactivateSupplier(int id)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            var supplier = db.Suppliers.Find(id);
            if (supplier == null)
            {
                TempData["Error"] = "Supplier not found.";
                return RedirectToAction("Suppliers");
            }

            supplier.IsActive = !supplier.IsActive;
            db.Entry(supplier).State = EntityState.Modified;
            db.SaveChanges();

            TempData["Success"] = $"Supplier \"{supplier.Name}\" {(supplier.IsActive ? "reactivated" : "deactivated")}.";
            return RedirectToAction("Suppliers");
        }

        // ============================================================
        // SUPPLIER PRODUCT CATALOGUE
        // ============================================================
        public ActionResult SupplierCatalogue(int supplierId)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            var supplier = db.Suppliers.Find(supplierId);
            if (supplier == null) return HttpNotFound();

            var catalogue = db.SupplierProducts
                .Include(x => x.Product)
                .Where(x => x.SupplierId == supplierId)
                .OrderBy(x => x.Product.Name)
                .ToList();

            ViewBag.Supplier = supplier;
            ViewBag.AvailableProducts = db.Products
                .Where(x => x.IsActive && !db.SupplierProducts.Any(sp =>
                    sp.SupplierId == supplierId && sp.ProductId == x.ProductId))
                .OrderBy(x => x.Name)
                .ToList();
            ViewBag.IsAdmin = IsAdminLoggedIn();
            return View(catalogue);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult AddSupplierProduct(int supplierId, int productId,
            decimal currentUnitCost, int estimatedDeliveryDays = 7,
            bool isPreferredSupplier = false)
        {
            if (!IsAdminLoggedIn())
            {
                TempData["Error"] = "Only an admin can maintain supplier prices.";
                return RedirectToAction("SupplierCatalogue", new { supplierId });
            }

            if (currentUnitCost <= 0 || estimatedDeliveryDays < 1)
            {
                TempData["Error"] = "Enter a valid unit cost and delivery time.";
                return RedirectToAction("SupplierCatalogue", new { supplierId });
            }

            if (db.SupplierProducts.Any(x => x.SupplierId == supplierId && x.ProductId == productId))
            {
                TempData["Error"] = "This product is already linked to the supplier.";
                return RedirectToAction("SupplierCatalogue", new { supplierId });
            }

            if (db.Suppliers.Find(supplierId) == null || db.Products.Find(productId) == null)
                return HttpNotFound();

            if (isPreferredSupplier)
            {
                var otherPreferred = db.SupplierProducts.Where(x =>
                    x.ProductId == productId && x.IsPreferredSupplier).ToList();
                foreach (var other in otherPreferred) other.IsPreferredSupplier = false;
            }

            db.SupplierProducts.Add(new SupplierProduct
            {
                SupplierId = supplierId,
                ProductId = productId,
                CurrentUnitCost = currentUnitCost,
                EstimatedDeliveryDays = estimatedDeliveryDays,
                IsPreferredSupplier = isPreferredSupplier,
                IsActive = true,
                PriceUpdatedAt = DateTime.Now,
                CreatedAt = DateTime.Now
            });
            db.SaveChanges();
            TempData["Success"] = "Product added to the supplier catalogue.";
            return RedirectToAction("SupplierCatalogue", new { supplierId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult UpdateSupplierProduct(int supplierProductId,
            decimal currentUnitCost, int estimatedDeliveryDays,
            bool? isPreferredSupplier, bool? isActive)
        {
            if (!IsAdminLoggedIn())
            {
                TempData["Error"] = "Only an admin can maintain supplier prices.";
                return RedirectToAction("Suppliers");
            }

            var record = db.SupplierProducts.Find(supplierProductId);
            if (record == null) return HttpNotFound();

            if (currentUnitCost <= 0 || estimatedDeliveryDays < 1)
            {
                TempData["Error"] = "Enter a valid unit cost and delivery time.";
                return RedirectToAction("SupplierCatalogue", new { supplierId = record.SupplierId });
            }

            if (isPreferredSupplier == true)
            {
                var others = db.SupplierProducts.Where(x =>
                    x.ProductId == record.ProductId &&
                    x.SupplierProductId != record.SupplierProductId &&
                    x.IsPreferredSupplier).ToList();
                foreach (var other in others) other.IsPreferredSupplier = false;
            }

            record.CurrentUnitCost = currentUnitCost;
            record.EstimatedDeliveryDays = estimatedDeliveryDays;
            record.IsPreferredSupplier = isPreferredSupplier ?? false;
            record.IsActive = isActive ?? false;
            record.PriceUpdatedAt = DateTime.Now;
            db.SaveChanges();

            TempData["Success"] = "Supplier product updated.";
            return RedirectToAction("SupplierCatalogue", new { supplierId = record.SupplierId });
        }

        [HttpGet]
        public JsonResult GetSupplierProducts(int supplierId)
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Session expired." }, JsonRequestBehavior.AllowGet);

            var records = db.SupplierProducts
                .Where(x => x.SupplierId == supplierId && x.IsActive && x.Product.IsActive)
                .Select(x => new
                {
                    id = x.ProductId,
                    name = x.Product.Name,
                    stock = x.Product.QuantityOnHand,
                    reorder = x.Product.ReorderLevel,
                    unitCost = x.CurrentUnitCost,
                    deliveryDays = x.EstimatedDeliveryDays,
                    preferred = x.IsPreferredSupplier
                })
                .OrderBy(x => x.name)
                .ToList();

            return Json(new { success = true, products = records }, JsonRequestBehavior.AllowGet);
        }

        private const decimal SupplierReturnApprovalThreshold = 1000m;

        public ActionResult PurchaseOrders()
        {
            if (!IsAuthorized()) return RedirectToAction("Login", "Admin");
            var orders = db.PurchaseOrders
                .Include(x => x.Supplier)
                .Include(x => x.Items.Select(i => i.Product))
                .Where(x => x.Status != "Completed")
                .OrderByDescending(x => x.CreatedAt).ToList();
            ViewBag.IsAdmin = IsAdminLoggedIn();
            ViewBag.AwaitingApprovalCount = orders.Count(x => x.Status == "PendingAdminApproval");
            return View(orders);
        }

        // ============================================================
        // QR DELIVERY LOOKUP
        // ============================================================
        [HttpGet]
        public ActionResult ScanDelivery()
        {
            if (!IsAuthorized()) return RedirectToAction("Login", "Admin");
            return View();
        }

        [HttpPost]
        public JsonResult VerifyDeliveryQr(string qrData)
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Session expired. Please log in again." });

            if (!ProcurementQrHelper.IsPurchaseOrderQr(qrData))
                return Json(new { success = false, message = "This is not a GymHub purchase-order QR code." });

            string reference = ProcurementQrHelper.ExtractField(qrData, "Ref");
            string token = ProcurementQrHelper.ExtractField(qrData, "Token");

            var po = db.PurchaseOrders.FirstOrDefault(x =>
                x.ReferenceNumber == reference && x.ReceivingQrToken == token);

            if (po == null)
                return Json(new { success = false, message = "The purchase-order QR could not be verified." });
            if (po.Status == "Inspected" || po.Status == "Completed")
                return Json(new { success = false, message = po.ReferenceNumber + " has already been inspected." });
            if (po.Status != "Confirmed")
                return Json(new { success = false, message = po.ReferenceNumber + " is currently " + po.Status + " and cannot be inspected." });

            return Json(new
            {
                success = true,
                purchaseOrderId = po.PurchaseOrderId,
                referenceNumber = po.ReferenceNumber,
                redirectUrl = Url.Action("InspectDelivery", "StoreAdmin", new { purchaseOrderId = po.PurchaseOrderId })
            });
        }

        [HttpPost]
        public JsonResult FindPurchaseOrdersByProductQr(string qrData)
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Session expired. Please log in again." });
            if (!ProcurementQrHelper.IsProductQr(qrData))
                return Json(new { success = false, message = "This is not a GymHub product QR code." });

            string sku = ProcurementQrHelper.ExtractField(qrData, "SKU");
            string token = ProcurementQrHelper.ExtractField(qrData, "Token");
            var product = db.Products.FirstOrDefault(x => x.SKU == sku && x.ProductQrToken == token);
            if (product == null)
                return Json(new { success = false, message = "The product QR could not be verified." });

            var matches = db.PurchaseOrderItems
                .Where(x => x.ProductId == product.ProductId && x.PurchaseOrder.Status == "Confirmed")
                .Select(x => new
                {
                    purchaseOrderId = x.PurchaseOrderId,
                    referenceNumber = x.PurchaseOrder.ReferenceNumber,
                    supplierName = x.PurchaseOrder.Supplier.Name,
                    expectedQty = x.QuantityOrdered
                }).ToList();

            return Json(new
            {
                success = matches.Any(),
                message = matches.Any() ? null : "No confirmed, uninspected purchase order contains this product.",
                productName = product.Name,
                matches = matches
            });
        }

        [HttpPost]
        public JsonResult VerifyInspectionProduct(int purchaseOrderId, string qrData)
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Session expired. Please log in again." });
            if (!ProcurementQrHelper.IsProductQr(qrData))
                return Json(new { success = false, message = "This is not a GymHub product QR code." });

            string sku = ProcurementQrHelper.ExtractField(qrData, "SKU");
            string token = ProcurementQrHelper.ExtractField(qrData, "Token");
            var product = db.Products.FirstOrDefault(x => x.SKU == sku && x.ProductQrToken == token);
            if (product == null)
                return Json(new { success = false, message = "The product QR could not be verified." });

            var item = db.PurchaseOrderItems.FirstOrDefault(x =>
                x.PurchaseOrderId == purchaseOrderId && x.ProductId == product.ProductId);
            if (item == null)
                return Json(new { success = false, message = product.Name + " is not expected on this purchase order." });

            return Json(new
            {
                success = true,
                productId = product.ProductId,
                productName = product.Name,
                sku = product.SKU,
                expectedQty = item.QuantityOrdered
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult GenerateMissingProcurementQrs()
        {
            if (!IsAdminLoggedIn())
            {
                TempData["Error"] = "Only an admin can generate missing QR codes.";
                return RedirectToAction("PurchaseOrders");
            }

            int productCount = 0;
            foreach (var product in db.Products.ToList())
            {
                if (string.IsNullOrWhiteSpace(product.ProductQrToken) || string.IsNullOrWhiteSpace(product.ProductQrBase64))
                {
                    EnsureProductHasQr(product);
                    productCount++;
                }
            }

            int poCount = 0;
            foreach (var po in db.PurchaseOrders.Where(x => x.Status == "Confirmed").ToList())
            {
                if (string.IsNullOrWhiteSpace(po.ReceivingQrToken) || string.IsNullOrWhiteSpace(po.ReceivingQrBase64))
                {
                    EnsurePurchaseOrderHasReceivingQr(po);
                    poCount++;
                }
            }

            db.SaveChanges();
            TempData["Success"] = productCount + " product QR(s) and " + poCount + " purchase-order QR(s) generated.";
            return RedirectToAction("PurchaseOrders");
        }

        private PurchaseOrderCreateViewModel BuildPurchaseOrderForm(PurchaseOrderCreateViewModel model = null)
        {
            model = model ?? new PurchaseOrderCreateViewModel
            {
                RequestedDeliveryDate = DateTime.Today.AddDays(7),
                Items = new List<PurchaseOrderLineInput> { new PurchaseOrderLineInput { Quantity = 1 } }
            };
            model.Suppliers = db.Suppliers.Where(x => x.IsActive).OrderBy(x => x.Name)
                .Select(x => new SelectListItem { Value = x.SupplierId.ToString(), Text = x.Name }).ToList();
            model.Products = db.Products.Where(x => x.IsActive).OrderBy(x => x.Name).ToList();
            return model;
        }

        [HttpGet]
        public ActionResult CreatePurchaseOrder()
        {
            if (!IsAuthorized()) return RedirectToAction("Login", "Admin");
            ViewBag.IsAdmin = IsAdminLoggedIn();
            return View(BuildPurchaseOrderForm());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult CreatePurchaseOrder(PurchaseOrderCreateViewModel model)
        {
            if (!IsAuthorized()) return RedirectToAction("Login", "Admin");
            model.Items = (model.Items ?? new List<PurchaseOrderLineInput>())
                .Where(x => x.ProductId > 0 || x.Quantity > 0 || x.UnitCost > 0).ToList();

            if (model.RequestedDeliveryDate.Date < DateTime.Today)
                ModelState.AddModelError("RequestedDeliveryDate", "The requested delivery date cannot be in the past.");
            if (model.Priority != "Normal" && model.Priority != "Urgent")
                ModelState.AddModelError("Priority", "Select Normal or Urgent priority.");
            if (!model.Items.Any()) ModelState.AddModelError("Items", "Add at least one product.");
            if (model.Items.GroupBy(x => x.ProductId).Any(g => g.Key > 0 && g.Count() > 1))
                ModelState.AddModelError("Items", "A product may only appear once on an order.");

            var supplierProducts = db.SupplierProducts
                .Where(x => x.SupplierId == model.SupplierId && x.IsActive && x.Product.IsActive)
                .ToList();

            if (model.Items.Any(x => !supplierProducts.Any(sp => sp.ProductId == x.ProductId)))
                ModelState.AddModelError("Items", "One or more products are not registered for the selected supplier.");

            if (!ModelState.IsValid)
            {
                ViewBag.IsAdmin = IsAdminLoggedIn();
                return View(BuildPurchaseOrderForm(model));
            }

            decimal total = model.Items.Sum(x => x.Quantity * x.UnitCost);
            bool unusualPrice = model.Items.Any(line =>
            {
                var cataloguePrice = supplierProducts
                    .Where(sp => sp.ProductId == line.ProductId)
                    .Select(sp => sp.CurrentUnitCost)
                    .FirstOrDefault();

                return cataloguePrice > 0 &&
                    line.UnitCost > cataloguePrice * 1.15m;
            });

            bool needsApproval = total > PurchaseOrderHelper.ApprovalThreshold ||
                                 model.Priority == "Urgent" || unusualPrice;

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var po = new PurchaseOrder
                    {
                        ReferenceNumber = PurchaseOrderHelper.GenerateReferenceNumber(db),
                        SupplierId = model.SupplierId,
                        OrderPurpose = model.OrderPurpose.Trim(),
                        RequestedDeliveryDate = model.RequestedDeliveryDate.Date,
                        Priority = model.Priority,
                        InternalNotes = model.InternalNotes,
                        TotalValue = total,
                        RequiredAdminApproval = needsApproval,
                        Status = needsApproval && !IsAdminLoggedIn() ? "PendingAdminApproval" : "Pending",
                        CreatedByStoreStaffId = IsStoreStaffLoggedIn() ? (int?)Session["LoggedInStoreStaff"] : null,
                        CreatedAt = DateTime.Now,
                        ApprovedByAdminId = needsApproval && IsAdminLoggedIn() ? 1 : (int?)null,
                        ApprovedAt = needsApproval && IsAdminLoggedIn() ? DateTime.Now : (DateTime?)null
                    };
                    db.PurchaseOrders.Add(po);
                    db.SaveChanges();

                    foreach (var line in model.Items)
                        db.PurchaseOrderItems.Add(new PurchaseOrderItem
                        {
                            PurchaseOrderId = po.PurchaseOrderId,
                            ProductId = line.ProductId,
                            QuantityOrdered = line.Quantity,
                            UnitCost = line.UnitCost
                        });

                    db.SaveChanges();
                    transaction.Commit();
                    TempData["Success"] = po.Status == "PendingAdminApproval"
                        ? po.ReferenceNumber + " submitted for admin approval."
                        : po.ReferenceNumber + " created and ready to send.";
                }
                catch (Exception ex)
                {
                    transaction.Rollback();

                    string detailedError;
                    var validationException = ex as System.Data.Entity.Validation.DbEntityValidationException;

                    if (validationException != null)
                    {
                        detailedError = string.Join(" | ", validationException.EntityValidationErrors
                            .SelectMany(result => result.ValidationErrors)
                            .Select(error => error.PropertyName + ": " + error.ErrorMessage));
                    }
                    else
                    {
                        var deepestException = ex;
                        while (deepestException.InnerException != null)
                            deepestException = deepestException.InnerException;

                        detailedError = deepestException.Message;
                    }

                    System.Diagnostics.Debug.WriteLine(
                        "[CREATE PURCHASE ORDER ERROR] " + ex);

                    TempData["Error"] =
                        "The purchase order could not be created. No data was saved. Details: " +
                        detailedError;
                }
            }
            return RedirectToAction("PurchaseOrders");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ApprovePurchaseOrder(int purchaseOrderId)
        {
            if (!IsAdminLoggedIn()) { TempData["Error"] = "Only an admin can approve this purchase order."; return RedirectToAction("PurchaseOrders"); }
            var po = db.PurchaseOrders.Include(x => x.Supplier).FirstOrDefault(x => x.PurchaseOrderId == purchaseOrderId);
            if (po == null) return HttpNotFound();
            if (po.Status != "PendingAdminApproval") TempData["Error"] = "This order is not awaiting approval.";
            else { po.Status = "Pending"; po.ApprovedByAdminId = 1; po.ApprovedAt = DateTime.Now; db.SaveChanges(); TempData["Success"] = po.ReferenceNumber + " approved."; }
            return RedirectToAction("PurchaseOrders");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult RejectPurchaseOrder(int purchaseOrderId, string reason)
        {
            if (!IsAdminLoggedIn()) { TempData["Error"] = "Only an admin can reject this purchase order."; return RedirectToAction("PurchaseOrders"); }
            var po = db.PurchaseOrders.Find(purchaseOrderId);
            if (po == null) return HttpNotFound();
            if (po.Status != "PendingAdminApproval") TempData["Error"] = "This order is not awaiting approval.";
            else if (string.IsNullOrWhiteSpace(reason)) TempData["Error"] = "A rejection reason is required.";
            else { po.Status = "Rejected"; po.AdminRejectionReason = reason.Trim(); db.SaveChanges(); TempData["Success"] = po.ReferenceNumber + " rejected."; }
            return RedirectToAction("PurchaseOrders");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult MarkPurchaseOrderSent(int purchaseOrderId)
        {
            if (!IsAuthorized()) return RedirectToAction("Login", "Admin");
            var po = db.PurchaseOrders.Find(purchaseOrderId);
            if (po == null) return HttpNotFound();
            if (po.Status != "Pending") TempData["Error"] = "This order cannot be sent in its current status.";
            else
            {
                var items = db.PurchaseOrderItems
                    .Where(x => x.PurchaseOrderId == purchaseOrderId)
                    .ToList();

                foreach (var item in items)
                {
                    var supplierProduct = db.SupplierProducts.FirstOrDefault(x =>
                        x.SupplierId == po.SupplierId && x.ProductId == item.ProductId);

                    if (supplierProduct != null)
                    {
                        supplierProduct.CurrentUnitCost = item.UnitCost;
                        supplierProduct.PriceUpdatedAt = DateTime.Now;
                    }
                }

                po.Status = "Confirmed";
                po.SentToSupplierAt = DateTime.Now;
                po.ConfirmedAt = DateTime.Now;
                EnsurePurchaseOrderHasReceivingQr(po);
                db.SaveChanges();
                TempData["Success"] = po.ReferenceNumber + " sent and confirmed. Supplier prices were updated.";
            }
            return RedirectToAction("PurchaseOrders");
        }

        [HttpGet]
        public ActionResult InspectDelivery(int purchaseOrderId)
        {
            if (!IsAuthorized()) return RedirectToAction("Login", "Admin");
            var po = db.PurchaseOrders.Include(x => x.Supplier).Include(x => x.Items.Select(i => i.Product))
                .FirstOrDefault(x => x.PurchaseOrderId == purchaseOrderId);
            if (po == null) return HttpNotFound();
            if (po.Status != "Confirmed")
            {
                TempData["Error"] = "Only a confirmed purchase order can be inspected.";
                return RedirectToAction("PurchaseOrders");
            }

            var vm = new DeliveryInspectionViewModel
            {
                PurchaseOrderId = po.PurchaseOrderId,
                PurchaseOrder = po,
                DeliveredAt = DateTime.Now,
                PackagingCondition = "Good",
                SealCondition = "Intact",
                DeliveryNoteReceived = true,
                OverallResult = "Accept",
                Items = po.Items.OrderBy(x => x.Product.Name).Select(x => new DeliveryInspectionLineInput
                {
                    PurchaseOrderItemId = x.PurchaseOrderItemId,
                    ProductId = x.ProductId,
                    ProductName = x.Product.Name,
                    ExpectedQty = x.QuantityOrdered,
                    ReceivedQty = x.QuantityOrdered,
                    Condition = "Good"
                }).ToList()
            };
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SubmitInspection(DeliveryInspectionViewModel model)
        {
            if (!IsAuthorized()) return RedirectToAction("Login", "Admin");
            var po = db.PurchaseOrders.Include(x => x.Supplier).Include(x => x.Items.Select(i => i.Product))
                .FirstOrDefault(x => x.PurchaseOrderId == model.PurchaseOrderId);
            if (po == null) return HttpNotFound();
            if (po.Status != "Confirmed")
            {
                TempData["Error"] = "This order has already been processed or is not confirmed.";
                return RedirectToAction("PurchaseOrders");
            }

            model.Items = model.Items ?? new List<DeliveryInspectionLineInput>();
            if (model.Items.Count != po.Items.Count) ModelState.AddModelError("Items", "Every ordered product must be inspected.");

            foreach (var line in model.Items)
            {
                var expected = po.Items.SingleOrDefault(x => x.PurchaseOrderItemId == line.PurchaseOrderItemId);
                if (expected == null || expected.ProductId != line.ProductId)
                    ModelState.AddModelError("Items", "The inspection contains an invalid item.");
                if (line.DamagedQty > line.ReceivedQty)
                    ModelState.AddModelError("Items", "Damaged quantity cannot exceed received quantity.");
                if (line.DamagedQty > 0 && (string.IsNullOrWhiteSpace(line.DamageType) || string.IsNullOrWhiteSpace(line.DamageSeverity)))
                    ModelState.AddModelError("Items", "Damage type and severity are required for damaged items.");
            }

            if (!ModelState.IsValid)
            {
                model.PurchaseOrder = po;
                for (int i = 0; i < model.Items.Count; i++)
                {
                    var source = po.Items.FirstOrDefault(x => x.PurchaseOrderItemId == model.Items[i].PurchaseOrderItemId);
                    if (source != null) { model.Items[i].ProductName = source.Product.Name; model.Items[i].ExpectedQty = source.QuantityOrdered; }
                }
                return View("InspectDelivery", model);
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var delivery = new SupplierDelivery
                    {
                        PurchaseOrderId = po.PurchaseOrderId,
                        InspectionReference = "INS-" + DateTime.Now.Year + "-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper(),
                        DeliveryNoteNumber = model.DeliveryNoteNumber.Trim(),
                        DeliveredAt = model.DeliveredAt,
                        CourierDriver = model.CourierDriver,
                        VehicleRegistration = model.VehicleRegistration,
                        NumberOfPackages = model.NumberOfPackages,
                        PackagingCondition = model.PackagingCondition,
                        SealCondition = model.SealCondition,
                        SignsOfTampering = model.SignsOfTampering,
                        DeliveryNoteReceived = model.DeliveryNoteReceived,
                        SupplierInvoiceReceived = model.SupplierInvoiceReceived,
                        GeneralNotes = model.GeneralNotes,
                        OverallResult = model.OverallResult,
                        FinalInspectionNotes = model.FinalInspectionNotes,
                        InspectionStatus = "Completed",
                        InspectedAt = DateTime.Now,
                        InspectedByStoreStaffId = IsStoreStaffLoggedIn() ? (int?)Session["LoggedInStoreStaff"] : null,
                        InspectedByAdminId = IsAdminLoggedIn() ? 1 : (int?)null
                    };
                    db.SupplierDeliveries.Add(delivery);
                    db.SaveChanges();

                    foreach (var input in model.Items)
                    {
                        var poItem = po.Items.Single(x => x.PurchaseOrderItemId == input.PurchaseOrderItemId);
                        int missing = Math.Max(poItem.QuantityOrdered - input.ReceivedQty, 0);
                        int excess = Math.Max(input.ReceivedQty - poItem.QuantityOrdered, 0);
                        int good = input.ReceivedQty - input.DamagedQty;
                        string evidencePath = SaveInspectionEvidence(input.EvidenceImage);

                        db.DeliveryItems.Add(new DeliveryItem
                        {
                            SupplierDeliveryId = delivery.SupplierDeliveryId,
                            ProductId = poItem.ProductId,
                            ExpectedQty = poItem.QuantityOrdered,
                            ReceivedQty = input.ReceivedQty,
                            DamagedQty = input.DamagedQty,
                            GoodQty = good,
                            MissingQty = missing,
                            ExcessQty = excess,
                            Condition = input.DamagedQty > 0 && good > 0 ? "Mixed" : input.DamagedQty > 0 ? "Damaged" : input.Condition,
                            DamageType = input.DamagedQty > 0 ? input.DamageType : null,
                            DamageSeverity = input.DamagedQty > 0 ? input.DamageSeverity : null,
                            InspectionNotes = input.InspectionNotes,
                            EvidenceImagePath = evidencePath,
                            IsFlagged = input.DamagedQty > 0 || missing > 0 || excess > 0
                        });

                        // Only physically received, undamaged units enter sellable stock.
                        if (good > 0) poItem.Product.QuantityOnHand += good;
                    }

                    po.Status = "Inspected";
                    db.SaveChanges();
                    transaction.Commit();
                    TempData["Success"] = delivery.InspectionReference + " completed. Only accepted units were added to stock.";
                }
                catch
                {
                    transaction.Rollback();
                    TempData["Error"] = "Inspection failed. Inventory and inspection records were not changed.";
                    return RedirectToAction("InspectDelivery", new { purchaseOrderId = model.PurchaseOrderId });
                }
            }
            return RedirectToAction("DeliveryIssues", new { purchaseOrderId = model.PurchaseOrderId });
        }

        private string SaveInspectionEvidence(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0) return null;
            if (file.ContentLength > 5 * 1024 * 1024) throw new InvalidOperationException("Evidence image exceeds 5 MB.");
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!(new[] { ".jpg", ".jpeg", ".png", ".webp" }).Contains(ext))
                throw new InvalidOperationException("Invalid evidence image type.");
            var folder = Server.MapPath("~/Content/uploads/delivery-inspections");
            Directory.CreateDirectory(folder);
            var name = Guid.NewGuid().ToString("N") + ext;
            file.SaveAs(Path.Combine(folder, name));
            return "/Content/uploads/delivery-inspections/" + name;
        }

        [HttpGet]
        public ActionResult DeliveryIssues(int purchaseOrderId)
        {
            if (!IsAuthorized()) return RedirectToAction("Login", "Admin");
            var po = db.PurchaseOrders.Include(x => x.Supplier).FirstOrDefault(x => x.PurchaseOrderId == purchaseOrderId);
            if (po == null) return HttpNotFound();
            var items = db.DeliveryItems.Include(x => x.Product).Include(x => x.SupplierDelivery)
                .Where(x => x.SupplierDelivery.PurchaseOrderId == purchaseOrderId &&
                    (x.DamagedQty > 0 || x.MissingQty > 0 || x.ExcessQty > 0)).ToList();
            var ids = items.Select(x => x.DeliveryItemId).ToList();
            ViewBag.ExistingReturns = db.SupplierReturns.Where(x => ids.Contains(x.DeliveryItemId)).ToList()
                .GroupBy(x => x.DeliveryItemId).ToDictionary(x => x.Key, x => x.ToList());
            ViewBag.PurchaseOrder = po;
            ViewBag.IsAdmin = IsAdminLoggedIn();
            return View(items);
        }

        [HttpGet]
        public ActionResult CreateSupplierReturn(int deliveryItemId)
        {
            if (!IsAuthorized()) return RedirectToAction("Login", "Admin");
            var item = db.DeliveryItems.Include(x => x.Product)
                .Include(x => x.SupplierDelivery.PurchaseOrder.Supplier)
                .FirstOrDefault(x => x.DeliveryItemId == deliveryItemId);
            if (item == null) return HttpNotFound();
            int alreadyReturned = db.SupplierReturns.Where(x => x.DeliveryItemId == item.DeliveryItemId && x.Reason == "Damaged" && x.Status != "Rejected").Select(x => (int?)x.Quantity).Sum() ?? 0;
            ViewBag.MaximumReturnable = Math.Max(item.DamagedQty - alreadyReturned, 0);
            return View(new SupplierReturnCreateViewModel { DeliveryItemId = item.DeliveryItemId, DeliveryItem = item, Quantity = Math.Max(item.DamagedQty - alreadyReturned, 0), Reason = "Damaged", RequestedResolution = "Replacement" });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult CreateSupplierReturn(SupplierReturnCreateViewModel model)
        {
            if (!IsAuthorized()) return RedirectToAction("Login", "Admin");
            var item = db.DeliveryItems.Include(x => x.Product).Include(x => x.SupplierDelivery.PurchaseOrder)
                .FirstOrDefault(x => x.DeliveryItemId == model.DeliveryItemId);
            if (item == null) return HttpNotFound();
            int used = db.SupplierReturns.Where(x => x.DeliveryItemId == item.DeliveryItemId && x.Reason == model.Reason && x.Status != "Rejected").Select(x => (int?)x.Quantity).Sum() ?? 0;
            int maximum = model.Reason == "Damaged" ? item.DamagedQty - used : item.MissingQty - used;
            if (model.Quantity < 1 || model.Quantity > maximum) ModelState.AddModelError("Quantity", "Maximum available quantity is " + Math.Max(maximum, 0) + ".");

            decimal unitCost = db.PurchaseOrderItems.Where(x => x.PurchaseOrderId == item.SupplierDelivery.PurchaseOrderId && x.ProductId == item.ProductId).Select(x => x.UnitCost).FirstOrDefault();
            decimal value = unitCost * model.Quantity;
            bool severe = item.DamageSeverity == "Severe";
            bool requiresApproval = !IsAdminLoggedIn() && (value > SupplierReturnApprovalThreshold || severe || item.SupplierDelivery.OverallResult == "Reject");

            if (!ModelState.IsValid)
            {
                item.Product = db.Products.Find(item.ProductId);
                model.DeliveryItem = item;
                ViewBag.MaximumReturnable = Math.Max(maximum, 0);
                return View(model);
            }

            var entity = new SupplierReturn
            {
                ReferenceNumber = "SR-" + DateTime.Now.Year + "-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper(),
                DeliveryItemId = item.DeliveryItemId,
                Quantity = model.Quantity,
                Reason = model.Reason,
                RequestedResolution = model.RequestedResolution,
                Notes = model.Notes,
                EstimatedReturnValue = value,
                RequiresAdminApproval = requiresApproval,
                Status = requiresApproval ? "PendingAdminApproval" : "Approved",
                CreatedByStoreStaffId = IsStoreStaffLoggedIn() ? (int?)Session["LoggedInStoreStaff"] : null,
                CreatedByAdminId = IsAdminLoggedIn() ? 1 : (int?)null,
                ApprovedByAdminId = IsAdminLoggedIn() ? 1 : (int?)null,
                ApprovedAt = IsAdminLoggedIn() ? DateTime.Now : (DateTime?)null,
                CreatedAt = DateTime.Now
            };
            db.SupplierReturns.Add(entity);
            db.SaveChanges();
            TempData["Success"] = requiresApproval ? entity.ReferenceNumber + " submitted for admin approval." : entity.ReferenceNumber + " approved for supplier return.";
            return RedirectToAction("DeliveryIssues", new { purchaseOrderId = item.SupplierDelivery.PurchaseOrderId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ApproveSupplierReturn(int supplierReturnId)
        {
            if (!IsAdminLoggedIn()) { TempData["Error"] = "Admin access is required."; return RedirectToAction("PurchaseOrders"); }
            var entity = db.SupplierReturns.Include(x => x.DeliveryItem.SupplierDelivery).FirstOrDefault(x => x.SupplierReturnId == supplierReturnId);
            if (entity == null) return HttpNotFound();
            if (entity.Status != "PendingAdminApproval") { TempData["Error"] = "This return is not awaiting approval."; }
            else { entity.Status = "Approved"; entity.ApprovedByAdminId = 1; entity.ApprovedAt = DateTime.Now; db.SaveChanges(); TempData["Success"] = entity.ReferenceNumber + " approved."; }
            return RedirectToAction("DeliveryIssues", new { purchaseOrderId = entity.DeliveryItem.SupplierDelivery.PurchaseOrderId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult RejectSupplierReturn(int supplierReturnId, string reason)
        {
            if (!IsAdminLoggedIn()) { TempData["Error"] = "Admin access is required."; return RedirectToAction("PurchaseOrders"); }
            var entity = db.SupplierReturns.Include(x => x.DeliveryItem.SupplierDelivery).FirstOrDefault(x => x.SupplierReturnId == supplierReturnId);
            if (entity == null) return HttpNotFound();
            if (entity.Status != "PendingAdminApproval" || string.IsNullOrWhiteSpace(reason)) TempData["Error"] = "A pending return and rejection reason are required.";
            else { entity.Status = "Rejected"; entity.RejectionReason = reason.Trim(); db.SaveChanges(); TempData["Success"] = entity.ReferenceNumber + " rejected."; }
            return RedirectToAction("DeliveryIssues", new { purchaseOrderId = entity.DeliveryItem.SupplierDelivery.PurchaseOrderId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ResolveSupplierReturn(int supplierReturnId, string resolution, string resolutionNotes)
        {
            if (!IsAdminLoggedIn()) { TempData["Error"] = "Only an admin can resolve supplier returns."; return RedirectToAction("PurchaseOrders"); }
            var entity = db.SupplierReturns.Include(x => x.DeliveryItem.SupplierDelivery).FirstOrDefault(x => x.SupplierReturnId == supplierReturnId);
            if (entity == null) return HttpNotFound();
            var valid = new[] { "Credited", "Replaced", "WrittenOff" };
            if (entity.Status != "Approved" || !valid.Contains(resolution)) TempData["Error"] = "Select a valid resolution for an approved return.";
            else { entity.Status = "Resolved"; entity.Resolution = resolution; entity.ResolutionNotes = resolutionNotes; entity.ResolvedAt = DateTime.Now; db.SaveChanges(); TempData["Success"] = entity.ReferenceNumber + " resolved."; }
            return RedirectToAction("DeliveryIssues", new { purchaseOrderId = entity.DeliveryItem.SupplierDelivery.PurchaseOrderId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult CompletePurchaseOrder(int purchaseOrderId)
        {
            if (!IsAdminLoggedIn()) { TempData["Error"] = "Only an admin can complete a purchase order."; return RedirectToAction("PurchaseOrders"); }
            var po = db.PurchaseOrders.Find(purchaseOrderId);
            if (po == null) return HttpNotFound();
            bool openReturns = db.SupplierReturns.Any(x => x.DeliveryItem.SupplierDelivery.PurchaseOrderId == purchaseOrderId && (x.Status == "PendingAdminApproval" || x.Status == "Approved"));
            if (po.Status != "Inspected") TempData["Error"] = "Only an inspected purchase order can be completed.";
            else if (openReturns) TempData["Error"] = "Resolve or reject all open supplier returns first.";
            else { po.Status = "Completed"; po.CompletedAt = DateTime.Now; po.CompletedByAdminId = 1; db.SaveChanges(); TempData["Success"] = po.ReferenceNumber + " completed."; }
            return RedirectToAction("PurchaseOrders");
        }
        // GET: StoreAdmin/Promotions
        // GET: StoreAdmin/Promotions
        public ActionResult Promotions()
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            var thirtyDaysOut = DateTime.Today.AddDays(30);

            // ALL active products now, not just near-expiry ones — expiry is
            // just a flag on each row, not a gate on who's eligible for a promotion.
            var allProducts = db.Products
                .Where(p => p.IsActive)
                .Include(p => p.Promotions)
                .OrderBy(p => p.Name)
                .ToList();

            var activePromotions = db.Promotions
                .Include(p => p.Product)
                .Where(p => p.IsActive)
                .OrderBy(p => p.EndDate)
                .ToList();

            ViewBag.AllProducts = allProducts;
            ViewBag.NearExpiryCutoff = thirtyDaysOut;
            return View(activePromotions);
        }

        // POST: StoreAdmin/CreatePromotion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreatePromotion(int productId, decimal discountPercent, string startDate, string endDate)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            var product = db.Products.Find(productId);

            if (!DateTime.TryParse(startDate, out DateTime start) || !DateTime.TryParse(endDate, out DateTime end))
            {
                TempData["Error"] = "Please provide valid dates.";
                return RedirectToAction("Promotions");
            }

            string validationError = PromotionHelper.ValidatePromotion(product, start, end, discountPercent);
            if (validationError != null)
            {
                TempData["Error"] = validationError;
                return RedirectToAction("Promotions");
            }

            db.Promotions.Add(new Promotion
            {
                ProductId = productId,
                DiscountPercent = discountPercent,
                StartDate = start,
                EndDate = end,
                IsActive = true,
                CreatedByStoreStaffId = IsStoreStaffLoggedIn() ? (int)Session["LoggedInStoreStaff"] : 0,
                CreatedByAdmin = IsAdminLoggedIn(),
                CreatedAt = DateTime.Now
            });
            db.SaveChanges();

            TempData["Success"] = $"Promotion created for \"{product.Name}\" — {discountPercent}% off.";
            return RedirectToAction("Promotions");
        }

        // POST: StoreAdmin/CancelPromotion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelPromotion(int id)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            var promo = db.Promotions.Find(id);
            if (promo == null)
            {
                TempData["Error"] = "Promotion not found.";
                return RedirectToAction("Promotions");
            }

            promo.IsActive = false;
            db.Entry(promo).State = EntityState.Modified;
            db.SaveChanges();

            TempData["Success"] = "Promotion cancelled.";
            return RedirectToAction("Promotions");
        }
        // ── ADD to StoreAdminController.cs ──────────────────────────

        public ActionResult Analytics(string range = "6m")
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            var allOrders = db.StoreOrders
                .Where(o => o.Status != "PendingPayment")
                .ToList();

            var allItems = db.StoreOrderItems
                .Include(i => i.Product)
                .Include(i => i.StoreOrder)
                .Where(i => i.StoreOrder.Status != "PendingPayment")
                .ToList();

            // ── Date range filter for the chart (stat cards stay all-time) ──
            DateTime rangeStart;
            switch (range)
            {
                case "7d": rangeStart = DateTime.Today.AddDays(-7); break;
                case "30d": rangeStart = DateTime.Today.AddDays(-30); break;
                case "90d": rangeStart = DateTime.Today.AddDays(-90); break;
                case "all": rangeStart = DateTime.MinValue; break;
                default: rangeStart = DateTime.Today.AddMonths(-6); range = "6m"; break;
            }
            ViewBag.SelectedRange = range;

            var ordersInRange = allOrders.Where(o => o.CreatedAt >= rangeStart).ToList();

            // ── Stat cards (all-time) ──
            decimal totalRevenue = allOrders.Sum(o => o.FinalTotal);
            int totalOrders = allOrders.Count;
            int totalCustomers = allOrders.Select(o => o.MemberId).Distinct().Count();
            int totalProducts = db.Products.Count(p => p.IsActive);
            decimal averageOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;

            var thisMonth = DateTime.Today;
            var lastMonth = DateTime.Today.AddMonths(-1);
            decimal thisMonthRevenue = allOrders.Where(o => o.CreatedAt.Year == thisMonth.Year && o.CreatedAt.Month == thisMonth.Month).Sum(o => o.FinalTotal);
            decimal lastMonthRevenue = allOrders.Where(o => o.CreatedAt.Year == lastMonth.Year && o.CreatedAt.Month == lastMonth.Month).Sum(o => o.FinalTotal);
            decimal revenueChangePercent = lastMonthRevenue > 0 ? Math.Round(((thisMonthRevenue - lastMonthRevenue) / lastMonthRevenue) * 100, 1) : 0;

            int thisMonthOrders = allOrders.Count(o => o.CreatedAt.Year == thisMonth.Year && o.CreatedAt.Month == thisMonth.Month);
            int lastMonthOrders = allOrders.Count(o => o.CreatedAt.Year == lastMonth.Year && o.CreatedAt.Month == lastMonth.Month);
            decimal ordersChangePercent = lastMonthOrders > 0 ? Math.Round(((decimal)(thisMonthOrders - lastMonthOrders) / lastMonthOrders) * 100, 1) : 0;

            var thisMonthCustomers = allOrders.Where(o => o.CreatedAt.Year == thisMonth.Year && o.CreatedAt.Month == thisMonth.Month).Select(o => o.MemberId).Distinct().Count();
            var lastMonthCustomers = allOrders.Where(o => o.CreatedAt.Year == lastMonth.Year && o.CreatedAt.Month == lastMonth.Month).Select(o => o.MemberId).Distinct().Count();
            decimal customersChangePercent = lastMonthCustomers > 0 ? Math.Round(((decimal)(thisMonthCustomers - lastMonthCustomers) / lastMonthCustomers) * 100, 1) : 0;

            // ── Sparklines — last 7 days, per stat ──
            var sparkDays = new List<string>();
            var revenueSparkline = new List<decimal>();
            var ordersSparkline = new List<int>();
            for (int i = 6; i >= 0; i--)
            {
                var day = DateTime.Today.AddDays(-i);
                sparkDays.Add(day.ToString("ddd"));
                revenueSparkline.Add(allOrders.Where(o => o.CreatedAt.Date == day.Date).Sum(o => o.FinalTotal));
                ordersSparkline.Add(allOrders.Count(o => o.CreatedAt.Date == day.Date));
            }

            // ── Revenue trend, based on selected range ──
            var monthLabels = new List<string>();
            var monthRevenue = new List<decimal>();
            int monthsBack = range == "7d" ? 0 : range == "30d" ? 1 : range == "90d" ? 3 : range == "all" ? 12 : 6;
            if (range == "7d" || range == "30d")
            {
                int days = range == "7d" ? 7 : 30;
                for (int i = days - 1; i >= 0; i--)
                {
                    var day = DateTime.Today.AddDays(-i);
                    monthLabels.Add(day.ToString("dd MMM"));
                    monthRevenue.Add(ordersInRange.Where(o => o.CreatedAt.Date == day.Date).Sum(o => o.FinalTotal));
                }
            }
            else
            {
                for (int i = monthsBack; i >= 0; i--)
                {
                    var month = DateTime.Today.AddMonths(-i);
                    monthLabels.Add(month.ToString("MMM yyyy"));
                    monthRevenue.Add(allOrders.Where(o => o.CreatedAt.Year == month.Year && o.CreatedAt.Month == month.Month).Sum(o => o.FinalTotal));
                }
            }

            // ── Order status breakdown ──
            var statusBreakdown = allOrders
                .GroupBy(o => o.Status)
                .Select(g => new StatusBreakdownViewModel { Status = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToList();

            // ── Top 5 selling products ──
            var topProducts = allItems
                .GroupBy(i => i.ProductId)
                .Select(g => new TopProductViewModel
                {
                    Product = g.First().Product,
                    TotalQuantity = g.Sum(i => i.Quantity),
                    TotalEarning = g.Sum(i => i.Quantity * i.UnitPriceAtPurchase)
                })
                .OrderByDescending(g => g.TotalQuantity)
                .Take(5)
                .ToList();

            // ── Category revenue breakdown ──
            var categoryBreakdown = allItems
                .Where(i => i.Product != null)
                .GroupBy(i => i.Product.Category)
                .Select(g => new CategoryRevenueViewModel
                {
                    Category = g.Key,
                    Revenue = g.Sum(i => i.Quantity * i.UnitPriceAtPurchase)
                })
                .OrderByDescending(g => g.Revenue)
                .ToList();

            // ── Actionable alerts — reuses existing computed properties ──
            var products = db.Products.Where(p => p.IsActive).ToList();
            ViewBag.LowStockCount = products.Count(p => p.StockStatus == "LowStock");
            ViewBag.OutOfStockCount = products.Count(p => p.StockStatus == "OutOfStock");
            ViewBag.NearExpiryCount = products.Count(p => p.StockStatus == "NearExpiry");
            ViewBag.PendingReturnsCount = db.ReturnRequests.Count(r => r.Status == "Pending" || r.Status == "StaffReviewed" || r.Status == "PendingAdminApproval");
            ViewBag.PendingPurchaseOrdersCount = db.PurchaseOrders.Count(p => p.Status == "PendingAdminApproval");

            // ── Recent orders ──
            ViewBag.RecentOrders = allOrders.OrderByDescending(o => o.CreatedAt).Take(10).ToList();
            var recentOrderMemberIds = ((List<StoreOrder>)ViewBag.RecentOrders).Select(o => o.MemberId).Distinct().ToList();
            ViewBag.RecentOrderMembers = db.Members.Where(m => recentOrderMemberIds.Contains(m.MemberId)).ToDictionary(m => m.MemberId, m => m.FullName);

            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.TotalOrders = totalOrders;
            ViewBag.TotalCustomers = totalCustomers;
            ViewBag.TotalProducts = totalProducts;
            ViewBag.AverageOrderValue = averageOrderValue;
            ViewBag.RevenueChangePercent = revenueChangePercent;
            ViewBag.OrdersChangePercent = ordersChangePercent;
            ViewBag.CustomersChangePercent = customersChangePercent;
            ViewBag.SparkDays = sparkDays;
            ViewBag.RevenueSparkline = revenueSparkline;
            ViewBag.OrdersSparkline = ordersSparkline;
            ViewBag.MonthLabels = monthLabels;
            ViewBag.MonthRevenue = monthRevenue;
            ViewBag.StatusBreakdown = statusBreakdown;
            ViewBag.TopProducts = topProducts;
            ViewBag.CategoryBreakdown = categoryBreakdown;

            return View();
        }
        // ============================================================
        // RETURN / EXCHANGE QR CAMERA SCANNER
        // GET: StoreAdmin/ScanReturn
        // ============================================================

        [HttpGet]
        public ActionResult ScanReturn()
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            ViewBag.IsAdmin =
                Session["LoggedInAdmin"] != null;

            return View();
        }


        // ============================================================
        // VERIFY RETURN / EXCHANGE QR
        // POST: StoreAdmin/VerifyReturnQr
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult VerifyReturnQr(string qrData)
        {
            if (!IsAuthorized())
            {
                Response.StatusCode = 401;

                return Json(new
                {
                    success = false,
                    message = "Your session has expired. Please log in again."
                });
            }

            qrData = (qrData ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(qrData))
            {
                return Json(new
                {
                    success = false,
                    message = "No QR code data was received."
                });
            }

            // Prevent unexpectedly large scanner payloads.
            if (qrData.Length > 1000)
            {
                return Json(new
                {
                    success = false,
                    message = "The scanned QR code is too large."
                });
            }

            if (!ReturnRequestHelper.IsReturnQrFormat(qrData))
            {
                return Json(new
                {
                    success = false,
                    message = "This is not a valid GymHub return QR code."
                });
            }

            string orderReference =
                ReturnRequestHelper.ExtractReturnQrField(
                    qrData,
                    "Order:");

            string returnIdText =
                ReturnRequestHelper.ExtractReturnQrField(
                    qrData,
                    "ReturnId:");

            string rawToken =
                ReturnRequestHelper.ExtractReturnQrField(
                    qrData,
                    "Token:");

            int returnRequestId;

            if (string.IsNullOrWhiteSpace(orderReference) ||
                string.IsNullOrWhiteSpace(returnIdText) ||
                string.IsNullOrWhiteSpace(rawToken) ||
                !int.TryParse(returnIdText, out returnRequestId) ||
                returnRequestId <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "The return QR code contains invalid information."
                });
            }

            var request = db.ReturnRequests
                .Include(r => r.Member)
                .Include(r => r.StoreOrderItem.Product)
                .Include(r => r.StoreOrderItem.StoreOrder)
                .FirstOrDefault(r =>
                    r.ReturnRequestId == returnRequestId);

            if (request == null ||
                request.StoreOrderItem == null ||
                request.StoreOrderItem.StoreOrder == null)
            {
                return Json(new
                {
                    success = false,
                    message = "No return request matches this QR code."
                });
            }

            // The QR order reference must match the return's actual order.
            if (!string.Equals(
                request.StoreOrderItem.StoreOrder.ReferenceNumber,
                orderReference,
                StringComparison.OrdinalIgnoreCase))
            {
                return Json(new
                {
                    success = false,
                    message = "This QR code does not match the return order."
                });
            }

            // Compare the scanned raw token against the stored SHA-256 hash.
            if (!ReturnRequestHelper.VerifyReturnQrToken(
                rawToken,
                request.ReturnQrTokenHash))
            {
                return Json(new
                {
                    success = false,
                    message = "This return QR code could not be authenticated."
                });
            }

            bool isAdmin =
                Session["LoggedInAdmin"] != null;

            string productName =
                request.StoreOrderItem.Product?.Name ??
                "Store product";

            string memberName =
                request.Member?.FullName ??
                "Member";

            // ========================================================
            // ROUTE ACCORDING TO CURRENT WORKFLOW STATUS
            // ========================================================

            switch (request.Status)
            {
                case "Pending":
                    return Json(new
                    {
                        success = true,
                        canContinue = true,
                        status = request.Status,
                        title = "Return request verified",
                        message =
                            $"{productName} has been verified. Opening the staff inspection form.",
                        referenceNumber = request.ReferenceNumber,
                        orderReference = orderReference,
                        productName = productName,
                        memberName = memberName,
                        quantity = request.Quantity,
                        nextUrl = Url.Action(
                            "InspectReturn",
                            "StoreAdmin",
                            new { id = request.ReturnRequestId })
                    });

                case "StaffReviewed":
                case "PendingAdminApproval":
                    if (isAdmin)
                    {
                        return Json(new
                        {
                            success = true,
                            canContinue = true,
                            status = request.Status,
                            title = "Awaiting Admin decision",
                            message =
                                $"{productName} has already been inspected. Opening the Admin review.",
                            referenceNumber = request.ReferenceNumber,
                            orderReference = orderReference,
                            productName = productName,
                            memberName = memberName,
                            quantity = request.Quantity,
                            nextUrl = Url.Action(
                                "ReviewReturnDecision",
                                "StoreAdmin",
                                new { id = request.ReturnRequestId })
                        });
                    }

                    return Json(new
                    {
                        success = true,
                        canContinue = false,
                        status = request.Status,
                        title = "Inspection already completed",
                        message =
                            "This return has already been inspected and is awaiting an Admin decision.",
                        referenceNumber = request.ReferenceNumber,
                        orderReference = orderReference,
                        productName = productName,
                        memberName = memberName,
                        quantity = request.Quantity,
                        nextUrl = (string)null
                    });

                case "ExchangeApproved":
                    return Json(new
                    {
                        success = true,
                        canContinue = false,
                        status = request.Status,
                        title = "Exchange approved",
                        message =
                            "The exchange was approved, but it is not yet ready for physical completion.",
                        referenceNumber = request.ReferenceNumber,
                        orderReference = orderReference,
                        productName = productName,
                        memberName = memberName,
                        quantity = request.Quantity,
                        nextUrl = (string)null
                    });

                case "AwaitingExchangePayment":
                    return Json(new
                    {
                        success = true,
                        canContinue = false,
                        status = request.Status,
                        title = "Exchange payment outstanding",
                        message =
                            "The member must pay the exchange price difference before the exchange can be completed.",
                        referenceNumber = request.ReferenceNumber,
                        orderReference = orderReference,
                        productName = productName,
                        memberName = memberName,
                        quantity = request.Quantity,
                        nextUrl = (string)null
                    });

                case "ReadyForExchange":
                    return Json(new
                    {
                        success = true,
                        canContinue = true,
                        status = request.Status,
                        title = "Exchange ready",
                        message =
                            $"{productName} has been verified. Opening the exchange handover form.",
                        referenceNumber = request.ReferenceNumber,
                        orderReference = orderReference,
                        productName = productName,
                        memberName = memberName,
                        quantity = request.Quantity,
                        nextUrl = Url.Action(
                            "CompleteExchange",
                            "StoreAdmin",
                            new { id = request.ReturnRequestId })
                    });

                case "RefundProcessing":
                    return Json(new
                    {
                        success = true,
                        canContinue = false,
                        status = request.Status,
                        title = "Refund processing",
                        message =
                            "This return has been approved and its refund is currently being processed.",
                        referenceNumber = request.ReferenceNumber,
                        orderReference = orderReference,
                        productName = productName,
                        memberName = memberName,
                        quantity = request.Quantity,
                        nextUrl = (string)null
                    });

                case "Approved":
                case "Refunded":
                    return Json(new
                    {
                        success = true,
                        canContinue = false,
                        status = request.Status,
                        title = "Return completed",
                        message =
                            "This return was already approved and processed. It cannot be inspected again.",
                        referenceNumber = request.ReferenceNumber,
                        orderReference = orderReference,
                        productName = productName,
                        memberName = memberName,
                        quantity = request.Quantity,
                        nextUrl = (string)null
                    });

                case "ExchangeCompleted":
                    return Json(new
                    {
                        success = true,
                        canContinue = false,
                        status = request.Status,
                        title = "Exchange already completed",
                        message =
                            "The replacement product has already been handed to the member.",
                        referenceNumber = request.ReferenceNumber,
                        orderReference = orderReference,
                        productName = productName,
                        memberName = memberName,
                        quantity = request.Quantity,
                        nextUrl = (string)null
                    });

                case "Rejected":
                    return Json(new
                    {
                        success = true,
                        canContinue = false,
                        status = request.Status,
                        title = "Request rejected",
                        message =
                            "This return or exchange request was rejected and cannot be inspected again.",
                        referenceNumber = request.ReferenceNumber,
                        orderReference = orderReference,
                        productName = productName,
                        memberName = memberName,
                        quantity = request.Quantity,
                        nextUrl = (string)null
                    });

                case "Cancelled":
                    return Json(new
                    {
                        success = true,
                        canContinue = false,
                        status = request.Status,
                        title = "Request cancelled",
                        message =
                            "This return or exchange request was cancelled.",
                        referenceNumber = request.ReferenceNumber,
                        orderReference = orderReference,
                        productName = productName,
                        memberName = memberName,
                        quantity = request.Quantity,
                        nextUrl = (string)null
                    });

                default:
                    return Json(new
                    {
                        success = true,
                        canContinue = false,
                        status = request.Status,
                        title = "Request verified",
                        message =
                            $"The QR is valid, but the request is currently in the '{request.Status}' stage.",
                        referenceNumber = request.ReferenceNumber,
                        orderReference = orderReference,
                        productName = productName,
                        memberName = memberName,
                        quantity = request.Quantity,
                        nextUrl = (string)null
                    });
            }
        }
        public ActionResult UncollectedOrders(string search, string status)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            new UncollectedOrderService(db).ProcessDueOrders();

            var query = db.StoreOrders
                .Include(o => o.Member)
                .Include(o => o.Items.Select(i => i.Product))
                .Where(o =>
                    o.Status == "ReadyForPickup" ||
                    o.Status == "UncollectedPendingReview" ||
                    o.Status == "UncollectedRefundPending" ||
                    o.Status == "UncollectedRefunded" ||
                    o.Status == "UncollectedResolved");

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(o =>
                    o.ReferenceNumber.Contains(search) ||
                    o.Member.FullName.Contains(search) ||
                    o.Member.Email.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(o => o.Status == status);

            var orders = query
                .OrderBy(o => o.PickupDeadline)
                .ToList();

            var now = DateTime.Now;
            var monthStart = new DateTime(now.Year, now.Month, 1);

            var model = new UncollectedOrdersPageViewModel
            {
                Orders = orders,
                Search = search,
                Status = status,
                IsAdmin = IsAdminLoggedIn(),
                ApproachingDeadlineCount = orders.Count(o =>
                    o.Status == "ReadyForPickup" &&
                    o.PickupDeadline.HasValue &&
                    o.PickupDeadline.Value > now &&
                    o.PickupDeadline.Value <= now.AddHours(24)),
                OverdueCount = orders.Count(o =>
                    o.Status == "ReadyForPickup" &&
                    o.PickupDeadline.HasValue &&
                    o.PickupDeadline.Value <= now),
                AwaitingReviewCount = orders.Count(o =>
                    o.Status == "UncollectedPendingReview"),
                RefundPendingCount = orders.Count(o =>
                    o.Status == "UncollectedRefundPending"),
                ResolvedThisMonthCount = db.StoreOrders.Count(o =>
                    o.UncollectedResolvedAt.HasValue &&
                    o.UncollectedResolvedAt.Value >= monthStart)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProcessDueUncollectedOrders()
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            new UncollectedOrderService(db).ProcessDueOrders();
            TempData["Success"] = "Ready-order deadlines and reminders were checked.";
            return RedirectToAction("UncollectedOrders");
        }

        public ActionResult ReviewUncollectedOrder(int id)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Admin");

            var order = db.StoreOrders
                .Include(o => o.Member)
                .Include(o => o.Items.Select(i => i.Product))
                .FirstOrDefault(o => o.StoreOrderId == id);

            if (order == null) return HttpNotFound();

            var events = db.StoreOrderCollectionEvents
                .Where(e => e.StoreOrderId == id)
                .OrderByDescending(e => e.CreatedAt)
                .ToList();

            var problems = order.Items
                .Where(i => i.Product == null || !i.Product.IsActive ||
                            i.Product.QuantityOnHand < i.Quantity)
                .Select(i => i.Product == null
                    ? "Unknown product is unavailable."
                    : i.Product.Name + " requires " + i.Quantity +
                      " but only " + i.Product.QuantityOnHand + " is available.")
                .ToList();

            return View(new ReviewUncollectedOrderViewModel
            {
                Order = order,
                Events = events,
                IsAdmin = IsAdminLoggedIn(),
                CanReReserveStock = !problems.Any(),
                StockProblems = problems
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExtendUncollectedDeadline(
            int orderId, DateTime newDeadline, string reason)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            string actor = Session["LoggedInAdmin"]?.ToString() ?? "Admin";
            var result = new UncollectedOrderService(db)
                .ExtendDeadline(orderId, newDeadline, reason, actor);

            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction("ReviewUncollectedOrder", new { id = orderId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResolveUncollectedWithoutRefund(int orderId, string reason)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            string actor = Session["LoggedInAdmin"]?.ToString() ?? "Admin";
            var result = new UncollectedOrderService(db)
                .ResolveWithoutRefund(orderId, reason, actor);

            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction("ReviewUncollectedOrder", new { id = orderId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RefundUncollectedOrder(
            int orderId, decimal refundAmount, string reason)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "A refund reason is required.";
                return RedirectToAction("ReviewUncollectedOrder", new { id = orderId });
            }

            var order = db.StoreOrders
                .Include(o => o.Member)
                .FirstOrDefault(o => o.StoreOrderId == orderId);

            if (order == null) return HttpNotFound();

            if (order.Status != "UncollectedPendingReview" &&
                order.Status != "UncollectedRefundPending")
            {
                TempData["Error"] = "This order is not awaiting an uncollected-order refund.";
                return RedirectToAction("ReviewUncollectedOrder", new { id = orderId });
            }

            if (!string.IsNullOrWhiteSpace(order.UncollectedRefundReference) ||
                order.UncollectedRefundedAt.HasValue)
            {
                TempData["Error"] = "A refund has already been recorded for this order.";
                return RedirectToAction("ReviewUncollectedOrder", new { id = orderId });
            }

            if (refundAmount <= 0 || refundAmount > order.FinalTotal)
            {
                TempData["Error"] =
                    "The refund must be greater than zero and cannot exceed R" +
                    order.FinalTotal.ToString("F2") + ".";
                return RedirectToAction("ReviewUncollectedOrder", new { id = orderId });
            }

            if (string.IsNullOrWhiteSpace(order.PaystackReference))
            {
                TempData["Error"] = "The original Paystack reference is missing.";
                return RedirectToAction("ReviewUncollectedOrder", new { id = orderId });
            }

            string secretKey = WebConfigurationManager.AppSettings["Paystack:SecretKey"];
            if (string.IsNullOrWhiteSpace(secretKey))
            {
                TempData["Error"] = "Paystack is not configured.";
                return RedirectToAction("ReviewUncollectedOrder", new { id = orderId });
            }

            // Mark pending first so a second Admin cannot start another refund.
            order.Status = "UncollectedRefundPending";
            order.UncollectedResolutionStatus = "RefundPending";
            order.UncollectedResolutionNotes = reason.Trim();
            db.StoreOrderCollectionEvents.Add(new StoreOrderCollectionEvent
            {
                StoreOrderId = order.StoreOrderId,
                EventType = "RefundRequested",
                Description = "Refund of R" + refundAmount.ToString("F2") + " requested. " + reason.Trim(),
                PerformedByRole = "Admin",
                PerformedBy = Session["LoggedInAdmin"]?.ToString() ?? "Admin"
            });
            db.SaveChanges();

            string paystackRefundReference;
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", secretKey);

                    var payload = new JObject
                    {
                        ["transaction"] = order.PaystackReference,
                        ["amount"] = (int)Math.Round(
                            refundAmount * 100, 0, MidpointRounding.AwayFromZero)
                    };

                    var content = new StringContent(
                        payload.ToString(), System.Text.Encoding.UTF8, "application/json");
                    var response = await client.PostAsync("https://api.paystack.co/refund", content);
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(responseBody);

                    if (!response.IsSuccessStatusCode || json["status"]?.Value<bool>() != true)
                    {
                        order.Status = "UncollectedPendingReview";
                        order.UncollectedResolutionStatus = "RefundFailed";
                        db.SaveChanges();
                        TempData["Error"] = "Paystack refund failed: " +
                            (json["message"]?.ToString() ?? "Unknown error.");
                        return RedirectToAction("ReviewUncollectedOrder", new { id = orderId });
                    }

                    paystackRefundReference = json["data"]?["id"]?.ToString();
                    if (string.IsNullOrWhiteSpace(paystackRefundReference))
                        paystackRefundReference = "PAYSTACK-ACCEPTED-" + DateTime.Now.Ticks;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[UNCOLLECTED REFUND ERROR] " + ex);
                order.Status = "UncollectedPendingReview";
                order.UncollectedResolutionStatus = "RefundFailed";
                db.SaveChanges();
                TempData["Error"] = "The refund could not be submitted. You may retry safely.";
                return RedirectToAction("ReviewUncollectedOrder", new { id = orderId });
            }

            string admin = Session["LoggedInAdmin"]?.ToString() ?? "Admin";
            order.Status = "UncollectedRefunded";
            order.UncollectedResolutionStatus = "Refunded";
            order.UncollectedRefundAmount = refundAmount;
            order.UncollectedRefundReference = paystackRefundReference;
            order.UncollectedRefundedAt = DateTime.Now;
            order.UncollectedResolvedAt = DateTime.Now;
            order.UncollectedResolvedBy = admin;
            order.UncollectedResolutionNotes = reason.Trim();

            db.StoreOrderCollectionEvents.Add(new StoreOrderCollectionEvent
            {
                StoreOrderId = order.StoreOrderId,
                EventType = "RefundProcessed",
                Description = "Paystack accepted a refund of R" + refundAmount.ToString("F2") + ".",
                PerformedByRole = "Admin",
                PerformedBy = admin
            });
            db.SaveChanges();

            try
            {
                EmailService.SendUncollectedOrderRefunded(
                    order.Member.Email, order.Member.FullName, order.ReferenceNumber,
                    refundAmount, paystackRefundReference);
                new NotificationService(db).Notify(
                    "Member", order.MemberId, "Uncollected Order Refunded",
                    "A refund of R" + refundAmount.ToString("F2") +
                    " was submitted for order " + order.ReferenceNumber + ".",
                    "Success", "/Store/OrderConfirmation/" + order.StoreOrderId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[UNCOLLECTED REFUND NOTIFY] " + ex);
            }

            TempData["Success"] = "Paystack accepted the refund of R" +
                refundAmount.ToString("F2") + ".";
            return RedirectToAction("ReviewUncollectedOrder", new { id = orderId });
        }
        // ================================================================
        // STORE REPORTING AND HISTORY
        // These pages are read-only and may be accessed by Admin or
        // authenticated Store Staff.
        // ================================================================

        // ================================================================
        // MAIN REPORTING DASHBOARD
        // GET: StoreAdmin/StoreReports
        // ================================================================

        [HttpGet]
        public ActionResult StoreReports()
        {
            if (!IsAuthorized())
            {
                return RedirectToAction("Login", "Admin");
            }

            var reportService =
                new StoreReportService(db);

            StoreReportsDashboardViewModel model =
                reportService.BuildDashboard();

            return View(model);
        }

        // ================================================================
        // ORDER HISTORY
        // GET: StoreAdmin/OrderHistory
        // ================================================================

        [HttpGet]
        public ActionResult OrderHistory(
            string search = null,
            string status = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool discountOnly = false,
            decimal? minimumAmount = null,
            decimal? maximumAmount = null)
        {
            if (!IsAuthorized())
            {
                return RedirectToAction("Login", "Admin");
            }

            search = CleanFilterValue(search);
            status = CleanFilterValue(status);

            if (fromDate.HasValue &&
                toDate.HasValue &&
                fromDate.Value.Date > toDate.Value.Date)
            {
                TempData["Error"] =
                    "The start date cannot be later than the end date.";

                return RedirectToAction("OrderHistory");
            }

            if (minimumAmount.HasValue &&
                minimumAmount.Value < 0)
            {
                TempData["Error"] =
                    "The minimum amount cannot be negative.";

                return RedirectToAction("OrderHistory");
            }

            if (maximumAmount.HasValue &&
                maximumAmount.Value < 0)
            {
                TempData["Error"] =
                    "The maximum amount cannot be negative.";

                return RedirectToAction("OrderHistory");
            }

            if (minimumAmount.HasValue &&
                maximumAmount.HasValue &&
                minimumAmount.Value > maximumAmount.Value)
            {
                TempData["Error"] =
                    "The minimum amount cannot be greater than the maximum amount.";

                return RedirectToAction("OrderHistory");
            }

            var reportService =
                new StoreReportService(db);

            OrderHistoryPageViewModel model =
                reportService.BuildOrderHistoryPage(
                    search,
                    status,
                    fromDate,
                    toDate,
                    discountOnly,
                    minimumAmount,
                    maximumAmount);

            return View(model);
        }

        // ================================================================
        // INDIVIDUAL ORDER HISTORY RECORD
        // GET: StoreAdmin/OrderHistoryDetails/5
        // ================================================================

        [HttpGet]
        public ActionResult OrderHistoryDetails(int id)
        {
            if (!IsAuthorized())
            {
                return RedirectToAction("Login", "Admin");
            }

            var reportService =
                new StoreReportService(db);

            OrderHistoryDetailsViewModel model =
                reportService.BuildOrderHistoryDetails(id);

            if (model == null || model.Order == null)
            {
                TempData["Error"] =
                    "The requested store order record could not be found.";

                return RedirectToAction("OrderHistory");
            }

            return View(model);
        }

        // ================================================================
        // COLLECTION HISTORY
        // GET: StoreAdmin/CollectionHistory
        // ================================================================

        [HttpGet]
        public ActionResult CollectionHistory(
            string search = null,
            string outcome = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            if (!IsAuthorized())
            {
                return RedirectToAction("Login", "Admin");
            }

            search = CleanFilterValue(search);
            outcome = CleanFilterValue(outcome);

            if (fromDate.HasValue &&
                toDate.HasValue &&
                fromDate.Value.Date > toDate.Value.Date)
            {
                TempData["Error"] =
                    "The start date cannot be later than the end date.";

                return RedirectToAction("CollectionHistory");
            }

            var reportService =
                new StoreReportService(db);

            CollectionHistoryPageViewModel model =
                reportService.BuildCollectionHistoryPage(
                    search,
                    outcome,
                    fromDate,
                    toDate);

            return View(model);
        }

        // ================================================================
        // INDIVIDUAL COLLECTION HISTORY RECORD
        // GET: StoreAdmin/CollectionHistoryDetails/5
        // ================================================================

        [HttpGet]
        public ActionResult CollectionHistoryDetails(int id)
        {
            if (!IsAuthorized())
            {
                return RedirectToAction("Login", "Admin");
            }

            var reportService =
                new StoreReportService(db);

            CollectionHistoryDetailsViewModel model =
                reportService.BuildCollectionHistoryDetails(id);

            if (model == null || model.Order == null)
            {
                TempData["Error"] =
                    "The requested collection record could not be found.";

                return RedirectToAction("CollectionHistory");
            }

            return View(model);
        }

        // ================================================================
        // INVENTORY MOVEMENT HISTORY
        // GET: StoreAdmin/InventoryMovementHistory
        // ================================================================

        [HttpGet]
        public ActionResult InventoryMovementHistory(
            string search = null,
            string movementType = null,
            string referenceType = null,
            int? productId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            if (!IsAuthorized())
            {
                return RedirectToAction("Login", "Admin");
            }

            search = CleanFilterValue(search);
            movementType = CleanFilterValue(movementType);
            referenceType = CleanFilterValue(referenceType);

            if (fromDate.HasValue &&
                toDate.HasValue &&
                fromDate.Value.Date > toDate.Value.Date)
            {
                TempData["Error"] =
                    "The start date cannot be later than the end date.";

                return RedirectToAction(
                    "InventoryMovementHistory");
            }

            if (productId.HasValue &&
                !db.Products.Any(p =>
                    p.ProductId == productId.Value))
            {
                TempData["Error"] =
                    "The selected product could not be found.";

                return RedirectToAction(
                    "InventoryMovementHistory");
            }

            PopulateInventoryReportFilters(
                productId,
                movementType,
                referenceType);

            var reportService =
                new StoreReportService(db);

            InventoryMovementHistoryPageViewModel model =
                reportService.BuildInventoryMovementHistoryPage(
                    search,
                    movementType,
                    referenceType,
                    productId,
                    fromDate,
                    toDate);

            return View(model);
        }
        // ================================================================
        // REPORT FILTER HELPERS
        // ================================================================

        private static string CleanFilterValue(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private void PopulateInventoryReportFilters(
            int? selectedProductId = null,
            string selectedMovementType = null,
            string selectedReferenceType = null)
        {
            var products = db.Products
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .Select(p => new
                {
                    p.ProductId,
                    p.Name
                })
                .ToList();

            ViewBag.Products = new SelectList(
                products,
                "ProductId",
                "Name",
                selectedProductId);

            var movementTypes = db.InventoryMovements
                .AsNoTracking()
                .Where(m =>
                    m.MovementType != null &&
                    m.MovementType != "")
                .Select(m => m.MovementType)
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            ViewBag.MovementTypes = new SelectList(
                movementTypes,
                selectedMovementType);

            var referenceTypes = db.InventoryMovements
                .AsNoTracking()
                .Where(m =>
                    m.ReferenceType != null &&
                    m.ReferenceType != "")
                .Select(m => m.ReferenceType)
                .Distinct()
                .OrderBy(r => r)
                .ToList();

            ViewBag.ReferenceTypes = new SelectList(
                referenceTypes,
                selectedReferenceType);
        }
        // Add these namespaces to StoreAdminController.cs:
        // using SwiftyS26.Services;
        // using System.Globalization;

        // Paste these actions INSIDE StoreAdminController, before Dispose().

        [HttpGet]
        public ActionResult ExportOrderHistoryPdf(string search = null, string status = null,
            DateTime? fromDate = null, DateTime? toDate = null, bool discountOnly = false,
            decimal? minimumAmount = null, decimal? maximumAmount = null)
        {
            if (!IsAuthorized()) return RedirectToAction("Login", "Admin");
            var service = new StoreReportService(db);
            var rows = service.GetOrderHistoryQuery(search, status, fromDate, toDate,
                discountOnly, minimumAmount, maximumAmount).ToList();
            var bytes = StoreReportExportService.OrderHistoryPdf(rows, ReportUser());
            return File(bytes, "application/pdf", "GymHub_Order_History_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".pdf");
        }

        [HttpGet]
        public ActionResult ExportOrderHistoryExcel(string search = null, string status = null,
            DateTime? fromDate = null, DateTime? toDate = null, bool discountOnly = false,
            decimal? minimumAmount = null, decimal? maximumAmount = null)
        {
            if (!IsAuthorized()) return RedirectToAction("Login", "Admin");
            var service = new StoreReportService(db);
            var rows = service.GetOrderHistoryQuery(search, status, fromDate, toDate,
                discountOnly, minimumAmount, maximumAmount).ToList();
            var bytes = StoreReportExportService.OrderHistoryExcel(rows, ReportUser());
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "GymHub_Order_History_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xlsx");
        }

        [HttpGet]
        public ActionResult ExportCollectionHistoryPdf(string search = null, string outcome = null,
            DateTime? fromDate = null, DateTime? toDate = null)
        {
            if (!IsAuthorized()) return RedirectToAction("Login", "Admin");
            var service = new StoreReportService(db);
            var rows = service.GetCollectionHistoryQuery(search, outcome, fromDate, toDate).ToList();
            var bytes = StoreReportExportService.CollectionHistoryPdf(rows, ReportUser());
            return File(bytes, "application/pdf", "GymHub_Collection_History_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".pdf");
        }

        [HttpGet]
        public ActionResult ExportCollectionHistoryExcel(string search = null, string outcome = null,
            DateTime? fromDate = null, DateTime? toDate = null)
        {
            if (!IsAuthorized()) return RedirectToAction("Login", "Admin");
            var service = new StoreReportService(db);
            var rows = service.GetCollectionHistoryQuery(search, outcome, fromDate, toDate).ToList();
            var bytes = StoreReportExportService.CollectionHistoryExcel(rows, ReportUser());
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "GymHub_Collection_History_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xlsx");
        }

        [HttpGet]
        public ActionResult ExportInventoryMovementHistoryPdf(string search = null,
            string movementType = null, string referenceType = null, int? productId = null,
            DateTime? fromDate = null, DateTime? toDate = null)
        {
            if (!IsAuthorized()) return RedirectToAction("Login", "Admin");
            var service = new StoreReportService(db);
            var rows = service.GetInventoryMovementQuery(search, movementType, referenceType,
                productId, fromDate, toDate).ToList();
            var bytes = StoreReportExportService.InventoryHistoryPdf(rows, ReportUser());
            return File(bytes, "application/pdf", "GymHub_Inventory_History_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".pdf");
        }

        [HttpGet]
        public ActionResult ExportInventoryMovementHistoryExcel(string search = null,
            string movementType = null, string referenceType = null, int? productId = null,
            DateTime? fromDate = null, DateTime? toDate = null)
        {
            if (!IsAuthorized()) return RedirectToAction("Login", "Admin");
            var service = new StoreReportService(db);
            var rows = service.GetInventoryMovementQuery(search, movementType, referenceType,
                productId, fromDate, toDate).ToList();
            var bytes = StoreReportExportService.InventoryHistoryExcel(rows, ReportUser());
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "GymHub_Inventory_History_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xlsx");
        }

        [HttpGet]
        public ActionResult ExportOrderRecordPdf(int id)
        {
            if (!IsAuthorized()) return RedirectToAction("Login", "Admin");
            var model = new StoreReportService(db).BuildOrderHistoryDetails(id);
            if (model == null || model.Order == null) return HttpNotFound();
            var bytes = StoreReportExportService.OrderRecordPdf(model, ReportUser());
            return File(bytes, "application/pdf", "GymHub_Order_" + model.Order.ReferenceNumber + ".pdf");
        }

        [HttpGet]
        public ActionResult ExportCollectionRecordPdf(int id)
        {
            if (!IsAuthorized()) return RedirectToAction("Login", "Admin");
            var model = new StoreReportService(db).BuildCollectionHistoryDetails(id);
            if (model == null || model.Order == null) return HttpNotFound();
            var bytes = StoreReportExportService.CollectionRecordPdf(model, ReportUser());
            return File(bytes, "application/pdf", "GymHub_Collection_" + model.Order.ReferenceNumber + ".pdf");
        }

        private string ReportUser()
        {
            if (Session["LoggedInAdmin"] != null) return "GymHub Admin #" + Session["LoggedInAdmin"];
            if (Session["LoggedInStoreStaff"] != null) return "Store Staff #" + Session["LoggedInStoreStaff"];
            return "GymHub System";
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
