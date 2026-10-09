using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;
using System.Web.Mvc;

namespace SwiftyS26.Models
{
    public class PurchaseOrderCreateViewModel
    {
        [Required] public int SupplierId { get; set; }
        [Required, StringLength(100)] public string OrderPurpose { get; set; }
        [Required, DataType(DataType.Date)] public DateTime RequestedDeliveryDate { get; set; }
        [Required] public string Priority { get; set; } = "Normal";
        [StringLength(500)] public string InternalNotes { get; set; }
        public List<PurchaseOrderLineInput> Items { get; set; } = new List<PurchaseOrderLineInput>();
        public IEnumerable<SelectListItem> Suppliers { get; set; }
        public IEnumerable<Product> Products { get; set; }
    }

    public class PurchaseOrderLineInput
    {
        [Required] public int ProductId { get; set; }
        [Range(1, 100000)] public int Quantity { get; set; }
        // Use the numeric RangeAttribute constructor. The typeof(decimal)
        // overload parses string limits with the current server culture and
        // can throw FormatException for "0.01" on comma-decimal cultures.
        [Range(0.01, 999999999.0, ErrorMessage = "Unit cost must be greater than zero.")]
        public decimal UnitCost { get; set; }
    }

    public class DeliveryInspectionViewModel
    {
        public int PurchaseOrderId { get; set; }
        public PurchaseOrder PurchaseOrder { get; set; }
        [Required, StringLength(80)] public string DeliveryNoteNumber { get; set; }
        [Required] public DateTime DeliveredAt { get; set; }
        [StringLength(100)] public string CourierDriver { get; set; }
        [StringLength(30)] public string VehicleRegistration { get; set; }
        [Range(1, 100000)] public int NumberOfPackages { get; set; }
        [Required] public string PackagingCondition { get; set; }
        [Required] public string SealCondition { get; set; }
        public bool SignsOfTampering { get; set; }
        public bool DeliveryNoteReceived { get; set; }
        public bool SupplierInvoiceReceived { get; set; }
        [StringLength(1000)] public string GeneralNotes { get; set; }
        [Required] public string OverallResult { get; set; }
        [StringLength(1000)] public string FinalInspectionNotes { get; set; }
        public List<DeliveryInspectionLineInput> Items { get; set; } = new List<DeliveryInspectionLineInput>();
    }

    public class DeliveryInspectionLineInput
    {
        public int PurchaseOrderItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int ExpectedQty { get; set; }
        [Range(0, 100000)] public int ReceivedQty { get; set; }
        [Range(0, 100000)] public int DamagedQty { get; set; }
        [Required] public string Condition { get; set; }
        public string DamageType { get; set; }
        public string DamageSeverity { get; set; }
        [StringLength(1000)] public string InspectionNotes { get; set; }
        public HttpPostedFileBase EvidenceImage { get; set; }
    }

    public class SupplierReturnCreateViewModel
    {
        public int DeliveryItemId { get; set; }
        public DeliveryItem DeliveryItem { get; set; }
        [Range(1, 100000)] public int Quantity { get; set; }
        [Required] public string Reason { get; set; }
        [Required] public string RequestedResolution { get; set; }
        [StringLength(500)] public string Notes { get; set; }
    }
}
