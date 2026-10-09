using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class WeeklyProgressLog
    {
        [Key]
        public int WeeklyProgressLogId { get; set; }

        // Foreign key to your Member table
        [Required]
        public int MemberId { get; set; }

        // Which week of their program (1, 2, 3 …)
        [Required]
        public int WeekNumber { get; set; }

        // When the member actually submitted this entry
        [Required]
        public DateTime LoggedDate { get; set; }

        // The four fields from the member's "Log This Week" form
        [Required]
        public decimal WeightKg { get; set; }
        [Required]
        public decimal? BodyFatPercent { get; set; }
        [Required]
        public int WorkoutsThisWeek { get; set; }

        [MaxLength(500)]
        public string Notes { get; set; }

        // Navigation property (optional but useful)
        [ForeignKey("MemberId")]
        public virtual MemberForm Member { get; set; }
    }
}