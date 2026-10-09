using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace SwiftyS26.Models
{
    public class RefundRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RefundRequestId { get; set; }

        public int MemberId { get; set; }

        [StringLength(20)]
        public string MemberNumber { get; set; }

        
        [StringLength(50)]
        public string RefundType { get; set; }

        public decimal RefundAmount { get; set; }

        
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        public DateTime RequestedAt { get; set; } = DateTime.Now;

        public DateTime? ProcessedAt { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }

        [ForeignKey("MemberId")]
        public virtual MemberForm Member { get; set; }
    }
}