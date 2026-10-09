using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class Promotion
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PromotionId { get; set; }

        [Required]
        [ForeignKey("Product")]
        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        [Required]
        [Range(1, 90)]
        public decimal DiscountPercent { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        public int CreatedByStoreStaffId { get; set; }
        public bool CreatedByAdmin { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ── COMPUTED ──────────────────────────────────────────────────
        [NotMapped]
        public bool IsCurrentlyRunning =>
            IsActive && DateTime.Today >= StartDate.Date && DateTime.Today <= EndDate.Date;

        [NotMapped]
        public decimal DiscountedPrice =>
            Product != null ? Math.Round(Product.NormalPrice * (1 - DiscountPercent / 100m), 2) : 0;
    }
}