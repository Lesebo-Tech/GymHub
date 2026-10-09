using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace SwiftyS26.Models
{
    public class FreezeRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int FreezeRequestId { get; set; }

        // ── Foreign key to member ──
        [Required]
        public int MemberId { get; set; }

        [ForeignKey("MemberId")]
        public virtual MemberForm Member { get; set; }

        [Required]
        [StringLength(20)]
        public string MemberNumber { get; set; }

        // ── Requested dates ──
        [Required]
        public DateTime FreezeStart { get; set; }

        [Required]
        public DateTime FreezeEnd { get; set; }

        // ── Computed freeze duration in days ──
        [NotMapped]
        public int FreezeDurationDays =>
            Math.Max(1, (int)(FreezeEnd.Date - FreezeStart.Date).TotalDays);

        // ── Reason provided by member ──
        [Required]
        [StringLength(500)]
        public string Reason { get; set; }

        // ── Status: Pending | Approved | Rejected | Completed ──
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        // ── Timestamps ──
        public DateTime RequestedAt { get; set; } = DateTime.Now;
        public DateTime? ProcessedAt { get; set; }

        // ── Admin notes on approval/rejection ──
        [StringLength(1000)]
        public string AdminNotes { get; set; }

        // ── Original membership expiry before freeze (saved so we can extend it) ──
        public DateTime? OriginalExpiry { get; set; }

        // ── New membership expiry after extension ──
        public DateTime? ExtendedExpiry { get; set; }

        // ── True while the freeze window is currently active ──
        [NotMapped]
        public bool IsCurrentlyFrozen =>
            Status == "Approved" &&
            DateTime.Today >= FreezeStart.Date &&
            DateTime.Today <= FreezeEnd.Date;

        // ── True once the freeze window has fully passed ──
        [NotMapped]
        public bool IsCompleted =>
            Status == "Approved" && DateTime.Today > FreezeEnd.Date;

        // ── Display helpers ──
        [NotMapped]
        public string FreezeStartDisplay => FreezeStart.ToString("dd MMM yyyy");

        [NotMapped]
        public string FreezeEndDisplay => FreezeEnd.ToString("dd MMM yyyy");

        [NotMapped]
        public string RequestedAtDisplay => RequestedAt.ToString("dd MMM yyyy");

        [NotMapped]
        public string ProcessedAtDisplay =>
            ProcessedAt.HasValue ? ProcessedAt.Value.ToString("dd MMM yyyy") : "—";
    }
}