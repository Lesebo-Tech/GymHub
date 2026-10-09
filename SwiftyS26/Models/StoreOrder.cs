using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class StoreOrder
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int StoreOrderId { get; set; }

        [Required]
        [ForeignKey("Member")]
        public int MemberId { get; set; }

        public virtual MemberForm Member { get; set; }

        [Required]
        [StringLength(20)]
        public string ReferenceNumber { get; set; }

        public decimal Subtotal { get; set; }

        public decimal DiscountAmount { get; set; } = 0m;

        public decimal FinalTotal { get; set; }

        [StringLength(100)]
        public string PaystackReference { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "PendingPayment";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? PaidAt { get; set; }

        public DateTime? ReadyAt { get; set; }

        public DateTime? PickupDeadline { get; set; }

        public DateTime? CollectedAt { get; set; }

        [StringLength(255)]
        public string PickupSignaturePath { get; set; }

        public int? CollectedByStaffId { get; set; }

        public DateTime? FirstReminderSentAt { get; set; }

        public DateTime? FinalReminderSentAt { get; set; }

        // Customer-facing QR used when the member collects the order.
        [Column(TypeName = "nvarchar(MAX)")]
        public string OrderQrBase64 { get; set; }

        // One-time token used by the internal Mark Ready QR code.
        [StringLength(100)]
        public string ReadyQrToken { get; set; }

        // Records when the current internal QR token was generated.
        public DateTime? ReadyQrGeneratedAt { get; set; }

        // Prevents a used internal QR code from being reused.
        public DateTime? ReadyQrUsedAt { get; set; }

        // Professional uncollected-order workflow.
        public DateTime? UncollectedAt { get; set; }

        public DateTime? StockReleasedAt { get; set; }

        // True while units are reserved for future pickup.
        public bool IsPickupStockReserved { get; set; } = true;

        [StringLength(40)]
        public string UncollectedResolutionStatus { get; set; }

        [StringLength(500)]
        public string UncollectedResolutionNotes { get; set; }

        [StringLength(80)]
        public string UncollectedResolvedBy { get; set; }

        public DateTime? UncollectedResolvedAt { get; set; }

        public int DeadlineExtensionCount { get; set; }

        public DateTime? PreviousPickupDeadline { get; set; }

        public decimal? UncollectedRefundAmount { get; set; }

        [StringLength(100)]
        public string UncollectedRefundReference { get; set; }

        public DateTime? UncollectedRefundedAt { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; }

        public virtual ICollection<StoreOrderItem> Items { get; set; }

        public virtual ICollection<StoreOrderCollectionEvent> CollectionEvents { get; set; }

        public StoreOrder()
        {
            Items = new HashSet<StoreOrderItem>();
            CollectionEvents = new HashSet<StoreOrderCollectionEvent>();
        }

        [NotMapped]
        public bool WasDiscounted
        {
            get { return DiscountAmount > 0m; }
        }

        [NotMapped]
        public decimal MaximumUncollectedRefund
        {
            get
            {
                return Math.Max(
                    FinalTotal - (UncollectedRefundAmount ?? 0m),
                    0m);
            }
        }

        [NotMapped]
        public bool HasActiveReadyQr
        {
            get
            {
                return !string.IsNullOrWhiteSpace(ReadyQrToken) &&
                       ReadyQrGeneratedAt.HasValue &&
                       !ReadyQrUsedAt.HasValue &&
                       (Status == "Paid" || Status == "Preparing");
            }
        }
    }
}