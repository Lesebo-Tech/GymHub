using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class WalletTransaction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int WalletTransactionId { get; set; }

        [Required]
        public int MemberId { get; set; }

        [ForeignKey("MemberId")]
        public virtual MemberForm Member { get; set; }

        // ── Type: TopUp | Purchase | PointsConversion ──
        [Required]
        [StringLength(30)]
        public string TransactionType { get; set; }

        public decimal Amount { get; set; }

        [StringLength(200)]
        public string Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}