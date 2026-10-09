using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class LoyaltyAccount
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LoyaltyAccountId { get; set; }

        [Required]
        public int MemberId { get; set; }

        [ForeignKey("MemberId")]
        public virtual MemberForm Member { get; set; }

        // ── Points balance ──
        public int CurrentPoints { get; set; } = 0;
        public int TotalPointsEarned { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        // ── Computed ──
        [NotMapped]
        public decimal PointsRandValue => CurrentPoints * 0.10m;

        [NotMapped]
        public string PointsRandDisplay => $"R{PointsRandValue:F2}";
    }
}