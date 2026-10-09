using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class PointsTransaction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PointsTransactionId { get; set; }

        [Required]
        public int MemberId { get; set; }

        [ForeignKey("MemberId")]
        public virtual MemberForm Member { get; set; }

        // ── Type: Earned | Bonus | Converted ──
        [Required]
        [StringLength(20)]
        public string TransactionType { get; set; }

        public int Points { get; set; }

        [StringLength(200)]
        public string Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}