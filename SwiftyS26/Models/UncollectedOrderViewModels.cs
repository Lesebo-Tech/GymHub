using SwiftyS26.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SwiftyS26.ViewModels
{
    // ============================================================
    // UNCOLLECTED ORDERS DASHBOARD
    // Used by Views/StoreAdmin/UncollectedOrders.cshtml
    // ============================================================
    public class UncollectedOrdersPageViewModel
    {
        public UncollectedOrdersPageViewModel()
        {
            Orders = new List<StoreOrder>();
        }

        public IList<StoreOrder> Orders { get; set; }

        // Current search and filter selections.
        public string Search { get; set; }

        public string Status { get; set; }

        // Dashboard summary values.
        public int ApproachingDeadlineCount { get; set; }

        public int OverdueCount { get; set; }

        public int AwaitingReviewCount { get; set; }

        public int RefundPendingCount { get; set; }

        public int ResolvedThisMonthCount { get; set; }

        // Controls whether Admin-only actions are displayed.
        public bool IsAdmin { get; set; }
    }


    // ============================================================
    // UNCOLLECTED ORDER REVIEW PAGE
    // Used by Views/StoreAdmin/ReviewUncollectedOrder.cshtml
    // ============================================================
    public class ReviewUncollectedOrderViewModel
    {
        public ReviewUncollectedOrderViewModel()
        {
            Events =
                new List<StoreOrderCollectionEvent>();

            StockProblems =
                new List<string>();
        }

        public StoreOrder Order { get; set; }

        public IList<StoreOrderCollectionEvent>
            Events
        { get; set; }

        public bool IsAdmin { get; set; }

        // True only when every order item has sufficient stock
        // to reopen and reserve the order.
        public bool CanReReserveStock { get; set; }

        public IList<string> StockProblems { get; set; }
    }


    // ============================================================
    // OPTIONAL COMBINED RESOLUTION INPUT MODEL
    // This can be used later if all Admin decisions are submitted
    // through one controller action.
    // ============================================================
    public class UncollectedResolutionInputViewModel
    {
        [Required]
        public int OrderId { get; set; }

        [Required]
        [StringLength(30)]
        public string Resolution { get; set; }

        [Display(Name = "New pickup deadline")]
        public DateTime? NewDeadline { get; set; }

        [Range(
            typeof(decimal),
            "0.01",
            "999999999.99",
            ErrorMessage =
                "Enter a valid refund amount.")]
        [Display(Name = "Refund amount")]
        public decimal? RefundAmount { get; set; }

        [Required(
            ErrorMessage =
                "A decision reason is required.")]
        [StringLength(
            500,
            MinimumLength = 5,
            ErrorMessage =
                "The reason must contain between 5 and 500 characters.")]
        public string Reason { get; set; }

        // Used for optimistic concurrency checking.
        public byte[] RowVersion { get; set; }
    }
}