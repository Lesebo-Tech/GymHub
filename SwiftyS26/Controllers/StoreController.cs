using SwiftyS26.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Configuration;
using System.Web.Mvc;
using Newtonsoft.Json.Linq;

namespace SwiftyS26.Controllers
{
    public class StoreController : Controller
    {
        private GymHubDB db = new GymHubDB();

        // GET: Store — browse/search/category filter
        public ActionResult Index(string category, string search)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            var query = db.Products.Where(p => p.IsActive).AsQueryable();

            if (!string.IsNullOrWhiteSpace(category) && ProductHelper.IsValidCategory(category))
                query = query.Where(p => p.Category == category);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.Name.Contains(search) || p.Description.Contains(search));

            // Include Promotions so IsOnPromotion / DisplayPrice / ActivePromotion
            // actually have data to work with — without this they silently
            // fall back to NormalPrice for every product.
            var products = query.Include(p => p.Promotions).OrderBy(p => p.Name).ToList();

            int currentMemberId = (int)Session["LoggedInMember"];
            ViewBag.CartCount = db.ShoppingCartItems.Count(c => c.MemberId == currentMemberId);
            ViewBag.Categories = ProductHelper.Categories;
            ViewBag.CurrentCategory = category;
            ViewBag.CurrentSearch = search;

            return View(products);
        }

        // POST: Store/AddToCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddToCart(int productId, int quantity = 1)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            int currentMemberId = (int)Session["LoggedInMember"];

            var product = db.Products.Find(productId);
            if (product == null || !product.IsActive)
            {
                TempData["Error"] = "This product is no longer available.";
                return RedirectToAction("Index");
            }

            if (quantity < 1) quantity = 1;

            var existing = db.ShoppingCartItems
                .FirstOrDefault(c => c.MemberId == currentMemberId && c.ProductId == productId);

            if (existing != null)
            {
                existing.Quantity += quantity;
                db.Entry(existing).State = EntityState.Modified;
            }
            else
            {
                db.ShoppingCartItems.Add(new ShoppingCartItem
                {
                    MemberId = currentMemberId,
                    ProductId = productId,
                    Quantity = quantity,
                    AddedAt = DateTime.Now
                });
            }

            db.SaveChanges();

            TempData["Success"] = $"{product.Name} added to your cart.";
            return RedirectToAction("Index");
        }

        // GET: Store/Cart
        public ActionResult Cart()
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            int currentMemberId = (int)Session["LoggedInMember"];

            var items = db.ShoppingCartItems
                .Include(c => c.Product.Promotions)
                .Where(c => c.MemberId == currentMemberId)
                .OrderByDescending(c => c.AddedAt)
                .ToList();

            // Uses DisplayPrice (promotional if active) rather than
            // LineTotal, which only ever reflects NormalPrice.
            ViewBag.Subtotal = items.Sum(c => c.Product != null ? c.Product.DisplayPrice * c.Quantity : 0);
            return View(items);
        }

        // POST: Store/UpdateCartQuantity
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateCartQuantity(int cartItemId, int quantity)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            int currentMemberId = (int)Session["LoggedInMember"];

            var item = db.ShoppingCartItems.FirstOrDefault(c => c.ShoppingCartItemId == cartItemId && c.MemberId == currentMemberId);
            if (item == null)
            {
                TempData["Error"] = "Cart item not found.";
                return RedirectToAction("Cart");
            }

            if (quantity < 1)
            {
                db.ShoppingCartItems.Remove(item);
            }
            else
            {
                item.Quantity = quantity;
                db.Entry(item).State = EntityState.Modified;
            }

            db.SaveChanges();
            return RedirectToAction("Cart");
        }

        // POST: Store/RemoveFromCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveFromCart(int cartItemId)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            int currentMemberId = (int)Session["LoggedInMember"];

            var item = db.ShoppingCartItems.FirstOrDefault(c => c.ShoppingCartItemId == cartItemId && c.MemberId == currentMemberId);
            if (item != null)
            {
                db.ShoppingCartItems.Remove(item);
                db.SaveChanges();
            }

            TempData["Success"] = "Item removed from cart.";
            return RedirectToAction("Cart");
        }

        // GET: Store/Checkout
        public ActionResult Checkout()
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            int currentMemberId = (int)Session["LoggedInMember"];
            var member = db.Members.Find(currentMemberId);
            if (member == null)
                return RedirectToAction("Login", "Forms");

            var cartItems = db.ShoppingCartItems
                .Include(c => c.Product.Promotions)
                .Where(c => c.MemberId == currentMemberId)
                .ToList();

            if (!cartItems.Any())
            {
                TempData["Error"] = "Your cart is empty.";
                return RedirectToAction("Cart");
            }

            var unavailable = cartItems.Where(c => c.Product == null || !c.Product.IsActive || c.Product.QuantityOnHand < c.Quantity).ToList();
            if (unavailable.Any())
            {
                TempData["Error"] = $"\"{unavailable.First().Product?.Name ?? "An item"}\" no longer has enough stock. Please update your cart.";
                return RedirectToAction("Cart");
            }

            // DisplayPrice (promotional if active) drives the subtotal —
            // NOT LineTotal / NormalPrice.
            decimal subtotal = cartItems.Sum(c => c.Product.DisplayPrice * c.Quantity);
            decimal discount = StoreHelper.CalculateDiscount(subtotal, member);

            ViewBag.CartItems = cartItems;
            ViewBag.Subtotal = subtotal;
            ViewBag.Discount = discount;
            ViewBag.FinalTotal = subtotal - discount;
            ViewBag.IsPremium = StoreHelper.IsPremiumMember(member);

            return View();
        }

        // POST: Store/InitiatePayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> InitiatePayment()
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            int currentMemberId = (int)Session["LoggedInMember"];
            var member = db.Members.Find(currentMemberId);
            if (member == null)
                return RedirectToAction("Login", "Forms");

            var cartItems = db.ShoppingCartItems
                .Include(c => c.Product.Promotions)
                .Where(c => c.MemberId == currentMemberId)
                .ToList();

            if (!cartItems.Any())
            {
                TempData["Error"] = "Your cart is empty.";
                return RedirectToAction("Cart");
            }

            decimal subtotal = cartItems.Sum(c => c.Product.DisplayPrice * c.Quantity);
            decimal discount = StoreHelper.CalculateDiscount(subtotal, member);
            decimal finalTotal = subtotal - discount;

            string secretKey = WebConfigurationManager.AppSettings["Paystack:SecretKey"];
            string callbackUrl = Url.Action("PaymentCallback", "Store", null, Request.Url.Scheme);

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", secretKey);

                var payload = new JObject
                {
                    ["email"] = member.Email,
                    ["amount"] = (int)(finalTotal * 100), // Paystack expects kobo/cents
                    ["callback_url"] = callbackUrl,
                    ["metadata"] = new JObject { ["member_id"] = currentMemberId }
                };

                var content = new System.Net.Http.StringContent(payload.ToString(), System.Text.Encoding.UTF8, "application/json");
                var response = await client.PostAsync("https://api.paystack.co/transaction/initialize", content);
                var responseBody = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(responseBody);

                if (json["status"]?.Value<bool>() == true)
                {
                    string authUrl = json["data"]["authorization_url"].ToString();
                    return Redirect(authUrl);
                }

                TempData["Error"] = "Could not start payment. Please try again.";
                return RedirectToAction("Checkout");
            }
        }

        // GET: Store/PaymentCallback — Paystack redirects here after payment
        // ============================================================
        // PAYMENT CALLBACK
        // Paystack redirects the member here after payment.
        // ============================================================
        [HttpGet]
        public async Task<ActionResult> PaymentCallback(string reference)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            if (string.IsNullOrWhiteSpace(reference))
            {
                TempData["Error"] = "The payment reference is missing.";
                return RedirectToAction("Cart");
            }

            int currentMemberId = (int)Session["LoggedInMember"];

            // Prevent a refreshed callback from creating another order.
            var previouslyCreatedOrder = db.StoreOrders
                .FirstOrDefault(o =>
                    o.MemberId == currentMemberId &&
                    o.PaystackReference == reference);

            if (previouslyCreatedOrder != null)
            {
                TempData["Success"] = "This payment has already been processed.";

                return RedirectToAction(
                    "OrderConfirmation",
                    new { id = previouslyCreatedOrder.StoreOrderId });
            }

            string secretKey =
                WebConfigurationManager.AppSettings["Paystack:SecretKey"];

            if (string.IsNullOrWhiteSpace(secretKey))
            {
                TempData["Error"] =
                    "The payment service is not configured. Please contact support.";

                return RedirectToAction("Cart");
            }

            // ========================================================
            // VERIFY PAYMENT WITH PAYSTACK
            // ========================================================

            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue(
                            "Bearer",
                            secretKey);

                    var response = await client.GetAsync(
                        "https://api.paystack.co/transaction/verify/" +
                        Uri.EscapeDataString(reference));

                    var responseBody =
                        await response.Content.ReadAsStringAsync();

                    JObject json;

                    try
                    {
                        json = JObject.Parse(responseBody);
                    }
                    catch
                    {
                        TempData["Error"] =
                            "The payment service returned an invalid response.";

                        return RedirectToAction("Cart");
                    }

                    bool verified =
                        response.IsSuccessStatusCode &&
                        json["status"]?.Value<bool>() == true &&
                        string.Equals(
                            json["data"]?["status"]?.ToString(),
                            "success",
                            StringComparison.OrdinalIgnoreCase);

                    if (!verified)
                    {
                        TempData["Error"] =
                            "Payment could not be verified. Please try again or contact support.";

                        return RedirectToAction("Cart");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[PAYSTACK VERIFICATION ERROR] " + ex);

                TempData["Error"] =
                    "Payment verification is temporarily unavailable. Please try again.";

                return RedirectToAction("Cart");
            }

            // Check again after verification in case another callback completed
            // while the Paystack request was running.
            previouslyCreatedOrder = db.StoreOrders
                .FirstOrDefault(o =>
                    o.MemberId == currentMemberId &&
                    o.PaystackReference == reference);

            if (previouslyCreatedOrder != null)
            {
                return RedirectToAction(
                    "OrderConfirmation",
                    new { id = previouslyCreatedOrder.StoreOrderId });
            }

            var member = db.Members.Find(currentMemberId);

            if (member == null)
            {
                TempData["Error"] = "The member account could not be found.";
                return RedirectToAction("Cart");
            }

            var cartItems = db.ShoppingCartItems
                .Include(c => c.Product.Promotions)
                .Where(c => c.MemberId == currentMemberId)
                .ToList();

            if (!cartItems.Any())
            {
                TempData["Error"] =
                    "Your cart is empty or this payment has already been processed.";

                return RedirectToAction("MyOrders");
            }

            // Revalidate stock after Paystack verification.
            foreach (var cartItem in cartItems)
            {
                if (cartItem.Product == null ||
                    !cartItem.Product.IsActive)
                {
                    TempData["Error"] =
                        "One or more products are no longer available.";

                    return RedirectToAction("Cart");
                }

                if (cartItem.Quantity < 1 ||
                    cartItem.Product.QuantityOnHand < cartItem.Quantity)
                {
                    TempData["Error"] =
                        $"{cartItem.Product.Name} no longer has enough stock.";

                    return RedirectToAction("Cart");
                }
            }

            decimal subtotal = cartItems.Sum(c =>
                c.Product.DisplayPrice * c.Quantity);

            decimal discount =
                StoreHelper.CalculateDiscount(subtotal, member);

            decimal finalTotal = subtotal - discount;

            StoreOrder order;

            // ========================================================
            // CREATE ORDER, RESERVE STOCK AND RECORD STOCK MOVEMENTS
            // ========================================================

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    order = new StoreOrder
                    {
                        MemberId = currentMemberId,

                        ReferenceNumber =
                            StoreHelper.GenerateReferenceNumber(db),

                        Subtotal = subtotal,
                        DiscountAmount = discount,
                        FinalTotal = finalTotal,

                        PaystackReference = reference,

                        Status = "Paid",
                        PaidAt = DateTime.Now,

                        // The stock is reserved immediately after payment.
                        IsPickupStockReserved = true
                    };

                    db.StoreOrders.Add(order);

                    // Save once to obtain StoreOrderId.
                    db.SaveChanges();

                    foreach (var cartItem in cartItems)
                    {
                        decimal purchasePrice =
                            cartItem.Product.DisplayPrice;

                        db.StoreOrderItems.Add(
                            new StoreOrderItem
                            {
                                StoreOrderId = order.StoreOrderId,
                                ProductId = cartItem.ProductId,
                                Quantity = cartItem.Quantity,

                                // Historical purchase price snapshot.
                                UnitPriceAtPurchase = purchasePrice
                            });

                        // Reserve stock immediately.
                        cartItem.Product.QuantityOnHand -=
                            cartItem.Quantity;

                        db.Entry(cartItem.Product).State =
                            EntityState.Modified;

                        // Record the inventory movement.
                        db.InventoryMovements.Add(
                            new InventoryMovement
                            {
                                ProductId = cartItem.ProductId,

                                // Negative value because stock is leaving
                                // available inventory.
                                QuantityChange = -cartItem.Quantity,

                                MovementType =
                                    "StoreOrderReservation",

                                ReferenceType = "StoreOrder",

                                ReferenceId =
                                    order.StoreOrderId,

                                ReferenceNumber =
                                    order.ReferenceNumber,

                                Notes =
                                    "Stock reserved after verified Paystack payment.",

                                PerformedBy =
                                    "GymHub System",

                                CreatedAt =
                                    DateTime.Now
                            });
                    }

                    db.StoreOrderCollectionEvents.Add(
                        new StoreOrderCollectionEvent
                        {
                            StoreOrderId = order.StoreOrderId,

                            EventType = "PaymentConfirmed",

                            Description =
                                $"Payment of R{order.FinalTotal:F2} was verified. " +
                                "The order was created and stock was reserved.",

                            PerformedByRole = "System",
                            PerformedBy = "GymHub System",
                            CreatedAt = DateTime.Now
                        });

                    db.ShoppingCartItems.RemoveRange(cartItems);

                    db.SaveChanges();
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();

                    System.Diagnostics.Debug.WriteLine(
                        "[STORE ORDER CREATION ERROR] " + ex);

                    TempData["Error"] =
                        "The payment was verified, but GymHub could not create " +
                        "the order. No inventory changes were saved. " +
                        "Please contact support with payment reference " +
                        reference + ".";

                    return RedirectToAction("Cart");
                }
            }

            // Notifications are sent only after the transaction commits.
            try
            {
                new NotificationService(db).Notify(
                    "Member",
                    currentMemberId,
                    "Order Confirmed",
                    $"Your order {order.ReferenceNumber} has been paid " +
                    "and is being prepared.",
                    "Success",
                    "/Store/OrderConfirmation/" + order.StoreOrderId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[MEMBER ORDER NOTIFICATION ERROR] " + ex);
            }

            try
            {
                ProfessionalStoreEmailService.SendOrderPlaced(
                    member.Email,
                    member.FullName,
                    order.ReferenceNumber,
                    order.FinalTotal);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[ORDER CONFIRMATION EMAIL ERROR] " + ex);
            }

            try
            {
                var notificationService =
                    new NotificationService(db);

                notificationService.NotifyAdmin(
                    "New Store Order",
                    $"Order {order.ReferenceNumber} was placed by " +
                    $"{member.FullName} for R{order.FinalTotal:F2}.",
                    "Info",
                    "/StoreAdmin/Orders");

                notificationService.NotifyAllStoreStaff(
                    "New Order to Prepare",
                    $"Order {order.ReferenceNumber} — " +
                    $"{member.FullName} — R{order.FinalTotal:F2}.",
                    "Info",
                    "/StoreAdmin/Orders");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[STAFF ORDER NOTIFICATION ERROR] " + ex);
            }

            return RedirectToAction(
                "OrderConfirmation",
                new { id = order.StoreOrderId });
        }

        // GET: Store/OrderConfirmation/5
        public ActionResult OrderConfirmation(int id)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            int currentMemberId = (int)Session["LoggedInMember"];

            var order = db.StoreOrders
                .Include(o => o.Items.Select(i => i.Product))
                .FirstOrDefault(o => o.StoreOrderId == id && o.MemberId == currentMemberId);

            if (order == null)
                return RedirectToAction("Index");

            return View(order);
        }

        // ============================================================
        // MEMBER ORDER HISTORY
        // ============================================================
        [HttpGet]
        public ActionResult MyOrders()
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            // This ensures the member sees the current deadline status even
            // when Store Staff has not recently opened the fulfilment page.
            StoreOrderHelper.CheckUncollectedOrders(db);

            int currentMemberId =
                (int)Session["LoggedInMember"];

            var orders = db.StoreOrders
                .Include(o => o.Items.Select(i => i.Product))
                .Where(o =>
                    o.MemberId == currentMemberId &&
                    o.Status != "PendingPayment")
                .OrderByDescending(o => o.CreatedAt)
                .ToList();

            var orderItemIds = orders
                .SelectMany(o =>
                    o.Items.Select(i =>
                        i.StoreOrderItemId))
                .ToList();

            var allReturns = db.ReturnRequests
                .Where(r =>
                    orderItemIds.Contains(
                        r.StoreOrderItemId))
                .OrderByDescending(r =>
                    r.RequestedAt)
                .ToList()
                .GroupBy(r =>
                    r.StoreOrderItemId)
                .ToDictionary(
                    group => group.Key,
                    group => group.First());

            ViewBag.ReturnsByItem =
                allReturns;

            return View(orders);
        }
        // ============================================================
        // REQUEST RETURN — DISPLAY FORM
        // GET: Store/RequestReturn/5
        // ============================================================

        [HttpGet]
        public ActionResult RequestReturn(int id)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            int currentMemberId = (int)Session["LoggedInMember"];

            var member = db.Members.Find(currentMemberId);

            if (member == null)
                return RedirectToAction("Login", "Forms");

            var item = db.StoreOrderItems
                .Include(i => i.Product.Promotions)
                .Include(i => i.StoreOrder)
                .FirstOrDefault(i =>
                    i.StoreOrderItemId == id &&
                    i.StoreOrder.MemberId == currentMemberId);

            if (item == null)
            {
                TempData["Error"] =
                    "The selected order item could not be found.";

                return RedirectToAction("MyOrders");
            }

            if (item.StoreOrder.Status != "Collected")
            {
                TempData["Error"] =
                    "Returns can only be requested for collected orders.";

                return RedirectToAction("MyOrders");
            }

            bool hasOpenRequest = db.ReturnRequests.Any(r =>
                r.StoreOrderItemId == id &&
                ReturnRequestHelper.OpenStatuses.Contains(r.Status));

            if (hasOpenRequest)
            {
                TempData["Error"] =
                    "A return or exchange request for this item is already being processed.";

                return RedirectToAction("MyOrders");
            }

            int previouslyReturnedQuantity = db.ReturnRequests
                .Where(r =>
                    r.StoreOrderItemId == id &&
                    r.Status != "Rejected" &&
                    r.Status != "Cancelled" &&
                    !ReturnRequestHelper.OpenStatuses.Contains(r.Status))
                .Select(r => (int?)r.Quantity)
                .Sum() ?? 0;

            int maximumReturnableQuantity =
                item.Quantity - previouslyReturnedQuantity;

            if (maximumReturnableQuantity <= 0)
            {
                TempData["Error"] =
                    "All units from this order item have already been returned or exchanged.";

                return RedirectToAction("MyOrders");
            }

            ViewBag.MaximumReturnableQuantity =
                maximumReturnableQuantity;

            var exchangeProducts = db.Products
                .Include(p => p.Promotions)
                .Where(p => p.IsActive && p.QuantityOnHand > 0)
                .OrderBy(p => p.Name)
                .ToList();

            ViewBag.ExchangeProducts = exchangeProducts;

            ViewBag.ExchangeNetPrices = exchangeProducts.ToDictionary(
                p => p.ProductId,
                p =>
                {
                    decimal grossPrice = p.DisplayPrice;
                    decimal discount = StoreHelper.CalculateDiscount(
                        grossPrice,
                        member);

                    return Math.Round(
                        grossPrice - discount,
                        2,
                        MidpointRounding.AwayFromZero);
                });

            ViewBag.EstimatedReturnCredit =
                ReturnRequestHelper.CalculateRefundAmount(
                    item.StoreOrder,
                    item,
                    1);

            ViewBag.IsPremiumMember =
                StoreHelper.CalculateDiscount(100m, member) > 0m;

            return View(item);
        }


        // ============================================================
        // POST: Store/RequestReturn
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RequestReturn(
            int storeOrderItemId,
            int quantity,
            string reason,
            string description,
            string requestedResolution,
            string exchangeType,
            int? exchangeProductId,
            int? exchangeQuantity,
            bool confirmDeclaration = false)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            int currentMemberId = (int)Session["LoggedInMember"];

            var member = db.Members.Find(currentMemberId);

            if (member == null)
                return RedirectToAction("Login", "Forms");

            var item = db.StoreOrderItems
                .Include(i => i.Product.Promotions)
                .Include(i => i.StoreOrder)
                .FirstOrDefault(i =>
                    i.StoreOrderItemId == storeOrderItemId);

            if (item == null ||
                item.StoreOrder == null ||
                item.StoreOrder.MemberId != currentMemberId)
            {
                TempData["Error"] = "Order item not found.";
                return RedirectToAction("MyOrders");
            }

            if (item.StoreOrder.Status != "Collected")
            {
                TempData["Error"] =
                    "Returns can only be requested for collected orders.";

                return RedirectToAction("MyOrders");
            }

            bool alreadyBeingProcessed = db.ReturnRequests.Any(r =>
                r.StoreOrderItemId == storeOrderItemId &&
                ReturnRequestHelper.OpenStatuses.Contains(r.Status));

            if (alreadyBeingProcessed)
            {
                TempData["Error"] =
                    "A return request for this item is already being processed.";

                return RedirectToAction("MyOrders");
            }

            if (!ReturnRequestHelper.IsValidReason(reason))
            {
                TempData["Error"] =
                    "Please select a valid return reason.";

                return RedirectToAction(
                    "RequestReturn",
                    new { id = storeOrderItemId });
            }

            description = (description ?? string.Empty).Trim();

            if (description.Length < 10)
            {
                TempData["Error"] =
                    "Please provide a description of at least 10 characters.";

                return RedirectToAction(
                    "RequestReturn",
                    new { id = storeOrderItemId });
            }

            if (!ReturnRequestHelper.IsValidRequestedResolution(
                requestedResolution))
            {
                TempData["Error"] =
                    "Please select either a refund or an exchange.";

                return RedirectToAction(
                    "RequestReturn",
                    new { id = storeOrderItemId });
            }

            int previouslyReturnedQuantity = db.ReturnRequests
                .Where(r =>
                    r.StoreOrderItemId == storeOrderItemId &&
                    r.Status != "Rejected" &&
                    r.Status != "Cancelled" &&
                    !ReturnRequestHelper.OpenStatuses.Contains(r.Status))
                .Select(r => (int?)r.Quantity)
                .Sum() ?? 0;

            int maximumReturnableQuantity =
                item.Quantity - previouslyReturnedQuantity;

            if (quantity < 1 ||
                quantity > maximumReturnableQuantity)
            {
                TempData["Error"] =
                    $"Quantity must be between 1 and " +
                    $"{maximumReturnableQuantity}.";

                return RedirectToAction(
                    "RequestReturn",
                    new { id = storeOrderItemId });
            }

            if (!confirmDeclaration)
            {
                TempData["Error"] =
                    "You must confirm the return declaration.";

                return RedirectToAction(
                    "RequestReturn",
                    new { id = storeOrderItemId });
            }

            // Amount the member actually paid for the returned
            // quantity after the original order discount.
            decimal returnCredit =
                ReturnRequestHelper.CalculateRefundAmount(
                    item.StoreOrder,
                    item,
                    quantity);

            Product selectedExchangeProduct = null;

            decimal? exchangeUnitPrice = null;
            decimal? exchangeTotal = null;
            decimal? exchangeDifference = null;
            int? requestedExchangeQuantity = null;

            if (requestedResolution == "Exchange")
            {
                if (!ReturnRequestHelper.IsValidExchangeType(
                    exchangeType))
                {
                    TempData["Error"] =
                        "Please select a valid exchange type.";

                    return RedirectToAction(
                        "RequestReturn",
                        new { id = storeOrderItemId });
                }

                // ========================================================
                // SAME-PRODUCT EXCHANGE
                // ========================================================
                if (exchangeType == "SameProduct")
                {
                    exchangeProductId = item.ProductId;

                    // Like-for-like exchange must use the same quantity.
                    exchangeQuantity = quantity;

                    selectedExchangeProduct = item.Product;

                    if (selectedExchangeProduct == null ||
                        !selectedExchangeProduct.IsActive)
                    {
                        TempData["Error"] =
                            "The original product is no longer available.";

                        return RedirectToAction(
                            "RequestReturn",
                            new { id = storeOrderItemId });
                    }

                    if (selectedExchangeProduct.QuantityOnHand < quantity)
                    {
                        TempData["Error"] =
                            $"Only {selectedExchangeProduct.QuantityOnHand} " +
                            $"{selectedExchangeProduct.Name} unit(s) " +
                            $"are currently available.";

                        return RedirectToAction(
                            "RequestReturn",
                            new { id = storeOrderItemId });
                    }

                    requestedExchangeQuantity = quantity;

                    // A same-product, same-quantity replacement carries
                    // forward the value originally paid.
                    exchangeTotal = returnCredit;

                    exchangeUnitPrice = Math.Round(
                        returnCredit / quantity,
                        2,
                        MidpointRounding.AwayFromZero);

                    // No additional payment and no partial refund.
                    exchangeDifference = 0m;
                }
                // ========================================================
                // DIFFERENT-PRODUCT EXCHANGE
                // ========================================================
                else
                {
                    if (!exchangeProductId.HasValue)
                    {
                        TempData["Error"] =
                            "Please select a replacement product.";

                        return RedirectToAction(
                            "RequestReturn",
                            new { id = storeOrderItemId });
                    }

                    if (!exchangeQuantity.HasValue ||
                        exchangeQuantity.Value < 1)
                    {
                        TempData["Error"] =
                            "Please enter a valid replacement quantity.";

                        return RedirectToAction(
                            "RequestReturn",
                            new { id = storeOrderItemId });
                    }

                    selectedExchangeProduct = db.Products
                        .Include(p => p.Promotions)
                        .FirstOrDefault(p =>
                            p.ProductId == exchangeProductId.Value &&
                            p.IsActive);

                    if (selectedExchangeProduct == null)
                    {
                        TempData["Error"] =
                            "The selected replacement product is unavailable.";

                        return RedirectToAction(
                            "RequestReturn",
                            new { id = storeOrderItemId });
                    }

                    if (selectedExchangeProduct.QuantityOnHand <
                        exchangeQuantity.Value)
                    {
                        TempData["Error"] =
                            $"Only " +
                            $"{selectedExchangeProduct.QuantityOnHand} " +
                            $"{selectedExchangeProduct.Name} unit(s) " +
                            $"are currently available.";

                        return RedirectToAction(
                            "RequestReturn",
                            new { id = storeOrderItemId });
                    }

                    requestedExchangeQuantity =
                        exchangeQuantity.Value;

                    decimal grossReplacementTotal =
                        selectedExchangeProduct.DisplayPrice *
                        requestedExchangeQuantity.Value;

                    // Apply the member's current applicable discount to
                    // the replacement transaction.
                    decimal replacementDiscount =
                        StoreHelper.CalculateDiscount(
                            grossReplacementTotal,
                            member);

                    decimal netReplacementTotal =
                        grossReplacementTotal -
                        replacementDiscount;

                    netReplacementTotal = Math.Round(
                        netReplacementTotal,
                        2,
                        MidpointRounding.AwayFromZero);

                    exchangeTotal = netReplacementTotal;

                    exchangeUnitPrice = Math.Round(
                        netReplacementTotal /
                        requestedExchangeQuantity.Value,
                        2,
                        MidpointRounding.AwayFromZero);

                    exchangeDifference =
                        ReturnRequestHelper
                            .CalculateExchangePriceDifference(
                                returnCredit,
                                netReplacementTotal);
                }
            }
            else
            {
                exchangeType = null;
                exchangeProductId = null;
                exchangeQuantity = null;
            }

            var request = new ReturnRequest
            {
                ReferenceNumber =
                    ReturnRequestHelper.GenerateReferenceNumber(db),

                StoreOrderItemId = storeOrderItemId,
                MemberId = currentMemberId,

                Quantity = quantity,
                Reason = reason,
                Description = description,

                RequestedResolution =
                    requestedResolution,

                ExchangeType =
                    requestedResolution == "Exchange"
                        ? exchangeType
                        : null,

                ExchangeProductId =
                    requestedResolution == "Exchange"
                        ? exchangeProductId
                        : null,

                ExchangeQuantity =
                    requestedResolution == "Exchange"
                        ? requestedExchangeQuantity
                        : null,

                ReturnCreditAmount = returnCredit,
                ExchangeUnitPrice = exchangeUnitPrice,
                ExchangeTotalAmount = exchangeTotal,
                ExchangePriceDifference = exchangeDifference,

                AdditionalPaymentAmount =
                    exchangeDifference.HasValue
                        ? ReturnRequestHelper.CalculateAdditionalPayment(
                            exchangeDifference.Value)
                        : (decimal?)null,

                ExchangeRefundAmount =
                    exchangeDifference.HasValue
                        ? ReturnRequestHelper.CalculateExchangeRefund(
                            exchangeDifference.Value)
                        : (decimal?)null,

                Status = "Pending",
                RequestedAt = DateTime.Now
            };

            // Save the formal request and its secure QR atomically.
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Save first so EF/SQL generates ReturnRequestId.
                    db.ReturnRequests.Add(request);
                    db.SaveChanges();

                    // Only the SHA-256 hash is stored. The raw token
                    // exists only inside the generated QR image.
                    string rawQrToken =
                        ReturnRequestHelper.GenerateSecureQrToken();

                    string qrContent =
                        ReturnRequestHelper.GenerateReturnQrContent(
                            item.StoreOrder.ReferenceNumber,
                            request.ReturnRequestId,
                            rawQrToken);

                    request.ReturnQrTokenHash =
                        ReturnRequestHelper.HashQrToken(rawQrToken);

                    request.ReturnQrBase64 =
                        ReturnRequestHelper.GenerateReturnQrBase64(
                            qrContent);

                    request.ReturnQrGeneratedAt = DateTime.Now;

                    db.Entry(request).State = EntityState.Modified;
                    db.SaveChanges();

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();

                    System.Diagnostics.Debug.WriteLine(
                        $"[RETURN REQUEST ERROR] {ex}");

                    TempData["Error"] =
                        "The return or exchange request could not be submitted. No data was saved.";

                    return RedirectToAction(
                        "RequestReturn",
                        new { id = storeOrderItemId });
                }
            }

            try
            {
                string resolutionText =
                    requestedResolution == "Exchange"
                        ? "an exchange"
                        : "a refund";

                new NotificationService(db).NotifyAdmin(
                    "New Return Request",
                    $"Return {request.ReferenceNumber} was submitted " +
                    $"for {resolutionText}. Staff inspection is required.",
                    "Info",
                    "/StoreAdmin/Returns");

                new NotificationService(db).NotifyAllStoreStaff(
                    "New Return Request",
                    $"Return {request.ReferenceNumber} was submitted " +
                    $"for {resolutionText}. The item requires inspection.",
                    "Info",
                    "/StoreAdmin/Returns");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[RETURN NOTIFICATION ERROR] {ex.Message}");
            }

            try
            {
                ProfessionalStoreEmailService.SendReturnRequestReceived(
                    member.Email,
                    member.FullName,
                    item.StoreOrder.ReferenceNumber,
                    request.ReferenceNumber,
                    item.Product != null
                        ? item.Product.Name
                        : "Store product",
                    request.Quantity,
                    request.RequestedResolution,
                   request.ReturnCreditAmount ?? returnCredit,
                    request.ReturnQrBase64);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[RETURN EMAIL ERROR] {ex}");
            }

            TempData["Success"] =
                $"Return request submitted successfully. " +
                $"Reference: {request.ReferenceNumber}.";

            return RedirectToAction("MyOrders");
        }

        // POST: Store/Reorder/5 — re-adds every item from a past order to
        // the cart in one click. Silently skips items now inactive/out of
        // stock rather than failing the whole action, and tells the member
        // exactly what got skipped.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reorder(int orderId)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            int currentMemberId = (int)Session["LoggedInMember"];

            var order = db.StoreOrders
                .Include(o => o.Items.Select(i => i.Product))
                .FirstOrDefault(o => o.StoreOrderId == orderId && o.MemberId == currentMemberId);

            if (order == null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToAction("MyOrders");
            }

            int addedCount = 0;
            var skippedItems = new System.Collections.Generic.List<string>();

            foreach (var item in order.Items)
            {
                if (item.Product == null || !item.Product.IsActive || item.Product.QuantityOnHand < 1)
                {
                    skippedItems.Add(item.Product?.Name ?? "an item");
                    continue;
                }

                var existing = db.ShoppingCartItems
                    .FirstOrDefault(c => c.MemberId == currentMemberId && c.ProductId == item.ProductId);

                int quantityToAdd = Math.Min(item.Quantity, item.Product.QuantityOnHand);

                if (existing != null)
                {
                    existing.Quantity += quantityToAdd;
                    db.Entry(existing).State = EntityState.Modified;
                }
                else
                {
                    db.ShoppingCartItems.Add(new ShoppingCartItem
                    {
                        MemberId = currentMemberId,
                        ProductId = item.ProductId,
                        Quantity = quantityToAdd,
                        AddedAt = DateTime.Now
                    });
                }

                addedCount++;
            }

            db.SaveChanges();

            if (addedCount == 0)
            {
                TempData["Error"] = "None of the items from this order are currently available.";
            }
            else if (skippedItems.Any())
            {
                TempData["Success"] = $"{addedCount} item(s) added to your cart. Unavailable: {string.Join(", ", skippedItems)}.";
            }
            else
            {
                TempData["Success"] = $"{addedCount} item(s) added to your cart.";
            }

            return RedirectToAction("Cart");
        }
        // ============================================================
        // POST: Store/InitiateExchangePayment
        //
        // Starts Paystack payment when an approved replacement costs
        // more than the member's return credit.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> InitiateExchangePayment(
            int returnRequestId)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");
            ReturnRequestHelper.ReleaseExpiredExchangeReservations(db);
            int currentMemberId =
                (int)Session["LoggedInMember"];

            var member = db.Members.Find(currentMemberId);

            if (member == null)
                return RedirectToAction("Login", "Forms");

            var request = db.ReturnRequests
                .Include(r => r.ExchangeProduct)
                .Include(r => r.StoreOrderItem.Product)
                .FirstOrDefault(r =>
                    r.ReturnRequestId == returnRequestId &&
                    r.MemberId == currentMemberId);

            if (request == null)
            {
                TempData["Error"] =
                    "Exchange request not found.";

                return RedirectToAction("MyOrders");
            }

            if (request.ApprovedResolution != "Exchange")
            {
                TempData["Error"] =
                    "This request was not approved as an exchange.";

                return RedirectToAction("MyOrders");
            }

            if (request.Status == "ReadyForExchange" ||
                request.Status == "ExchangeCompleted")
            {
                TempData["Success"] =
                    request.Status == "ExchangeCompleted"
                        ? "This exchange has already been completed."
                        : "The exchange payment has already been completed.";

                return RedirectToAction("MyOrders");
            }

            if (request.Status != "AwaitingExchangePayment")
            {
                TempData["Error"] =
                    "This exchange is not awaiting payment.";

                return RedirectToAction("MyOrders");
            }

            decimal amountDue =
                request.AdditionalPaymentAmount ?? 0m;

            if (amountDue <= 0)
            {
                // Defensive correction for a request that no longer has
                // an additional amount due.
                request.AdditionalPaymentAmount = 0m;
                request.Status = "ReadyForExchange";

                db.Entry(request).State =
                    EntityState.Modified;

                db.SaveChanges();

                TempData["Success"] =
                    "No additional payment is required. " +
                    "Your replacement is ready for exchange.";

                return RedirectToAction("MyOrders");
            }

            if (request.ExchangeReservationExpiresAt.HasValue &&
                DateTime.Now >
                request.ExchangeReservationExpiresAt.Value)
            {
                TempData["Error"] =
                    "The exchange reservation has expired. " +
                    "Please contact GymHub staff.";

                return RedirectToAction("MyOrders");
            }

            string secretKey =
                WebConfigurationManager.AppSettings[
                    "Paystack:SecretKey"];

            if (string.IsNullOrWhiteSpace(secretKey))
            {
                TempData["Error"] =
                    "Paystack is not configured.";

                return RedirectToAction("MyOrders");
            }

            string paymentReference =
                "EXCH-" +
                request.ReturnRequestId +
                "-" +
                Guid.NewGuid()
                    .ToString("N")
                    .Substring(0, 12);

            string callbackUrl =
                Url.Action(
                    "ExchangePaymentCallback",
                    "Store",
                    null,
                    Request.Url.Scheme);

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
                        ["email"] = member.Email,

                        ["amount"] =
                            (int)Math.Round(
                                amountDue * 100,
                                0,
                                MidpointRounding.AwayFromZero),

                        ["reference"] = paymentReference,

                        ["callback_url"] = callbackUrl,

                        ["metadata"] = new JObject
                        {
                            ["payment_type"] =
                                "exchange_difference",

                            ["return_request_id"] =
                                request.ReturnRequestId,

                            ["member_id"] =
                                currentMemberId
                        }
                    };

                    var content =
                        new StringContent(
                            payload.ToString(),
                            System.Text.Encoding.UTF8,
                            "application/json");

                    var response = await client.PostAsync(
                        "https://api.paystack.co/transaction/initialize",
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

                        return RedirectToAction("MyOrders");
                    }

                    bool initialized =
                        response.IsSuccessStatusCode &&
                        json["status"]?.Value<bool>() == true;

                    if (!initialized)
                    {
                        TempData["Error"] =
                            "Could not start exchange payment: " +
                            (json["message"]?.ToString()
                             ?? "Unknown Paystack error.");

                        return RedirectToAction("MyOrders");
                    }

                    string authorizationUrl =
                        json["data"]?["authorization_url"]
                            ?.ToString();

                    string returnedReference =
                        json["data"]?["reference"]
                            ?.ToString();

                    if (string.IsNullOrWhiteSpace(
                        authorizationUrl))
                    {
                        TempData["Error"] =
                            "Paystack did not provide a payment URL.";

                        return RedirectToAction("MyOrders");
                    }

                    request.ExchangePaymentReference =
                        string.IsNullOrWhiteSpace(returnedReference)
                            ? paymentReference
                            : returnedReference;

                    db.Entry(request).State =
                        EntityState.Modified;

                    db.SaveChanges();

                    return Redirect(authorizationUrl);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[EXCHANGE PAYMENT ERROR] {ex}");

                TempData["Error"] =
                    "Could not start the exchange payment. " +
                    "Please try again.";

                return RedirectToAction("MyOrders");
            }
        }


        // ============================================================
        // GET: Store/ExchangePaymentCallback
        //
        // Paystack redirects here after the member pays the exchange
        // price difference.
        // ============================================================
        public async Task<ActionResult> ExchangePaymentCallback(
            string reference)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            int currentMemberId =
                (int)Session["LoggedInMember"];

            if (string.IsNullOrWhiteSpace(reference))
            {
                TempData["Error"] =
                    "The exchange payment reference is missing.";

                return RedirectToAction("MyOrders");
            }

            var request = db.ReturnRequests
                .Include(r => r.Member)
                .Include(r => r.ExchangeProduct)
                .FirstOrDefault(r =>
                    r.ExchangePaymentReference == reference &&
                    r.MemberId == currentMemberId);

            if (request == null)
            {
                TempData["Error"] =
                    "The exchange payment could not be matched " +
                    "to a return request.";

                return RedirectToAction("MyOrders");
            }

            // Callback refresh protection.
            if (request.Status == "ReadyForExchange" &&
                request.ExchangePaymentCompletedAt.HasValue)
            {
                TempData["Success"] =
                    "Your exchange payment was already verified. " +
                    "Your replacement is ready.";

                return RedirectToAction("MyOrders");
            }

            if (request.Status == "ExchangeCompleted")
            {
                TempData["Success"] =
                    "This exchange has already been completed.";

                return RedirectToAction("MyOrders");
            }

            if (request.Status != "AwaitingExchangePayment")
            {
                TempData["Error"] =
                    "This exchange is not awaiting payment.";

                return RedirectToAction("MyOrders");
            }

            decimal expectedAmount =
                request.AdditionalPaymentAmount ?? 0m;

            int expectedAmountInCents =
                (int)Math.Round(
                    expectedAmount * 100,
                    0,
                    MidpointRounding.AwayFromZero);

            string secretKey =
                WebConfigurationManager.AppSettings[
                    "Paystack:SecretKey"];

            if (string.IsNullOrWhiteSpace(secretKey))
            {
                TempData["Error"] =
                    "Paystack is not configured.";

                return RedirectToAction("MyOrders");
            }

            JObject verificationJson;

            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers
                            .AuthenticationHeaderValue(
                                "Bearer",
                                secretKey);

                    var response = await client.GetAsync(
                        "https://api.paystack.co/transaction/verify/" +
                        Uri.EscapeDataString(reference));

                    string responseBody =
                        await response.Content.ReadAsStringAsync();

                    try
                    {
                        verificationJson =
                            JObject.Parse(responseBody);
                    }
                    catch
                    {
                        TempData["Error"] =
                            "Paystack returned an invalid " +
                            "verification response.";

                        return RedirectToAction("MyOrders");
                    }

                    bool verified =
                        response.IsSuccessStatusCode &&
                        verificationJson["status"]
                            ?.Value<bool>() == true &&
                        verificationJson["data"]?["status"]
                            ?.ToString() == "success";

                    if (!verified)
                    {
                        TempData["Error"] =
                            "The exchange payment could not be verified.";

                        return RedirectToAction("MyOrders");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[EXCHANGE VERIFY ERROR] {ex}");

                TempData["Error"] =
                    "Could not verify the exchange payment. " +
                    "Please try again.";

                return RedirectToAction("MyOrders");
            }

            int amountPaid =
                verificationJson["data"]?["amount"]
                    ?.Value<int>() ?? 0;

            string verifiedReference =
                verificationJson["data"]?["reference"]
                    ?.ToString();

            JObject metadata =
                verificationJson["data"]?["metadata"]
                    as JObject;

            int metadataReturnRequestId =
                metadata?["return_request_id"]
                    ?.Value<int>() ?? 0;

            int metadataMemberId =
                metadata?["member_id"]
                    ?.Value<int>() ?? 0;

            string paymentType =
                metadata?["payment_type"]
                    ?.ToString();

            if (!string.Equals(
                verifiedReference,
                reference,
                StringComparison.Ordinal))
            {
                TempData["Error"] =
                    "The verified payment reference does not match.";

                return RedirectToAction("MyOrders");
            }

            if (amountPaid != expectedAmountInCents)
            {
                TempData["Error"] =
                    "The exchange payment amount does not match " +
                    "the amount required.";

                return RedirectToAction("MyOrders");
            }

            if (metadataReturnRequestId !=
                request.ReturnRequestId ||
                metadataMemberId != currentMemberId ||
                paymentType != "exchange_difference")
            {
                TempData["Error"] =
                    "The exchange payment details could not be validated.";

                return RedirectToAction("MyOrders");
            }

            request.ExchangePaymentReference = reference;
            request.ExchangePaymentCompletedAt = DateTime.Now;
            request.Status = "ReadyForExchange";

            db.Entry(request).State =
                EntityState.Modified;

            db.SaveChanges();

            try
            {
                new NotificationService(db).Notify(
                    "Member",
                    request.MemberId,
                    "Exchange Payment Confirmed",
                    $"Your payment of R{expectedAmount:F2} for " +
                    $"exchange {request.ReferenceNumber} was confirmed. " +
                    $"Your replacement is ready at GymHub.",
                    "Success",
                    "/Store/MyOrders");

                new NotificationService(db).NotifyAdmin(
                    "Exchange Ready for Handover",
                    $"Exchange {request.ReferenceNumber} has been paid " +
                    $"and is ready for in-store handover.",
                    "Success",
                    "/StoreAdmin/Returns");

                new NotificationService(db).NotifyAllStoreStaff(
                    "Exchange Ready for Handover",
                    $"Exchange {request.ReferenceNumber} has been paid " +
                    $"and is ready for in-store handover.",
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
                ProfessionalStoreEmailService.SendExchangeReady(
                    request.Member.Email,
                    request.Member.FullName,
                    request.ReferenceNumber,
                    request.ExchangeProduct != null
                        ? request.ExchangeProduct.Name
                        : "Replacement product",
                    request.ExchangeQuantity ?? request.Quantity);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[EXCHANGE READY EMAIL ERROR] {ex}");
            }

            TempData["Success"] =
                $"Payment confirmed. Your replacement " +
                $"{request.ExchangeProduct?.Name} is ready for exchange.";

            return RedirectToAction("MyOrders");
        }
        // ============================================================
        // DISPLAY RETURN QR TO ITS OWNER
        // GET: Store/ReturnQr/5
        // ============================================================

        [HttpGet]
        public ActionResult ReturnQr(int id)
        {
            if (Session["LoggedInMember"] == null)
                return RedirectToAction("Login", "Forms");

            int currentMemberId =
                (int)Session["LoggedInMember"];

            var request = db.ReturnRequests
                .Include(r => r.Member)
                .Include(r => r.StoreOrderItem.Product)
                .Include(r => r.StoreOrderItem.StoreOrder)
                .FirstOrDefault(r =>
                    r.ReturnRequestId == id &&
                    r.MemberId == currentMemberId);

            if (request == null)
            {
                TempData["Error"] =
                    "The return request could not be found.";

                return RedirectToAction("MyOrders");
            }

            if (string.IsNullOrWhiteSpace(request.ReturnQrBase64) ||
                string.IsNullOrWhiteSpace(request.ReturnQrTokenHash))
            {
                TempData["Error"] =
                    "A QR code is not available for this return request.";

                return RedirectToAction("MyOrders");
            }

            return View(request);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
