using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace SwiftyS26.Models
{
    public class PurchaseOrder
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PurchaseOrderId { get; set; }

        [StringLength(20)]
        public string ReferenceNumber { get; set; } // PO-2026-4821

        [Required]
        [ForeignKey("Supplier")]
        public int SupplierId { get; set; }
        public virtual Supplier Supplier { get; set; }

        // Draft -> PendingAdminApproval -> Pending -> Confirmed ->
        // Delivered -> Inspected -> Completed
        // (PendingAdminApproval only appears if TotalValue exceeds the
        // threshold — see PurchaseOrderHelper.ApprovalThreshold)
        [Required]
        [StringLength(25)]
        public string Status { get; set; } = "Draft";

        public decimal TotalValue { get; set; }

        [Required]
        [StringLength(100)]
        public string OrderPurpose { get; set; }

        [StringLength(500)]
        public string InternalNotes { get; set; }

        [Required]
        [StringLength(15)]
        public string Priority { get; set; } = "Normal";

        public DateTime RequestedDeliveryDate { get; set; }

        // Whether this specific order required admin approval, computed
        // and stored at creation time — not recalculated later, so a
        // subsequent price/threshold change never rewrites history.
        public bool RequiredAdminApproval { get; set; }

        public int? CreatedByStoreStaffId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? ApprovedByAdminId { get; set; }
        public DateTime? ApprovedAt { get; set; }

        [StringLength(300)]
        public string AdminRejectionReason { get; set; }

        public DateTime? SentToSupplierAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? CompletedByAdminId { get; set; }

        // Secure QR used to locate this exact PO at goods receiving.
        [StringLength(100)]
        public string ReceivingQrToken { get; set; }

        [Column(TypeName = "nvarchar(MAX)")]
        public string ReceivingQrBase64 { get; set; }

        public DateTime? ReceivingQrGeneratedAt { get; set; }

        public virtual ICollection<PurchaseOrderItem> Items { get; set; }
    }

    public class PurchaseOrderItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PurchaseOrderItemId { get; set; }

        [Required]
        [ForeignKey("PurchaseOrder")]
        public int PurchaseOrderId { get; set; }

        public virtual PurchaseOrder PurchaseOrder { get; set; }

        [Required]
        [ForeignKey("Product")]
        public int ProductId { get; set; }

        public virtual Product Product { get; set; }

        [Required]
        [Range(1, int.MaxValue,
            ErrorMessage = "Quantity ordered must be at least 1.")]
        public int QuantityOrdered { get; set; }

        [Required]
        [Range(0.01, 999999999.0,
            ErrorMessage = "Unit cost must be greater than zero.")]
        public decimal UnitCost { get; set; }

        [NotMapped]
        public decimal LineTotal
        {
            get { return QuantityOrdered * UnitCost; }
        }
    }

    public class SupplierDelivery
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SupplierDeliveryId { get; set; }
        [Required, ForeignKey("PurchaseOrder")]
        public int PurchaseOrderId { get; set; }
        public virtual PurchaseOrder PurchaseOrder { get; set; }
        [Required, StringLength(25)]
        public string InspectionReference { get; set; }
        [Required, StringLength(80)]
        public string DeliveryNoteNumber { get; set; }
        public DateTime DeliveredAt { get; set; }
        [StringLength(100)] public string CourierDriver { get; set; }
        [StringLength(30)] public string VehicleRegistration { get; set; }
        [Range(1, 100000)] public int NumberOfPackages { get; set; }
        [Required, StringLength(20)] public string PackagingCondition { get; set; }
        [Required, StringLength(20)] public string SealCondition { get; set; }
        public bool SignsOfTampering { get; set; }
        public bool DeliveryNoteReceived { get; set; }
        public bool SupplierInvoiceReceived { get; set; }
        [StringLength(1000)] public string GeneralNotes { get; set; }
        [Required, StringLength(30)] public string OverallResult { get; set; }
        [StringLength(1000)] public string FinalInspectionNotes { get; set; }
        [Required, StringLength(20)] public string InspectionStatus { get; set; } = "Completed";
        public DateTime InspectedAt { get; set; } = DateTime.Now;
        public int? InspectedByStoreStaffId { get; set; }
        public int? InspectedByAdminId { get; set; }
        public virtual ICollection<DeliveryItem> Items { get; set; } = new HashSet<DeliveryItem>();
    }

    public class DeliveryItem
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DeliveryItemId { get; set; }
        [Required, ForeignKey("SupplierDelivery")]
        public int SupplierDeliveryId { get; set; }
        public virtual SupplierDelivery SupplierDelivery { get; set; }
        [Required, ForeignKey("Product")]
        public int ProductId { get; set; }
        public virtual Product Product { get; set; }
        public int ExpectedQty { get; set; }
        public int ReceivedQty { get; set; }
        public int GoodQty { get; set; }
        public int DamagedQty { get; set; }
        public int MissingQty { get; set; }
        public int ExcessQty { get; set; }
        [Required, StringLength(20)] public string Condition { get; set; }
        [StringLength(40)] public string DamageType { get; set; }
        [StringLength(20)] public string DamageSeverity { get; set; }
        [StringLength(1000)] public string InspectionNotes { get; set; }
        [StringLength(255)] public string EvidenceImagePath { get; set; }
        public bool IsFlagged { get; set; }
        [NotMapped] public int SellableQty => GoodQty;
        [NotMapped] public bool HasIssue => DamagedQty > 0 || MissingQty > 0 || ExcessQty > 0;
    }

    public class SupplierReturn
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SupplierReturnId { get; set; }
        [Required, StringLength(20)] public string ReferenceNumber { get; set; }
        [Required, ForeignKey("DeliveryItem")]
        public int DeliveryItemId { get; set; }
        public virtual DeliveryItem DeliveryItem { get; set; }
        [Range(1, int.MaxValue)] public int Quantity { get; set; }
        [Required, StringLength(30)] public string Reason { get; set; }
        [Required, StringLength(30)] public string RequestedResolution { get; set; }
        [StringLength(500)] public string Notes { get; set; }
        public decimal EstimatedReturnValue { get; set; }
        public bool RequiresAdminApproval { get; set; }
        [Required, StringLength(30)] public string Status { get; set; } = "Pending";
        public int? CreatedByStoreStaffId { get; set; }
        public int? CreatedByAdminId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? ApprovedByAdminId { get; set; }
        public DateTime? ApprovedAt { get; set; }
        [StringLength(300)] public string RejectionReason { get; set; }
        public DateTime? ResolvedAt { get; set; }
        [StringLength(20)] public string Resolution { get; set; }
        [StringLength(300)] public string ResolutionNotes { get; set; }
    }
}
