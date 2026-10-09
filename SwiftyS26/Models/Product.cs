using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace SwiftyS26.Models
{
    public class Product
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProductId { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; }

        [StringLength(1000)]
        public string Description { get; set; }

        [Required]
        [StringLength(30)]
        public string Category { get; set; }

        [StringLength(255)]
        public string ImagePath { get; set; }

        [Required]
        public decimal NormalPrice { get; set; }

        public int QuantityOnHand { get; set; } = 0;
        public int ReorderLevel { get; set; } = 10;
        public DateTime? ExpiryDate { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Permanent identity used during supplier-delivery inspection.
        [StringLength(30)]
        public string SKU { get; set; }

        [StringLength(100)]
        public string ProductQrToken { get; set; }

        [Column(TypeName = "nvarchar(MAX)")]
        public string ProductQrBase64 { get; set; }

        public DateTime? ProductQrGeneratedAt { get; set; }
        // ── ADD to Product.cs ─────────────────────────────────────────
        public virtual ICollection<Promotion> Promotions { get; set; }

        [NotMapped]
        public Promotion ActivePromotion =>
            Promotions?.FirstOrDefault(p => p.IsCurrentlyRunning);

        [NotMapped]
        public decimal DisplayPrice => ActivePromotion?.DiscountedPrice ?? NormalPrice;

        [NotMapped]
        public bool IsOnPromotion => ActivePromotion != null;

        [NotMapped]
        public string StockStatus
        {
            get
            {
                if (QuantityOnHand <= 0) return "OutOfStock";
                if (ExpiryDate.HasValue && ExpiryDate.Value <= DateTime.Today.AddDays(14)) return "NearExpiry";
                if (QuantityOnHand <= ReorderLevel) return "LowStock";
                return "InStock";
            }
        }

        [NotMapped]
        public string StockStatusDisplay
        {
            get
            {
                switch (StockStatus)
                {
                    case "OutOfStock": return "Out of Stock";
                    case "NearExpiry": return "Near Expiry";
                    case "LowStock": return "Low Stock";
                    default: return "In Stock";
                }
            }
        }

        [NotMapped]
        public bool IsPurchasable => IsActive && QuantityOnHand > 0;
    }
}
