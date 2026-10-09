using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class Payment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PaymentId { get; set; }

        public int MemberId { get; set; }

        // Paystack reference — e.g. "GH-2026-00001-1743000000"
        [StringLength(100)]
        public string Reference { get; set; }

        // Paystack transaction ID returned after verification
        [StringLength(100)]
        public string TransactionId { get; set; }

        public DateTime PaymentDate { get; set; }

        [StringLength(200)]
        public string Description { get; set; }

        // "card", "eft", etc.
        [StringLength(50)]
        public string Method { get; set; }

        public decimal Amount { get; set; }

        // "Pending" → "Success" → "Failed"
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        [ForeignKey("MemberId")]
        public virtual MemberForm Member { get; set; }
    }
}