using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    // ============================================================
    // UC14 — CafeProduct
    // Products available for redemption at the GymHub café
    // Managed by admin, displayed to members in wallet section
    // ============================================================
    public class CafeProduct
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CafeProductId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(300)]
        public string Description { get; set; }

        // Emoji icon displayed on the product card e.g. "🥤", "🍌", "☕"
        [StringLength(10)]
        public string Emoji { get; set; }

        // Image uploaded by admin (stored in ~/Content/CafeImages/)
        [StringLength(200)]
        public string ImagePath { get; set; }

        // Category: Smoothie | Shake | Snack | Supplement | Meal | Drink
        [Required]
        [StringLength(50)]
        public string Category { get; set; }

        // How many loyalty points it costs to redeem
        [Required]
        public int PointsCost { get; set; }

        // Rand value equivalent (for admin reporting / sales reconciliation)
        public decimal RandValue { get; set; }

        // Whether the product is visible to members
        public bool IsActive { get; set; } = true;

        // Whether it is currently in stock at the café
        public bool InStock { get; set; } = true;

        // Optional: limit redemptions per member per day (0 = unlimited)
        public int DailyRedemptionLimit { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    // ============================================================
    // UC14 — CafeRedemption
    // Logged every time a member redeems points for a café product
    // ============================================================
    public class CafeRedemption
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CafeRedemptionId { get; set; }

        [Required]
        public int MemberId { get; set; }

        [Required]
        public int CafeProductId { get; set; }

        // Points deducted from the member's loyalty account
        public int PointsSpent { get; set; }

        // Rand equivalent (for admin sales reconciliation)
        public decimal RandValue { get; set; }

        // Unique 6-digit code member shows at the café counter
        [StringLength(10)]
        public string CollectionCode { get; set; }

        // Pending | Collected | Expired | Cancelled
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        public DateTime RedeemedAt { get; set; } = DateTime.Now;

        // Set when staff marks it as collected
        public DateTime? CollectedAt { get; set; }

        // Admin notes (e.g. "collected by cashier Thabo")
        [StringLength(200)]
        public string Notes { get; set; }

        [ForeignKey("MemberId")]
        public virtual MemberForm Member { get; set; }

        [ForeignKey("CafeProductId")]
        public virtual CafeProduct Product { get; set; }
    }
}