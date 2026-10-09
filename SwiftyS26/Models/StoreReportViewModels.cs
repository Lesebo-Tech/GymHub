using SwiftyS26.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SwiftyS26.ViewModels
{
    // ================================================================
    // MAIN STORE REPORTING DASHBOARD
    // ================================================================

    public class StoreReportsDashboardViewModel
    {
        public int TotalOrders { get; set; }
        public int CollectedOrders { get; set; }
        public int ReadyForPickupOrders { get; set; }
        public int UncollectedOrders { get; set; }

        public int TotalCollectionEvents { get; set; }
        public int TotalInventoryMovements { get; set; }

        public decimal TotalStoreRevenue { get; set; }
        public decimal TotalDiscountsGranted { get; set; }
        public decimal TotalRefunds { get; set; }

        public decimal CollectionSuccessRate { get; set; }
        public decimal UncollectedRate { get; set; }

        public double AverageCollectionHours { get; set; }

        public DateTime GeneratedAt { get; set; } = DateTime.Now;
    }

    // ================================================================
    // ORDER HISTORY
    // ================================================================

    public class OrderHistoryPageViewModel
    {
        public OrderHistoryPageViewModel()
        {
            Orders = new List<StoreOrder>();
        }

        public List<StoreOrder> Orders { get; set; }

        public string Search { get; set; }
        public string Status { get; set; }

        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        public bool DiscountOnly { get; set; }

        public decimal? MinimumAmount { get; set; }
        public decimal? MaximumAmount { get; set; }

        public int TotalRecords { get; set; }
        public int CollectedCount { get; set; }
        public int ActiveCount { get; set; }
        public int CancelledCount { get; set; }
        public int UncollectedCount { get; set; }

        public decimal TotalSubtotal { get; set; }
        public decimal TotalDiscounts { get; set; }
        public decimal TotalPaid { get; set; }

        public DateTime GeneratedAt { get; set; } = DateTime.Now;
    }

    // ================================================================
    // INDIVIDUAL ORDER RECORD
    // ================================================================

    public class OrderHistoryDetailsViewModel
    {
        public OrderHistoryDetailsViewModel()
        {
            CollectionEvents =
                new List<StoreOrderCollectionEvent>();

            InventoryMovements =
                new List<InventoryMovement>();

            ReturnRequests =
                new List<ReturnRequest>();
        }

        public StoreOrder Order { get; set; }

        public List<StoreOrderCollectionEvent>
            CollectionEvents
        { get; set; }

        public List<InventoryMovement>
            InventoryMovements
        { get; set; }

        public List<ReturnRequest>
            ReturnRequests
        { get; set; }

        public int TotalItemQuantity { get; set; }

        public bool HasCollectionHistory
        {
            get
            {
                return CollectionEvents != null &&
                       CollectionEvents.Count > 0;
            }
        }

        public bool HasInventoryHistory
        {
            get
            {
                return InventoryMovements != null &&
                       InventoryMovements.Count > 0;
            }
        }

        public bool HasReturns
        {
            get
            {
                return ReturnRequests != null &&
                       ReturnRequests.Count > 0;
            }
        }
    }

    // ================================================================
    // COLLECTION HISTORY
    // ================================================================

    public class CollectionHistoryPageViewModel
    {
        public CollectionHistoryPageViewModel()
        {
            Orders = new List<StoreOrder>();
        }

        public List<StoreOrder> Orders { get; set; }

        public string Search { get; set; }
        public string Outcome { get; set; }

        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        public int TotalRecords { get; set; }
        public int CollectedCount { get; set; }
        public int ReadyCount { get; set; }
        public int ApproachingDeadlineCount { get; set; }
        public int OverdueCount { get; set; }
        public int UncollectedCount { get; set; }

        public double AverageCollectionHours { get; set; }

        public decimal CollectionSuccessRate { get; set; }
        public decimal UncollectedRate { get; set; }

        public DateTime GeneratedAt { get; set; } = DateTime.Now;
    }

    // ================================================================
    // INDIVIDUAL COLLECTION RECORD
    // ================================================================

    public class CollectionHistoryDetailsViewModel
    {
        public CollectionHistoryDetailsViewModel()
        {
            Events = new List<StoreOrderCollectionEvent>();
        }

        public StoreOrder Order { get; set; }

        public List<StoreOrderCollectionEvent> Events { get; set; }

        public string CollectionOutcome { get; set; }

        public TimeSpan? CollectionDuration { get; set; }

        public bool WasCollectedOnTime { get; set; }
        public bool WasDeadlineExtended { get; set; }

        public bool HasSignature
        {
            get
            {
                return Order != null &&
                       !string.IsNullOrWhiteSpace(
                           Order.PickupSignaturePath);
            }
        }
    }

    // ================================================================
    // INVENTORY MOVEMENT HISTORY
    // ================================================================

    public class InventoryMovementHistoryPageViewModel
    {
        public InventoryMovementHistoryPageViewModel()
        {
            Movements = new List<InventoryMovement>();
        }

        public List<InventoryMovement> Movements { get; set; }

        public string Search { get; set; }
        public string MovementType { get; set; }
        public string ReferenceType { get; set; }

        public int? ProductId { get; set; }

        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        public int TotalRecords { get; set; }

        public int TotalUnitsAdded { get; set; }
        public int TotalUnitsRemoved { get; set; }
        public int NetQuantityChange { get; set; }

        public int SupplierDeliveryUnits { get; set; }
        public int StoreOrderReservationUnits { get; set; }
        public int UncollectedReleasedUnits { get; set; }
        public int ReturnedToStockUnits { get; set; }
        public int DamagedWriteOffUnits { get; set; }

        public DateTime GeneratedAt { get; set; } = DateTime.Now;
    }

    // ================================================================
    // REPORT FILTER REQUEST
    // Used by PDF and Excel export actions.
    // ================================================================

    public class StoreReportFilterViewModel
    {
        public string Search { get; set; }
        public string Status { get; set; }
        public string Outcome { get; set; }

        public string MovementType { get; set; }
        public string ReferenceType { get; set; }

        public int? ProductId { get; set; }

        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        public bool DiscountOnly { get; set; }

        public decimal? MinimumAmount { get; set; }
        public decimal? MaximumAmount { get; set; }
    }
}