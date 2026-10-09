using SwiftyS26.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace SwiftyS26.Models
{
    public class StoreReportService
    {
        private readonly GymHubDB db;

        public StoreReportService(GymHubDB context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            db = context;
        }

        // ============================================================
        // REPORTING DASHBOARD
        // ============================================================

        public StoreReportsDashboardViewModel
            BuildDashboard()
        {
            var allOrders = db.StoreOrders.AsNoTracking();

            int totalOrders = allOrders.Count();

            int collectedOrders = allOrders.Count(o =>
                o.Status == "Collected");

            int readyOrders = allOrders.Count(o =>
                o.Status == "ReadyForPickup");

            string[] uncollectedStatuses =
            {
                "Uncollected",
                "UncollectedPendingReview",
                "UncollectedRefundPending",
                "UncollectedRefunded",
                "UncollectedResolved"
            };

            int uncollectedOrders = allOrders.Count(o =>
                uncollectedStatuses.Contains(o.Status));

            decimal totalRevenue = allOrders
                .Where(o =>
                    o.Status != "Cancelled" &&
                    o.Status != "Refunded" &&
                    o.Status != "UncollectedRefunded")
                .Select(o => (decimal?)o.FinalTotal)
                .Sum() ?? 0m;

            decimal totalDiscounts = allOrders
                .Select(o => (decimal?)o.DiscountAmount)
                .Sum() ?? 0m;

            decimal totalRefunds = db.ReturnRequests
                .Where(r =>
                    r.Status == "Approved" ||
                    r.Status == "Refunded" ||
                    r.Status == "RefundProcessing")
                .Select(r => r.RefundAmount ?? 0m)
                .DefaultIfEmpty(0m)
                .Sum();

            decimal collectionSuccessRate = 0m;
            decimal uncollectedRate = 0m;

            int completedCollectionProcesses =
                collectedOrders + uncollectedOrders;

            if (completedCollectionProcesses > 0)
            {
                collectionSuccessRate =
                    Math.Round(
                        collectedOrders * 100m /
                        completedCollectionProcesses,
                        2);

                uncollectedRate =
                    Math.Round(
                        uncollectedOrders * 100m /
                        completedCollectionProcesses,
                        2);
            }

            var collectionTimes = db.StoreOrders
                .AsNoTracking()
                .Where(o =>
                    o.Status == "Collected" &&
                    o.ReadyAt.HasValue &&
                    o.CollectedAt.HasValue)
                .Select(o => new
                {
                    ReadyAt = o.ReadyAt.Value,
                    CollectedAt = o.CollectedAt.Value
                })
                .ToList();

            double averageCollectionHours = 0;

            if (collectionTimes.Any())
            {
                averageCollectionHours =
                    Math.Round(
                        collectionTimes.Average(o =>
                            (o.CollectedAt - o.ReadyAt)
                                .TotalHours),
                        1);
            }

            return new StoreReportsDashboardViewModel
            {
                TotalOrders = totalOrders,
                CollectedOrders = collectedOrders,
                ReadyForPickupOrders = readyOrders,
                UncollectedOrders = uncollectedOrders,

                TotalCollectionEvents =
                    db.StoreOrderCollectionEvents
                        .AsNoTracking()
                        .Count(),

                TotalInventoryMovements =
                    db.InventoryMovements
                        .AsNoTracking()
                        .Count(),

                TotalStoreRevenue = totalRevenue,
                TotalDiscountsGranted = totalDiscounts,
                TotalRefunds = totalRefunds,

                CollectionSuccessRate =
                    collectionSuccessRate,

                UncollectedRate =
                    uncollectedRate,

                AverageCollectionHours =
                    averageCollectionHours,

                GeneratedAt = DateTime.Now
            };
        }

        // ============================================================
        // ORDER HISTORY QUERY
        // ============================================================

        public IQueryable<StoreOrder> GetOrderHistoryQuery(
            string search,
            string status,
            DateTime? fromDate,
            DateTime? toDate,
            bool discountOnly,
            decimal? minimumAmount,
            decimal? maximumAmount)
        {
            IQueryable<StoreOrder> query =
                db.StoreOrders
                    .AsNoTracking()
                    .Include(o => o.Member)
                    .Include(o => o.Items.Select(i => i.Product));

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(o =>
                    o.ReferenceNumber.Contains(search) ||
                    o.PaystackReference.Contains(search) ||
                    o.Member.FullName.Contains(search) ||
                    o.Member.Email.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(o =>
                    o.Status == status);
            }

            if (fromDate.HasValue)
            {
                DateTime startDate = fromDate.Value.Date;

                query = query.Where(o =>
                    o.CreatedAt >= startDate);
            }

            if (toDate.HasValue)
            {
                DateTime endDateExclusive =
                    toDate.Value.Date.AddDays(1);

                query = query.Where(o =>
                    o.CreatedAt < endDateExclusive);
            }

            if (discountOnly)
            {
                query = query.Where(o =>
                    o.DiscountAmount > 0);
            }

            if (minimumAmount.HasValue)
            {
                decimal minimum =
                    minimumAmount.Value;

                query = query.Where(o =>
                    o.FinalTotal >= minimum);
            }

            if (maximumAmount.HasValue)
            {
                decimal maximum =
                    maximumAmount.Value;

                query = query.Where(o =>
                    o.FinalTotal <= maximum);
            }

            return query.OrderByDescending(o =>
                o.CreatedAt);
        }

        // ============================================================
        // BUILD ORDER HISTORY PAGE
        // ============================================================

        public OrderHistoryPageViewModel
            BuildOrderHistoryPage(
                string search,
                string status,
                DateTime? fromDate,
                DateTime? toDate,
                bool discountOnly,
                decimal? minimumAmount,
                decimal? maximumAmount)
        {
            var query = GetOrderHistoryQuery(
                search,
                status,
                fromDate,
                toDate,
                discountOnly,
                minimumAmount,
                maximumAmount);

            var orders = query.ToList();

            string[] activeStatuses =
            {
                "Paid",
                "Preparing",
                "ReadyForPickup"
            };

            string[] uncollectedStatuses =
            {
                "Uncollected",
                "UncollectedPendingReview",
                "UncollectedRefundPending",
                "UncollectedRefunded",
                "UncollectedResolved"
            };

            return new OrderHistoryPageViewModel
            {
                Orders = orders,

                Search = search,
                Status = status,
                FromDate = fromDate,
                ToDate = toDate,
                DiscountOnly = discountOnly,
                MinimumAmount = minimumAmount,
                MaximumAmount = maximumAmount,

                TotalRecords = orders.Count,

                CollectedCount = orders.Count(o =>
                    o.Status == "Collected"),

                ActiveCount = orders.Count(o =>
                    activeStatuses.Contains(o.Status)),

                CancelledCount = orders.Count(o =>
                    o.Status == "Cancelled"),

                UncollectedCount = orders.Count(o =>
                    uncollectedStatuses.Contains(o.Status)),

                TotalSubtotal = orders.Sum(o =>
                    o.Subtotal),

                TotalDiscounts = orders.Sum(o =>
                    o.DiscountAmount),

                TotalPaid = orders.Sum(o =>
                    o.FinalTotal),

                GeneratedAt = DateTime.Now
            };
        }

        // ============================================================
        // INDIVIDUAL ORDER HISTORY RECORD
        // ============================================================

        public OrderHistoryDetailsViewModel
            BuildOrderHistoryDetails(int orderId)
        {
            var order = db.StoreOrders
                .AsNoTracking()
                .Include(o => o.Member)
                .Include(o => o.Items.Select(i => i.Product))
                .FirstOrDefault(o =>
                    o.StoreOrderId == orderId);

            if (order == null)
            {
                return null;
            }

            var collectionEvents =
                db.StoreOrderCollectionEvents
                    .AsNoTracking()
                    .Where(e =>
                        e.StoreOrderId == orderId)
                    .OrderByDescending(e =>
                        e.CreatedAt)
                    .ToList();

            var inventoryMovements =
                db.InventoryMovements
                    .AsNoTracking()
                    .Include(m => m.Product)
                    .Where(m =>
                        m.ReferenceType == "StoreOrder" &&
                        m.ReferenceId == orderId)
                    .OrderByDescending(m =>
                        m.CreatedAt)
                    .ToList();

            var itemIds = order.Items
                .Select(i => i.StoreOrderItemId)
                .ToList();

            var returnRequests =
                db.ReturnRequests
                    .AsNoTracking()
                    .Include(r => r.ExchangeProduct)
                    .Where(r =>
                        itemIds.Contains(
                            r.StoreOrderItemId))
                    .OrderByDescending(r =>
                        r.RequestedAt)
                    .ToList();

            return new OrderHistoryDetailsViewModel
            {
                Order = order,

                CollectionEvents =
                    collectionEvents,

                InventoryMovements =
                    inventoryMovements,

                ReturnRequests =
                    returnRequests,

                TotalItemQuantity =
                    order.Items.Sum(i => i.Quantity)
            };
        }

        // ============================================================
        // COLLECTION HISTORY QUERY
        // ============================================================

        public IQueryable<StoreOrder>
            GetCollectionHistoryQuery(
                string search,
                string outcome,
                DateTime? fromDate,
                DateTime? toDate)
        {
            string[] collectionStatuses =
            {
                "ReadyForPickup",
                "Collected",
                "Uncollected",
                "UncollectedPendingReview",
                "UncollectedRefundPending",
                "UncollectedRefunded",
                "UncollectedResolved"
            };

            IQueryable<StoreOrder> query =
                db.StoreOrders
                    .AsNoTracking()
                    .Include(o => o.Member)
                    .Include(o =>
                        o.Items.Select(i => i.Product))
                    .Where(o =>
                        collectionStatuses.Contains(
                            o.Status));

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(o =>
                    o.ReferenceNumber.Contains(search) ||
                    o.Member.FullName.Contains(search) ||
                    o.Member.Email.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(outcome))
            {
                switch (outcome)
                {
                    case "Ready":
                        query = query.Where(o =>
                            o.Status == "ReadyForPickup" &&
                            o.PickupDeadline.HasValue &&
                            o.PickupDeadline.Value >
                                DateTime.Now);
                        break;

                    case "Overdue":
                        query = query.Where(o =>
                            o.Status == "ReadyForPickup" &&
                            o.PickupDeadline.HasValue &&
                            o.PickupDeadline.Value <=
                                DateTime.Now);
                        break;

                    case "Collected":
                        query = query.Where(o =>
                            o.Status == "Collected");
                        break;

                    case "Uncollected":
                        query = query.Where(o =>
                            o.Status == "Uncollected" ||
                            o.Status ==
                                "UncollectedPendingReview" ||
                            o.Status ==
                                "UncollectedRefundPending" ||
                            o.Status ==
                                "UncollectedRefunded" ||
                            o.Status ==
                                "UncollectedResolved");
                        break;

                    case "Refunded":
                        query = query.Where(o =>
                            o.Status ==
                                "UncollectedRefunded");
                        break;

                    case "Resolved":
                        query = query.Where(o =>
                            o.Status ==
                                "UncollectedResolved");
                        break;
                }
            }

            if (fromDate.HasValue)
            {
                DateTime startDate =
                    fromDate.Value.Date;

                query = query.Where(o =>
                    (o.ReadyAt.HasValue &&
                     o.ReadyAt.Value >= startDate) ||
                    (!o.ReadyAt.HasValue &&
                     o.CreatedAt >= startDate));
            }

            if (toDate.HasValue)
            {
                DateTime endDateExclusive =
                    toDate.Value.Date.AddDays(1);

                query = query.Where(o =>
                    (o.ReadyAt.HasValue &&
                     o.ReadyAt.Value <
                        endDateExclusive) ||
                    (!o.ReadyAt.HasValue &&
                     o.CreatedAt <
                        endDateExclusive));
            }

            return query.OrderByDescending(o =>
                o.ReadyAt ?? o.CreatedAt);
        }

        // ============================================================
        // BUILD COLLECTION HISTORY PAGE
        // ============================================================

        public CollectionHistoryPageViewModel
            BuildCollectionHistoryPage(
                string search,
                string outcome,
                DateTime? fromDate,
                DateTime? toDate)
        {
            var query = GetCollectionHistoryQuery(
                search,
                outcome,
                fromDate,
                toDate);

            var orders = query.ToList();
            DateTime now = DateTime.Now;

            int collectedCount = orders.Count(o =>
                o.Status == "Collected");

            int readyCount = orders.Count(o =>
                o.Status == "ReadyForPickup" &&
                (!o.PickupDeadline.HasValue ||
                 o.PickupDeadline.Value > now));

            int approachingCount = orders.Count(o =>
                o.Status == "ReadyForPickup" &&
                o.PickupDeadline.HasValue &&
                o.PickupDeadline.Value > now &&
                o.PickupDeadline.Value <=
                    now.AddHours(24));

            int overdueCount = orders.Count(o =>
                o.Status == "ReadyForPickup" &&
                o.PickupDeadline.HasValue &&
                o.PickupDeadline.Value <= now);

            string[] uncollectedStatuses =
            {
                "Uncollected",
                "UncollectedPendingReview",
                "UncollectedRefundPending",
                "UncollectedRefunded",
                "UncollectedResolved"
            };

            int uncollectedCount = orders.Count(o =>
                uncollectedStatuses.Contains(o.Status));

            int completedProcesses =
                collectedCount + uncollectedCount;

            decimal successRate = 0m;
            decimal uncollectedRate = 0m;

            if (completedProcesses > 0)
            {
                successRate = Math.Round(
                    collectedCount * 100m /
                    completedProcesses,
                    2);

                uncollectedRate = Math.Round(
                    uncollectedCount * 100m /
                    completedProcesses,
                    2);
            }

            var collectionDurations = orders
                .Where(o =>
                    o.Status == "Collected" &&
                    o.ReadyAt.HasValue &&
                    o.CollectedAt.HasValue)
                .Select(o =>
                    (o.CollectedAt.Value -
                     o.ReadyAt.Value).TotalHours)
                .ToList();

            double averageHours =
                collectionDurations.Any()
                    ? Math.Round(
                        collectionDurations.Average(),
                        1)
                    : 0;

            return new CollectionHistoryPageViewModel
            {
                Orders = orders,

                Search = search,
                Outcome = outcome,
                FromDate = fromDate,
                ToDate = toDate,

                TotalRecords = orders.Count,
                CollectedCount = collectedCount,
                ReadyCount = readyCount,

                ApproachingDeadlineCount =
                    approachingCount,

                OverdueCount = overdueCount,
                UncollectedCount = uncollectedCount,

                AverageCollectionHours =
                    averageHours,

                CollectionSuccessRate =
                    successRate,

                UncollectedRate =
                    uncollectedRate,

                GeneratedAt = DateTime.Now
            };
        }

        // ============================================================
        // INDIVIDUAL COLLECTION RECORD
        // ============================================================

        public CollectionHistoryDetailsViewModel
            BuildCollectionHistoryDetails(int orderId)
        {
            var order = db.StoreOrders
                .AsNoTracking()
                .Include(o => o.Member)
                .Include(o =>
                    o.Items.Select(i => i.Product))
                .FirstOrDefault(o =>
                    o.StoreOrderId == orderId);

            if (order == null)
            {
                return null;
            }

            var events = db.StoreOrderCollectionEvents
                .AsNoTracking()
                .Where(e =>
                    e.StoreOrderId == orderId)
                .OrderBy(e =>
                    e.CreatedAt)
                .ToList();

            TimeSpan? collectionDuration = null;

            if (order.ReadyAt.HasValue &&
                order.CollectedAt.HasValue)
            {
                collectionDuration =
                    order.CollectedAt.Value -
                    order.ReadyAt.Value;
            }

            bool collectedOnTime =
                order.CollectedAt.HasValue &&
                order.PickupDeadline.HasValue &&
                order.CollectedAt.Value <=
                    order.PickupDeadline.Value;

            bool deadlineExtended = events.Any(e =>
                e.EventType == "DeadlineExtended" ||
                e.EventType == "PickupDeadlineExtended");

            return new CollectionHistoryDetailsViewModel
            {
                Order = order,
                Events = events,

                CollectionOutcome =
                    GetCollectionOutcome(order),

                CollectionDuration =
                    collectionDuration,

                WasCollectedOnTime =
                    collectedOnTime,

                WasDeadlineExtended =
                    deadlineExtended
            };
        }

        // ============================================================
        // INVENTORY MOVEMENT QUERY
        // ============================================================

        public IQueryable<InventoryMovement>
            GetInventoryMovementQuery(
                string search,
                string movementType,
                string referenceType,
                int? productId,
                DateTime? fromDate,
                DateTime? toDate)
        {
            IQueryable<InventoryMovement> query =
                db.InventoryMovements
                    .AsNoTracking()
                    .Include(m => m.Product);

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(m =>
                    m.Product.Name.Contains(search) ||
                    m.ReferenceNumber.Contains(search) ||
                    m.PerformedBy.Contains(search) ||
                    m.Notes.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(
                movementType))
            {
                query = query.Where(m =>
                    m.MovementType == movementType);
            }

            if (!string.IsNullOrWhiteSpace(
                referenceType))
            {
                query = query.Where(m =>
                    m.ReferenceType == referenceType);
            }

            if (productId.HasValue)
            {
                int selectedProductId =
                    productId.Value;

                query = query.Where(m =>
                    m.ProductId ==
                    selectedProductId);
            }

            if (fromDate.HasValue)
            {
                DateTime startDate =
                    fromDate.Value.Date;

                query = query.Where(m =>
                    m.CreatedAt >= startDate);
            }

            if (toDate.HasValue)
            {
                DateTime endDateExclusive =
                    toDate.Value.Date.AddDays(1);

                query = query.Where(m =>
                    m.CreatedAt <
                    endDateExclusive);
            }

            return query.OrderByDescending(m =>
                m.CreatedAt);
        }

        // ============================================================
        // BUILD INVENTORY MOVEMENT PAGE
        // ============================================================

        public InventoryMovementHistoryPageViewModel
            BuildInventoryMovementHistoryPage(
                string search,
                string movementType,
                string referenceType,
                int? productId,
                DateTime? fromDate,
                DateTime? toDate)
        {
            var query = GetInventoryMovementQuery(
                search,
                movementType,
                referenceType,
                productId,
                fromDate,
                toDate);

            var movements = query.ToList();

            int unitsAdded = movements
                .Where(m => m.QuantityChange > 0)
                .Sum(m => (int?)m.QuantityChange) ?? 0;

            int unitsRemoved = movements
                .Where(m => m.QuantityChange < 0)
                .Sum(m => (int?)-m.QuantityChange) ?? 0;

            return new InventoryMovementHistoryPageViewModel
            {
                Movements = movements,

                Search = search,
                MovementType = movementType,
                ReferenceType = referenceType,
                ProductId = productId,
                FromDate = fromDate,
                ToDate = toDate,

                TotalRecords = movements.Count,

                TotalUnitsAdded = unitsAdded,
                TotalUnitsRemoved = unitsRemoved,

                NetQuantityChange =
                    movements.Sum(m =>
                        m.QuantityChange),

                SupplierDeliveryUnits =
                    SumPositiveMovement(
                        movements,
                        "SupplierDelivery"),

                StoreOrderReservationUnits =
                    SumAbsoluteMovement(
                        movements,
                        "StoreOrderReservation"),

                UncollectedReleasedUnits =
                    SumPositiveMovement(
                        movements,
                        "UncollectedRelease"),

                ReturnedToStockUnits =
                    SumPositiveMovement(
                        movements,
                        "ReturnRestocked"),

                DamagedWriteOffUnits =
                    SumAbsoluteMovement(
                        movements,
                        "DamagedWriteOff"),

                GeneratedAt = DateTime.Now
            };
        }

        // ============================================================
        // PRIVATE REPORT HELPERS
        // ============================================================

        private static int SumPositiveMovement(
            IEnumerable<InventoryMovement> movements,
            string movementType)
        {
            return movements
                .Where(m =>
                    m.MovementType == movementType &&
                    m.QuantityChange > 0)
                .Sum(m =>
                    (int?)m.QuantityChange) ?? 0;
        }

        private static int SumAbsoluteMovement(
            IEnumerable<InventoryMovement> movements,
            string movementType)
        {
            return movements
                .Where(m =>
                    m.MovementType == movementType)
                .Sum(m =>
                    (int?)Math.Abs(
                        m.QuantityChange)) ?? 0;
        }

        public static string GetCollectionOutcome(
            StoreOrder order)
        {
            if (order == null)
            {
                return "Unknown";
            }

            switch (order.Status)
            {
                case "Collected":
                    if (order.CollectedAt.HasValue &&
                        order.PickupDeadline.HasValue &&
                        order.CollectedAt.Value <=
                            order.PickupDeadline.Value)
                    {
                        return "Collected On Time";
                    }

                    return "Collected After Deadline";

                case "ReadyForPickup":
                    if (order.PickupDeadline.HasValue &&
                        order.PickupDeadline.Value <=
                            DateTime.Now)
                    {
                        return "Overdue";
                    }

                    if (order.PickupDeadline.HasValue &&
                        order.PickupDeadline.Value <=
                            DateTime.Now.AddHours(24))
                    {
                        return "Approaching Deadline";
                    }

                    return "Awaiting Collection";

                case "Uncollected":
                case "UncollectedPendingReview":
                    return "Uncollected – Awaiting Review";

                case "UncollectedRefundPending":
                    return "Uncollected – Refund Pending";

                case "UncollectedRefunded":
                    return "Uncollected – Refunded";

                case "UncollectedResolved":
                    return "Uncollected – Resolved";

                default:
                    return order.Status;
            }
        }
    }
}