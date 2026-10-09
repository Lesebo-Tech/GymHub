using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class ReturnRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ReturnRequestId { get; set; }

        [StringLength(20)]
        public string ReferenceNumber { get; set; }

        // ============================================================
        // ORIGINAL PURCHASE
        // ============================================================

        [Required]
        [ForeignKey("StoreOrderItem")]
        public int StoreOrderItemId { get; set; }
        // ============================================================
        // SECURE RETURN / / EX
        // ============================================================

        // SHA-256 hash of the secret token contained inside the QR.
        // The raw token is never stored in the database.
        [StringLength(64)]
        public string ReturnQrTokenHash { get; set; }

        // QR image displayed to the member under My Orders.
        [Column(TypeName = "nvarchar(MAX)")]
        public string ReturnQrBase64 { get; set; }

        public DateTime? ReturnQrGeneratedAt { get; set; }

        public virtual StoreOrderItem StoreOrderItem { get; set; }

        [Required]
        [ForeignKey("Member")]
        public int MemberId { get; set; }

        public virtual MemberForm Member { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        [StringLength(30)]
        public string Reason { get; set; }

        [StringLength(1000)]
        public string Description { get; set; }

        // ============================================================
        // REQUESTED RESOLUTION
        // ============================================================

        // Refund | Exchange
        //
        // This is nullable for compatibility with existing requests.
        // Existing records without a value are treated as Refund.
        [StringLength(20)]
        public string RequestedResolution { get; set; } = "Refund";

        // SameProduct | DifferentProduct
        [StringLength(30)]
        public string ExchangeType { get; set; }

        // The product requested as a replacement.
        // For a same-product exchange, this will contain the original
        // ProductId.
        [ForeignKey("ExchangeProduct")]
        public int? ExchangeProductId { get; set; }

        public virtual Product ExchangeProduct { get; set; }

        public int? ExchangeQuantity { get; set; }

        // Financial values captured when the request is submitted.
        // Snapshots prevent later product price changes from changing
        // an already-submitted exchange request.
        public decimal? ReturnCreditAmount { get; set; }

        public decimal? ExchangeUnitPrice { get; set; }

        public decimal? ExchangeTotalAmount { get; set; }

        // Positive: member must pay GymHub.
        // Negative: GymHub owes the member a partial refund.
        // Zero: equal-value exchange.
        public decimal? ExchangePriceDifference { get; set; }

        // ============================================================
        // REQUEST WORKFLOW
        // ============================================================

        // Pending
        // StaffReviewed
        // PendingAdminApproval
        // Approved
        // Rejected
        // RefundProcessing
        // Refunded
        // ExchangeApproved
        // AwaitingExchangePayment
        // ReadyForExchange
        // ExchangeCompleted
        // Cancelled
        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Pending";

        public DateTime RequestedAt { get; set; } = DateTime.Now;

        // ============================================================
        // STAFF PHYSICAL INSPECTION
        // ============================================================

        public DateTime? ItemReceivedAt { get; set; }

        public int? QuantityPresented { get; set; }

        public DateTime? StaffReviewedAt { get; set; }

        public int? StaffReviewedByStoreStaffId { get; set; }

        // Unopened | LikeNew | Used | Damaged | SeverelyDamaged
        [StringLength(30)]
        public string ItemCondition { get; set; }

        // OriginalPackaging | OpenedPackaging | Missing |
        // Damaged | NotApplicable
        [StringLength(30)]
        public string PackagingCondition { get; set; }

        public bool? AccessoriesComplete { get; set; }

        public bool? ProductMatchesPurchase { get; set; }

        // Verified | PartiallyVerified | NotVerified
        [StringLength(30)]
        public string FaultVerification { get; set; }

        // ManufacturingDefect | ProductFailure | CustomerDamage |
        // NormalWear | WrongItem | UnableToDetermine | Other
        [StringLength(40)]
        public string FaultCause { get; set; }

        [StringLength(500)]
        public string StaffReviewNotes { get; set; }

        // Approve | Reject | Escalate
        [StringLength(20)]
        public string StaffRecommendation { get; set; }

        // Refund | Exchange
        [StringLength(20)]
        public string StaffRecommendedResolution { get; set; }

        [StringLength(255)]
        public string InspectionEvidencePath { get; set; }

        // Restock | Quarantine | ReturnToSupplier |
        // Dispose | AdminDecision
        [StringLength(30)]
        public string ItemDisposition { get; set; }

        // ============================================================
        // ADMIN FINAL DECISION
        // ============================================================

        public DateTime? DecidedAt { get; set; }

        public int? DecidedByAdminId { get; set; }

        [StringLength(500)]
        public string AdminDecisionReason { get; set; }

        // Refund | Exchange
        [StringLength(20)]
        public string ApprovedResolution { get; set; }

        // Existing field retained for compatibility.
        public bool WasRestocked { get; set; } = false;

        // ============================================================
        // REFUND
        // ============================================================

        public decimal? RefundAmount { get; set; }

        [StringLength(100)]
        public string PaystackRefundReference { get; set; }

        public DateTime? RefundProcessedAt { get; set; }

        // ============================================================
        // EXCHANGE PROCESS
        // ============================================================

        // Quantity reserved for the member after Admin approval.
        public int? ExchangeQuantityReserved { get; set; }

        public DateTime? ExchangeReservedAt { get; set; }

        // Optional deadline for the member to complete the exchange.
        public DateTime? ExchangeReservationExpiresAt { get; set; }
        // Set when reserved replacement stock is released back into
        // available inventory.
        public DateTime? ExchangeReservationReleasedAt { get; set; }

        [StringLength(500)]
        public string ExchangeCancellationReason { get; set; }

        // Additional amount the member must pay when the replacement
        // costs more than the returned-item credit.
        public decimal? AdditionalPaymentAmount { get; set; }

        [StringLength(100)]
        public string ExchangePaymentReference { get; set; }

        public DateTime? ExchangePaymentCompletedAt { get; set; }

        // Partial refund when the replacement is cheaper.
        public decimal? ExchangeRefundAmount { get; set; }

        [StringLength(100)]
        public string ExchangeRefundReference { get; set; }

        public DateTime? ExchangeCompletedAt { get; set; }

        public int? ExchangeCompletedByStoreStaffId { get; set; }

        // Optional member signature captured during the physical
        // exchange handover.
        [StringLength(255)]
        public string ExchangeSignaturePath { get; set; }

        // ============================================================
        // DISPLAY HELPERS
        // ============================================================

        [NotMapped]
        public bool IsExchange =>
            string.Equals(
                RequestedResolution,
                "Exchange",
                StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsRefund =>
            string.IsNullOrWhiteSpace(RequestedResolution) ||
            string.Equals(
                RequestedResolution,
                "Refund",
                StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsOpen =>
            ReturnRequestHelper.IsOpenStatus(Status);

        [NotMapped]
        public bool MemberMustPayDifference =>
            ExchangePriceDifference.HasValue &&
            ExchangePriceDifference.Value > 0;

        [NotMapped]
        public bool MemberReceivesDifferenceRefund =>
            ExchangePriceDifference.HasValue &&
            ExchangePriceDifference.Value < 0;
    }
}