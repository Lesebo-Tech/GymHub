using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class SupplierProduct
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SupplierProductId { get; set; }

        [Required, ForeignKey("Supplier")]
        public int SupplierId { get; set; }
        public virtual Supplier Supplier { get; set; }

        [Required, ForeignKey("Product")]
        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        [Range(0.01, 999999999.0, ErrorMessage = "Unit cost must be greater than zero.")]
        public decimal CurrentUnitCost { get; set; }

        [Range(1, 365)]
        public int EstimatedDeliveryDays { get; set; } = 7;

        public bool IsPreferredSupplier { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime PriceUpdatedAt { get; set; } = DateTime.Now;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
