using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    // Immutable audit record of staff opening a locker that a member still
    // holds. This is a SECURITY EVENT, not a maintenance one — it lives in
    // its own table so it survives independently of whatever happens to the
    // fault record, and can be queried on its own for review.
    //
    // Deliberately has no "update" path anywhere in the application: these
    // rows are written once and never modified.
    public class LockerAccessOverride
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LockerAccessOverrideId { get; set; }

        [Required]
        [ForeignKey("Locker")]
        public int LockerId { get; set; }
        public virtual Locker Locker { get; set; }

        // The reservation that was open at the time — preserved even after
        // it ends, so the record still shows whose locker was opened.
        public int? ReservationId { get; set; }

        [Required]
        [ForeignKey("Member")]
        public int MemberId { get; set; }
        public virtual MemberForm Member { get; set; }

        // The fault that justified the override. Required — staff cannot
        // open a member's locker without a logged, triaged reason.
        [Required]
        [ForeignKey("LockerFault")]
        public int LockerFaultId { get; set; }
        public virtual LockerFault LockerFault { get; set; }

        [Required]
        public int PerformedByAdminId { get; set; }

        [Required]
        [StringLength(500)]
        public string Reason { get; set; }

        // Staff attests the member was informed/present, or that this was an
        // emergency. Recorded verbatim for accountability.
        [StringLength(200)]
        public string Acknowledgement { get; set; }

        public DateTime PerformedAt { get; set; } = DateTime.Now;

        [StringLength(45)]
        public string IpAddress { get; set; }

        // Whether the member notification went out successfully. The member
        // is ALWAYS notified — this records whether delivery worked, not
        // whether it was attempted.
        public bool MemberNotified { get; set; } = false;
    }
}