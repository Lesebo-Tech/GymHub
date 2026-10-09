using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    // One person's report against a fault. The first report creates the
    // LockerFault; subsequent reports of the same problem attach here.
    // Keeping reporters as rows means every one of them gets notified when
    // the fault is resolved — not just whoever happened to report first.
    public class LockerFaultReport
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LockerFaultReportId { get; set; }

        [Required]
        [ForeignKey("LockerFault")]
        public int LockerFaultId { get; set; }
        public virtual LockerFault LockerFault { get; set; }

        // Null when logged by staff/admin rather than a member
        [ForeignKey("Member")]
        public int? MemberId { get; set; }
        public virtual MemberForm Member { get; set; }

        // "Member" | "Admin"
        [Required]
        [StringLength(20)]
        public string ReportedBySource { get; set; }

        [Required]
        [StringLength(1000)]
        public string Description { get; set; }

        [StringLength(20)]
        public string ReportedSeverity { get; set; }

        [StringLength(255)]
        public string PhotoPath { get; set; }

        // Was this reporter holding the locker at the time they reported it?
        // Captured per-report because it's the strongest signal that
        // belongings are trapped.
        public bool WasHoldingLocker { get; set; } = false;

        public DateTime ReportedDate { get; set; } = DateTime.Now;
    }
}