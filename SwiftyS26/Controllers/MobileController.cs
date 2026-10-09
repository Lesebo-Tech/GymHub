using System;
using System.Data.Entity;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Http;
using SwiftyS26.Models;
using SwiftyS26.Services;

namespace SwiftyS26.Controllers
{
    [RoutePrefix("api/mobile")]
    public class MobileController : ApiController
    {
        private GymHubDB db = new GymHubDB();

        // ==========================================================
        // TEST API CONNECTION
        // GET: api/mobile/ping
        // ==========================================================
        [HttpGet]
        [Route("ping")]
        public IHttpActionResult Ping()
        {
            return Ok(new
            {
                success = true,
                message = "GymHub Mobile API is working"
            });
        }


        // ==========================================================
        // STORE STAFF LOGIN
        // POST: api/mobile/login
        // ==========================================================
        [HttpPost]
        [Route("login")]
        public IHttpActionResult Login(MobileStaffLoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return Ok(new
                {
                    success = false,
                    message = "Email and password are required."
                });
            }

            string email = request.Email.Trim().ToLower();

            var staff = db.StoreStaffs
                .FirstOrDefault(s => s.Email.ToLower() == email);

            if (staff == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "Invalid email or password."
                });
            }

            if (!staff.IsActive)
            {
                return Ok(new
                {
                    success = false,
                    message = "This account is inactive."
                });
            }

            string hashedPassword = HashPassword(request.Password);

            if (staff.PasswordHash != hashedPassword)
            {
                return Ok(new
                {
                    success = false,
                    message = "Invalid email or password."
                });
            }

            return Ok(new
            {
                success = true,
                message = "Login successful.",
                storeStaffId = staff.StoreStaffId,
                fullName = staff.FullName,
                email = staff.Email,
                role = staff.Role,
                mustChangePassword = staff.MustChangePassword
            });
        }


        // ==========================================================
        // MOBILE STORE STAFF DASHBOARD
        // GET: api/mobile/dashboard/{storeStaffId}
        // ==========================================================
        [HttpGet]
        [Route("dashboard/{storeStaffId:int}")]
        public IHttpActionResult Dashboard(int storeStaffId)
        {
            // ------------------------------------------------------
            // VERIFY STORE STAFF
            // ------------------------------------------------------
            var staff = db.StoreStaffs
                .FirstOrDefault(s =>
                    s.StoreStaffId == storeStaffId &&
                    s.IsActive);

            if (staff == null)
            {
                return Ok(new MobileDashboardResponse
                {
                    Success = false,
                    Message = "Invalid or inactive Store Staff account."
                });
            }

            DateTime now = DateTime.Now;
            DateTime approachingDeadline = now.AddHours(24);

            // ------------------------------------------------------
            // DASHBOARD STATISTICS
            // ------------------------------------------------------
            int preparingCount =
                db.StoreOrders.Count(o =>
                    o.Status == "Paid" ||
                    o.Status == "Preparing");

            int readyForPickupCount =
                db.StoreOrders.Count(o =>
                    o.Status == "ReadyForPickup" &&
                    o.PickupDeadline.HasValue &&
                    o.PickupDeadline.Value >= now);

            int deadlineApproachingCount =
                db.StoreOrders.Count(o =>
                    o.Status == "ReadyForPickup" &&
                    o.PickupDeadline.HasValue &&
                    o.PickupDeadline.Value >= now &&
                    o.PickupDeadline.Value <= approachingDeadline);

            int uncollectedReviewCount =
                db.StoreOrders.Count(o =>
                    o.Status == "UncollectedPendingReview" ||
                    o.UncollectedResolutionStatus == "PendingReview");

            // ------------------------------------------------------
            // ORDERS REQUIRING ATTENTION
            // ------------------------------------------------------
            var attentionOrders =
                db.StoreOrders
                    .Include(o => o.Member)
                    .Where(o =>
                        (
                            o.Status == "ReadyForPickup" &&
                            o.PickupDeadline.HasValue &&
                            o.PickupDeadline.Value <= approachingDeadline
                        )
                        ||
                        o.Status == "UncollectedPendingReview"
                        ||
                        o.UncollectedResolutionStatus == "PendingReview")
                    .OrderBy(o => o.PickupDeadline)
                    .Take(3)
                    .ToList()
                    .Select(o =>
                        new MobileDashboardAttentionDto
                        {
                            OrderId = o.StoreOrderId,

                            ReferenceNumber =
                                o.ReferenceNumber,

                            MemberName =
                                o.Member != null
                                    ? o.Member.FullName
                                    : "Unknown Member",

                            Status =
                                o.Status,

                            PickupDeadline =
                                o.PickupDeadline,

                            AttentionType =
                                o.Status == "UncollectedPendingReview" ||
                                o.UncollectedResolutionStatus == "PendingReview"
                                    ? "UncollectedReview"
                                    : o.PickupDeadline.HasValue &&
                                      o.PickupDeadline.Value < now
                                        ? "Overdue"
                                        : "DeadlineApproaching"
                        })
                    .ToList();

            // ------------------------------------------------------
            // RECENT STORE ACTIVITY
            // ------------------------------------------------------
            var recentActivity =
                db.StoreOrderCollectionEvents
                    .Include(e => e.StoreOrder)
                    .OrderByDescending(e => e.CreatedAt)
                    .Take(3)
                    .ToList()
                    .Select(e =>
                        new MobileDashboardActivityDto
                        {
                            EventId =
                                e.StoreOrderCollectionEventId,

                            OrderId =
                                e.StoreOrderId,

                            ReferenceNumber =
                                e.StoreOrder != null
                                    ? e.StoreOrder.ReferenceNumber
                                    : "",

                            EventType =
                                e.EventType,

                            Description =
                                e.Description,

                            PerformedBy =
                                e.PerformedBy,

                            CreatedAt =
                                e.CreatedAt
                        })
                    .ToList();

            // ------------------------------------------------------
            // RETURN DASHBOARD
            // ------------------------------------------------------
            return Ok(new MobileDashboardResponse
            {
                Success = true,
                Message = "Dashboard loaded successfully.",

                Staff =
                    new MobileDashboardStaffDto
                    {
                        StoreStaffId =
                            staff.StoreStaffId,

                        FullName =
                            staff.FullName,

                        Role =
                            staff.Role
                    },

                Statistics =
                    new MobileDashboardStatsDto
                    {
                        Preparing =
                            preparingCount,

                        ReadyForPickup =
                            readyForPickupCount,

                        DeadlineApproaching =
                            deadlineApproachingCount,

                        UncollectedReview =
                            uncollectedReviewCount
                    },

                AttentionOrders =
                    attentionOrders,

                RecentActivity =
                    recentActivity
            });
        }


        // ==========================================================
        // VERIFY PREPARATION QR AND RETURN ORDER DETAILS
        // POST: api/mobile/verify-ready
        // ==========================================================
        [HttpPost]
        [Route("verify-ready")]
        public IHttpActionResult VerifyReady(MobileOrderQrRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.QrData) ||
                request.StoreStaffId <= 0)
            {
                return Ok(new MobileReadyOrderResponse
                {
                    Success = false,
                    Message = "QR data and Store Staff ID are required."
                });
            }

            var staff = db.StoreStaffs
                .FirstOrDefault(s =>
                    s.StoreStaffId == request.StoreStaffId &&
                    s.IsActive);

            if (staff == null)
            {
                return Ok(new MobileReadyOrderResponse
                {
                    Success = false,
                    Message = "Invalid or inactive Store Staff account."
                });
            }

            string qrData = request.QrData.Trim();

            if (!StoreOrderHelper.IsReadyQrContent(qrData))
            {
                return Ok(new MobileReadyOrderResponse
                {
                    Success = false,
                    Message = "This is not a valid GymHub preparation QR code."
                });
            }

            string referenceNumber =
                StoreOrderHelper.ExtractQrField(
                    qrData,
                    "Ref:"
                );

            string token =
                StoreOrderHelper.ExtractQrField(
                    qrData,
                    "Token:"
                );

            if (string.IsNullOrWhiteSpace(referenceNumber) ||
                string.IsNullOrWhiteSpace(token))
            {
                return Ok(new MobileReadyOrderResponse
                {
                    Success = false,
                    Message = "The preparation QR code is incomplete."
                });
            }

            var order = db.StoreOrders
                .Include(o => o.Member)
                .Include(o => o.Items.Select(i => i.Product))
                .FirstOrDefault(o =>
                    o.ReferenceNumber == referenceNumber);

            if (order == null)
            {
                return Ok(new MobileReadyOrderResponse
                {
                    Success = false,
                    Message = "Order not found."
                });
            }

            if (order.Status == "ReadyForPickup")
            {
                return Ok(new MobileReadyOrderResponse
                {
                    Success = false,
                    Message = "This order has already been marked ready for pickup."
                });
            }

            if (order.Status == "Collected")
            {
                return Ok(new MobileReadyOrderResponse
                {
                    Success = false,
                    Message = "This order has already been collected."
                });
            }

            if (order.Status != "Paid" &&
                order.Status != "Preparing")
            {
                return Ok(new MobileReadyOrderResponse
                {
                    Success = false,
                    Message =
                        $"This order cannot be prepared from its current status: {order.Status}."
                });
            }

            if (order.Member == null)
            {
                return Ok(new MobileReadyOrderResponse
                {
                    Success = false,
                    Message = "The member linked to this order could not be found."
                });
            }

            if (order.ReadyQrUsedAt.HasValue ||
                string.IsNullOrWhiteSpace(order.ReadyQrToken))
            {
                return Ok(new MobileReadyOrderResponse
                {
                    Success = false,
                    Message = "This preparation QR code is no longer active."
                });
            }

            bool tokenMatches =
                string.Equals(
                    order.ReadyQrToken,
                    token,
                    StringComparison.Ordinal
                );

            if (!tokenMatches)
            {
                return Ok(new MobileReadyOrderResponse
                {
                    Success = false,
                    Message = "The preparation QR token could not be verified."
                });
            }

            if (!order.IsPickupStockReserved)
            {
                return Ok(new MobileReadyOrderResponse
                {
                    Success = false,
                    Message = "This order does not have an active stock reservation."
                });
            }

            var items =
                order.Items
                    .Select(i =>
                        new MobileReadyOrderItemDto
                        {
                            StoreOrderItemId =
                                i.StoreOrderItemId,

                            ProductId =
                                i.ProductId,

                            ProductName =
                                i.Product != null
                                    ? i.Product.Name
                                    : "Unknown Product",

                            Category =
                                i.Product != null
                                    ? i.Product.Category
                                    : "",

                            ImagePath =
                                i.Product != null
                                    ? i.Product.ImagePath
                                    : null,

                            Quantity =
                                i.Quantity,

                            UnitPrice =
                                i.UnitPriceAtPurchase,

                            LineTotal =
                                i.Quantity *
                                i.UnitPriceAtPurchase
                        })
                    .ToList();

            return Ok(new MobileReadyOrderResponse
            {
                Success = true,
                Message = "Order verified successfully.",

                OrderId =
                    order.StoreOrderId,

                ReferenceNumber =
                    order.ReferenceNumber,

                Status =
                    order.Status,

                CreatedAt =
                    order.CreatedAt,

                PaidAt =
                    order.PaidAt,

                Subtotal =
                    order.Subtotal,

                DiscountAmount =
                    order.DiscountAmount,

                FinalTotal =
                    order.FinalTotal,

                Member =
                    new MobileReadyMemberDto
                    {
                        MemberId =
                            order.Member.MemberId,

                        MemberNumber =
                            order.Member.MemberNumber,

                        FullName =
                            order.Member.FullName,

                        Email =
                            order.Member.Email,

                        Phone =
                            order.Member.Phone,

                        Plan =
                            order.Member.Plan
                    },

                Items =
                    items
            });
        }


        // ==========================================================
        // MARK ORDER READY FROM PREPARATION QR
        // POST: api/mobile/mark-ready
        // ==========================================================
        [HttpPost]
        [Route("mark-ready")]
        public IHttpActionResult MarkReady(MobileOrderQrRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.QrData) ||
                request.StoreStaffId <= 0)
            {
                return Ok(new
                {
                    success = false,
                    message = "QR data and Store Staff ID are required."
                });
            }

            // ------------------------------------------------------
            // VERIFY STORE STAFF
            // ------------------------------------------------------
            var staff =
                db.StoreStaffs
                    .FirstOrDefault(s =>
                        s.StoreStaffId ==
                            request.StoreStaffId &&
                        s.IsActive);

            if (staff == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "Invalid or inactive Store Staff account."
                });
            }

            string qrData =
                request.QrData.Trim();

            // ------------------------------------------------------
            // VERIFY PREPARATION QR FORMAT
            // ------------------------------------------------------
            if (!StoreOrderHelper.IsReadyQrContent(qrData))
            {
                return Ok(new
                {
                    success = false,
                    message = "This is not a GymHub order preparation QR code."
                });
            }

            string referenceNumber =
                StoreOrderHelper.ExtractQrField(
                    qrData,
                    "Ref:"
                );

            string token =
                StoreOrderHelper.ExtractQrField(
                    qrData,
                    "Token:"
                );

            if (string.IsNullOrWhiteSpace(referenceNumber) ||
                string.IsNullOrWhiteSpace(token))
            {
                return Ok(new
                {
                    success = false,
                    message = "The QR code is incomplete or has an invalid format."
                });
            }

            // ------------------------------------------------------
            // FIND ORDER
            // ------------------------------------------------------
            var order =
                db.StoreOrders
                    .Include(o => o.Member)
                    .Include(o =>
                        o.Items.Select(i => i.Product))
                    .FirstOrDefault(o =>
                        o.ReferenceNumber ==
                            referenceNumber);

            if (order == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "No order matches this QR code."
                });
            }

            // ------------------------------------------------------
            // CHECK STATUS
            // ------------------------------------------------------
            if (order.Status == "ReadyForPickup")
            {
                return Ok(new
                {
                    success = true,
                    alreadyReady = true,
                    orderId = order.StoreOrderId,
                    referenceNumber = order.ReferenceNumber,

                    memberName =
                        order.Member != null
                            ? order.Member.FullName
                            : "Unknown member",

                    message = "This order has already been marked ready for pickup."
                });
            }

            if (order.Status == "Collected")
            {
                return Ok(new
                {
                    success = false,
                    message = "This order has already been collected."
                });
            }

            if (order.Status != "Paid" &&
                order.Status != "Preparing")
            {
                return Ok(new
                {
                    success = false,
                    message = "This order cannot be marked ready from its current status."
                });
            }

            if (order.Member == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "The member linked to this order could not be found."
                });
            }

            // ------------------------------------------------------
            // VERIFY PREPARATION QR
            // ------------------------------------------------------
            if (order.ReadyQrUsedAt.HasValue ||
                string.IsNullOrWhiteSpace(
                    order.ReadyQrToken))
            {
                return Ok(new
                {
                    success = false,
                    message = "This preparation QR code is no longer active."
                });
            }

            bool tokenMatches =
                string.Equals(
                    order.ReadyQrToken,
                    token,
                    StringComparison.Ordinal
                );

            if (!tokenMatches)
            {
                return Ok(new
                {
                    success = false,
                    message = "The preparation QR token could not be verified."
                });
            }

            // ------------------------------------------------------
            // VERIFY STOCK RESERVATION
            // ------------------------------------------------------
            if (!order.IsPickupStockReserved)
            {
                return Ok(new
                {
                    success = false,
                    message = "This order does not have an active stock reservation."
                });
            }

            DateTime now =
                DateTime.Now;

            // ------------------------------------------------------
            // MARK ORDER READY
            // ------------------------------------------------------
            order.Status =
                "ReadyForPickup";

            order.ReadyAt =
                now;

            order.PickupDeadline =
                now.AddDays(5);

            order.FirstReminderSentAt =
                null;

            order.FinalReminderSentAt =
                null;

            order.IsPickupStockReserved =
                true;

            order.UncollectedAt =
                null;

            order.UncollectedResolutionStatus =
                null;

            order.UncollectedResolutionNotes =
                null;

            order.UncollectedResolvedBy =
                null;

            order.UncollectedResolvedAt =
                null;

            // ------------------------------------------------------
            // INVALIDATE PREPARATION QR
            // ------------------------------------------------------
            StoreOrderHelper.InvalidateReadyQr(
                order
            );

            // ------------------------------------------------------
            // GENERATE MEMBER COLLECTION QR
            // ------------------------------------------------------
            string collectionQrContent =
                StoreOrderHelper.GenerateOrderQrContent(
                    order.ReferenceNumber,
                    order.Member.MemberNumber
                );

            order.OrderQrBase64 =
                StoreOrderHelper.GenerateOrderQrBase64(
                    collectionQrContent
                );

            // ------------------------------------------------------
            // LOG COLLECTION EVENT
            // ------------------------------------------------------
            db.StoreOrderCollectionEvents.Add(
                new StoreOrderCollectionEvent
                {
                    StoreOrderId =
                        order.StoreOrderId,

                    EventType =
                        "ReadyForPickup",

                    Description =
                        "The internal preparation QR was verified using the GymHub " +
                        "mobile app and the order was marked ready for pickup. " +
                        "Collection deadline: " +
                        order.PickupDeadline.Value.ToString(
                            "dd MMM yyyy HH:mm"
                        ) +
                        ".",

                    PerformedByRole =
                        "StoreStaff",

                    PerformedBy =
                        staff.FullName ??
                        staff.StoreStaffId.ToString(),

                    CreatedAt =
                        now
                }
            );

            db.Entry(order).State =
                EntityState.Modified;

            try
            {
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[MOBILE MARK READY DATABASE ERROR] " +
                    ex
                );

                return Ok(new
                {
                    success = false,
                    message = "The order could not be marked ready. No changes were saved."
                });
            }

            // ------------------------------------------------------
            // MEMBER NOTIFICATION
            // ------------------------------------------------------
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
                        "dd MMM yyyy HH:mm"
                    ) +
                    ".",
                    "Success",
                    "/Store/OrderConfirmation/" +
                    order.StoreOrderId
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[MOBILE READY NOTIFICATION ERROR] " +
                    ex
                );
            }

            // ------------------------------------------------------
            // MEMBER EMAIL
            // ------------------------------------------------------
            try
            {
                ProfessionalStoreEmailService.SendOrderReady(
                    order.Member.Email,
                    order.Member.FullName,
                    order.ReferenceNumber,
                    order.OrderQrBase64
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[MOBILE READY EMAIL ERROR] " +
                    ex
                );
            }

            return Ok(new
            {
                success = true,
                alreadyReady = false,
                orderId = order.StoreOrderId,
                referenceNumber = order.ReferenceNumber,
                memberName = order.Member.FullName,
                pickupDeadline = order.PickupDeadline,
                message = "Order marked ready for pickup successfully."
            });
        }


        // ==========================================================
        // VERIFY PICKUP QR AND RETURN ORDER DETAILS
        // POST: api/mobile/verify-pickup
        // ==========================================================
        [HttpPost]
        [Route("verify-pickup")]
        public IHttpActionResult VerifyPickup(
            MobileOrderQrRequest request)
        {
            // ------------------------------------------------------
            // VALIDATE REQUEST
            // ------------------------------------------------------
            if (request == null ||
                string.IsNullOrWhiteSpace(request.QrData) ||
                request.StoreStaffId <= 0)
            {
                return Ok(new MobilePickupOrderResponse
                {
                    Success = false,
                    Message = "QR data and Store Staff ID are required."
                });
            }

            // ------------------------------------------------------
            // VERIFY STORE STAFF
            // ------------------------------------------------------
            var staff =
                db.StoreStaffs
                    .FirstOrDefault(s =>
                        s.StoreStaffId ==
                            request.StoreStaffId &&
                        s.IsActive);

            if (staff == null)
            {
                return Ok(new MobilePickupOrderResponse
                {
                    Success = false,
                    Message = "Invalid or inactive Store Staff account."
                });
            }

            string qrData =
                request.QrData.Trim();

            // ------------------------------------------------------
            // READ COLLECTION QR
            // ------------------------------------------------------
            string reference =
                StoreOrderHelper.ExtractQrField(
                    qrData,
                    "Ref:"
                );

            string memberNumber =
                StoreOrderHelper.ExtractQrField(
                    qrData,
                    "MemberNumber:"
                );

            if (string.IsNullOrWhiteSpace(reference) ||
                string.IsNullOrWhiteSpace(memberNumber))
            {
                return Ok(new MobilePickupOrderResponse
                {
                    Success = false,
                    Message = "Invalid GymHub order QR code."
                });
            }

            // ------------------------------------------------------
            // FIND ORDER, MEMBER AND PRODUCTS
            // ------------------------------------------------------
            var order =
                db.StoreOrders
                    .Include(o => o.Member)
                    .Include(o => o.Items.Select(i => i.Product))
                    .FirstOrDefault(o =>
                        o.ReferenceNumber ==
                            reference);

            if (order == null)
            {
                return Ok(new MobilePickupOrderResponse
                {
                    Success = false,
                    Message = "Order not found."
                });
            }

            // ------------------------------------------------------
            // VERIFY MEMBER
            // ------------------------------------------------------
            if (order.Member == null ||
                order.Member.MemberNumber != memberNumber)
            {
                return Ok(new MobilePickupOrderResponse
                {
                    Success = false,
                    Message = "This QR code does not match the order member."
                });
            }

            // ------------------------------------------------------
            // VERIFY ORDER STATUS
            // ------------------------------------------------------
            if (order.Status == "Collected")
            {
                return Ok(new MobilePickupOrderResponse
                {
                    Success = false,
                    Message = "This order has already been collected."
                });
            }

            if (order.Status != "ReadyForPickup")
            {
                return Ok(new MobilePickupOrderResponse
                {
                    Success = false,
                    Message =
                        $"This order is not ready for pickup. Current status: {order.Status}."
                });
            }

            // ------------------------------------------------------
            // VERIFY STOCK IS STILL RESERVED
            // ------------------------------------------------------
            if (!order.IsPickupStockReserved)
            {
                return Ok(new MobilePickupOrderResponse
                {
                    Success = false,
                    Message = "This order no longer has an active pickup stock reservation."
                });
            }

            // ------------------------------------------------------
            // VERIFY PICKUP DEADLINE
            // ------------------------------------------------------
            if (order.PickupDeadline.HasValue &&
                order.PickupDeadline.Value < DateTime.Now)
            {
                return Ok(new MobilePickupOrderResponse
                {
                    Success = false,
                    Message =
                        "The pickup deadline for this order has passed. " +
                        "The order must be reviewed before collection."
                });
            }

            // ------------------------------------------------------
            // BUILD ORDER ITEMS
            // ------------------------------------------------------
            var items =
                order.Items
                    .Select(i =>
                        new MobilePickupOrderItemDto
                        {
                            StoreOrderItemId =
                                i.StoreOrderItemId,

                            ProductId =
                                i.ProductId,

                            ProductName =
                                i.Product != null
                                    ? i.Product.Name
                                    : "Unknown Product",

                            Category =
                                i.Product != null
                                    ? i.Product.Category
                                    : "",

                            ImagePath =
                                i.Product != null
                                    ? i.Product.ImagePath
                                    : null,

                            Quantity =
                                i.Quantity,

                            UnitPrice =
                                i.UnitPriceAtPurchase,

                            LineTotal =
                                i.Quantity *
                                i.UnitPriceAtPurchase
                        })
                    .ToList();

            // ------------------------------------------------------
            // RETURN PICKUP REVIEW DATA
            // ------------------------------------------------------
            return Ok(new MobilePickupOrderResponse
            {
                Success = true,
                Message = "Pickup verified successfully.",

                OrderId =
                    order.StoreOrderId,

                ReferenceNumber =
                    order.ReferenceNumber,

                Status =
                    order.Status,

                ReadyAt =
                    order.ReadyAt,

                PickupDeadline =
                    order.PickupDeadline,

                Subtotal =
                    order.Subtotal,

                DiscountAmount =
                    order.DiscountAmount,

                FinalTotal =
                    order.FinalTotal,

                ItemCount =
                    items.Sum(i => i.Quantity),

                Member =
                    new MobilePickupMemberDto
                    {
                        MemberId =
                            order.Member.MemberId,

                        MemberNumber =
                            order.Member.MemberNumber,

                        FullName =
                            order.Member.FullName,

                        Email =
                            order.Member.Email,

                        Phone =
                            order.Member.Phone,

                        Plan =
                            order.Member.Plan
                    },

                Items =
                    items
            });
        }


        // ==========================================================
        // CONFIRM COLLECTION
        // POST: api/mobile/confirm-collection
        // ==========================================================
        [HttpPost]
        [Route("confirm-collection")]
        public IHttpActionResult ConfirmCollection(
            MobileConfirmCollectionRequest request)
        {
            // ------------------------------------------------------
            // VALIDATE REQUEST
            // ------------------------------------------------------
            if (request == null ||
                request.OrderId <= 0 ||
                request.StoreStaffId <= 0 ||
                string.IsNullOrWhiteSpace(request.SignatureBase64))
            {
                return Ok(new
                {
                    success = false,
                    message = "Order, staff and signature details are required."
                });
            }

            // ------------------------------------------------------
            // VERIFY STORE STAFF
            // ------------------------------------------------------
            var staff =
                db.StoreStaffs
                    .FirstOrDefault(s =>
                        s.StoreStaffId == request.StoreStaffId &&
                        s.IsActive);

            if (staff == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "Invalid or inactive Store Staff account."
                });
            }

            // ------------------------------------------------------
            // FIND ORDER
            // ------------------------------------------------------
            var order =
                db.StoreOrders
                    .Include(o => o.Member)
                    .FirstOrDefault(o =>
                        o.StoreOrderId == request.OrderId);

            if (order == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "Order not found."
                });
            }

            // ------------------------------------------------------
            // PREVENT DUPLICATE COLLECTION
            // ------------------------------------------------------
            if (order.Status == "Collected")
            {
                return Ok(new
                {
                    success = false,
                    message = "This order has already been collected."
                });
            }

            // ------------------------------------------------------
            // VERIFY ORDER STATUS
            // ------------------------------------------------------
            if (order.Status != "ReadyForPickup")
            {
                return Ok(new
                {
                    success = false,
                    message =
                        "This order is not ready for collection. " +
                        "Current status: " +
                        order.Status +
                        "."
                });
            }

            // ------------------------------------------------------
            // VERIFY RESERVED STOCK
            // ------------------------------------------------------
            if (!order.IsPickupStockReserved)
            {
                return Ok(new
                {
                    success = false,
                    message =
                        "This order no longer has an active pickup stock reservation."
                });
            }

            DateTime now = DateTime.Now;

            // ------------------------------------------------------
            // VERIFY PICKUP DEADLINE
            // ------------------------------------------------------
            if (order.PickupDeadline.HasValue &&
                order.PickupDeadline.Value < now)
            {
                return Ok(new
                {
                    success = false,
                    message =
                        "The pickup deadline for this order has passed. " +
                        "The order must be reviewed before collection."
                });
            }

            string savedSignaturePath = null;

            try
            {
                // --------------------------------------------------
                // CLEAN BASE64 SIGNATURE
                // --------------------------------------------------
                string signatureData =
                    request.SignatureBase64.Trim();

                // Also supports:
                // data:image/png;base64,xxxxxxxx
                if (signatureData.Contains(","))
                {
                    signatureData =
                        signatureData.Substring(
                            signatureData.IndexOf(",") + 1
                        );
                }

                if (string.IsNullOrWhiteSpace(signatureData))
                {
                    return Ok(new
                    {
                        success = false,
                        message = "The signature data is empty."
                    });
                }

                // --------------------------------------------------
                // DECODE SIGNATURE
                // --------------------------------------------------
                byte[] signatureBytes =
                    Convert.FromBase64String(
                        signatureData
                    );

                if (signatureBytes == null ||
                    signatureBytes.Length == 0)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "The signature could not be captured."
                    });
                }

                // Maximum 2 MB
                const int maxSignatureBytes =
                    2 * 1024 * 1024;

                if (signatureBytes.Length >
                    maxSignatureBytes)
                {
                    return Ok(new
                    {
                        success = false,
                        message =
                            "The signature image is too large. Please sign again."
                    });
                }

                // --------------------------------------------------
                // SIGNATURE DIRECTORY
                // --------------------------------------------------
                string folderPath =
                    System.Web.Hosting.HostingEnvironment
                        .MapPath(
                            "~/Content/uploads/signatures"
                        );

                if (string.IsNullOrWhiteSpace(folderPath))
                {
                    return Ok(new
                    {
                        success = false,
                        message =
                            "The signature storage location is unavailable."
                    });
                }

                if (!System.IO.Directory.Exists(
                    folderPath))
                {
                    System.IO.Directory.CreateDirectory(
                        folderPath
                    );
                }

                // --------------------------------------------------
                // CREATE UNIQUE FILE NAME
                // --------------------------------------------------
                string fileName =
                    "signature_" +
                    order.StoreOrderId +
                    "_" +
                    Guid.NewGuid().ToString("N") +
                    ".png";

                string fullPath =
                    System.IO.Path.Combine(
                        folderPath,
                        fileName
                    );

                // --------------------------------------------------
                // SAVE SIGNATURE
                // --------------------------------------------------
                System.IO.File.WriteAllBytes(
                    fullPath,
                    signatureBytes
                );

                savedSignaturePath =
                    fullPath;

                order.PickupSignaturePath =
                    "/Content/uploads/signatures/" +
                    fileName;

                // --------------------------------------------------
                // BEGIN DATABASE TRANSACTION
                // --------------------------------------------------
                using (var transaction =
                    db.Database.BeginTransaction())
                {
                    try
                    {
                        // ------------------------------------------
                        // COMPLETE COLLECTION
                        // ------------------------------------------
                        order.Status =
                            "Collected";

                        order.CollectedAt =
                            now;

                        order.CollectedByStaffId =
                            request.StoreStaffId;

                        // ------------------------------------------
                        // RELEASE PICKUP RESERVATION
                        // ------------------------------------------
                        order.IsPickupStockReserved =
                            false;

                        // ------------------------------------------
                        // RESOLUTION INFORMATION
                        // ------------------------------------------
                        order.UncollectedResolutionStatus =
                            "Collected";

                        order.UncollectedResolutionNotes =
                            "Order collected by the member through the GymHub Staff mobile app.";

                        order.UncollectedResolvedBy =
                            staff.FullName ??
                            staff.StoreStaffId.ToString();

                        order.UncollectedResolvedAt =
                            now;

                        // ------------------------------------------
                        // COLLECTION AUDIT EVENT
                        // ------------------------------------------
                        db.StoreOrderCollectionEvents.Add(
                            new StoreOrderCollectionEvent
                            {
                                StoreOrderId =
                                    order.StoreOrderId,

                                EventType =
                                    "Collected",

                                Description =
                                    "Order collection was completed using the GymHub Staff mobile app. " +
                                    "The member signature was captured and the pickup stock reservation was released.",

                                PerformedByRole =
                                    "StoreStaff",

                                PerformedBy =
                                    staff.FullName ??
                                    staff.StoreStaffId.ToString(),

                                CreatedAt =
                                    now
                            }
                        );

                        db.Entry(order).State =
                            EntityState.Modified;

                        db.SaveChanges();

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();

                        throw;
                    }
                }

                // --------------------------------------------------
                // MEMBER NOTIFICATION
                // --------------------------------------------------
                if (order.Member != null)
                {
                    try
                    {
                        new NotificationService(db).Notify(
                            "Member",
                            order.MemberId,
                            "Order Collected",
                            "Your order " +
                            order.ReferenceNumber +
                            " was collected successfully on " +
                            now.ToString(
                                "dd MMM yyyy HH:mm"
                            ) +
                            ".",
                            "Success",
                            "/Store/OrderConfirmation/" +
                            order.StoreOrderId
                        );
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "[MOBILE COLLECTION NOTIFICATION ERROR] " +
                            ex
                        );
                    }
                }

                // --------------------------------------------------
                // SUCCESS
                // --------------------------------------------------
                return Ok(new
                {
                    success = true,

                    orderId =
                        order.StoreOrderId,

                    referenceNumber =
                        order.ReferenceNumber,

                    collectedAt =
                        order.CollectedAt,

                    collectedBy =
                        staff.FullName ??
                        staff.StoreStaffId.ToString(),

                    message =
                        "Collection completed successfully."
                });
            }
            catch (FormatException)
            {
                // --------------------------------------------------
                // DELETE SIGNATURE IF IT WAS CREATED
                // --------------------------------------------------
                if (!string.IsNullOrWhiteSpace(
                        savedSignaturePath) &&
                    System.IO.File.Exists(
                        savedSignaturePath))
                {
                    try
                    {
                        System.IO.File.Delete(
                            savedSignaturePath
                        );
                    }
                    catch
                    {
                        // Ignore cleanup failure.
                    }
                }

                return Ok(new
                {
                    success = false,
                    message = "The signature data is invalid."
                });
            }
            catch (Exception ex)
            {
                // --------------------------------------------------
                // DELETE ORPHANED SIGNATURE FILE
                // --------------------------------------------------
                if (!string.IsNullOrWhiteSpace(
                        savedSignaturePath) &&
                    System.IO.File.Exists(
                        savedSignaturePath))
                {
                    try
                    {
                        System.IO.File.Delete(
                            savedSignaturePath
                        );
                    }
                    catch (Exception cleanupEx)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "[MOBILE SIGNATURE CLEANUP ERROR] " +
                            cleanupEx
                        );
                    }
                }

                System.Diagnostics.Debug.WriteLine(
                    "[MOBILE COLLECTION ERROR] " +
                    ex
                );

                return Ok(new
                {
                    success = false,
                    message =
                        "Unable to complete collection. No collection changes were saved."
                });
            }
        }


        // ==========================================================
        // PASSWORD HASHER
        // ==========================================================
        private string HashPassword(
            string password)
        {
            using (
                SHA256 sha256 =
                    SHA256.Create())
            {
                byte[] bytes =
                    sha256.ComputeHash(
                        Encoding.UTF8.GetBytes(
                            password
                        )
                    );

                StringBuilder builder =
                    new StringBuilder();

                foreach (byte b in bytes)
                {
                    builder.Append(
                        b.ToString("x2")
                    );
                }

                return builder.ToString();
            }
        }


        // ==========================================================
        // DISPOSE DATABASE CONTEXT
        // ==========================================================
        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}