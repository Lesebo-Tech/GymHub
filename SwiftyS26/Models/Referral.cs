using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class Referral
    {
        // PRIMARY KEY
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ReferralId { get; set; }

        // ── REFERRER (the existing member who shared the link) ─────────

        [Required]
        [ForeignKey("Referrer")]
        public int ReferrerId { get; set; }

        public virtual MemberForm Referrer { get; set; }

        // Snapshot of the code at the time it was used — referrer's code could
        // theoretically change later, this keeps history accurate.
        [Required]
        [StringLength(20)]
        public string ReferralCodeUsed { get; set; }

        // ── REFERRED FRIEND (the new member who registered) ────────────

        [Required]
        [ForeignKey("ReferredMember")]
        public int ReferredMemberId { get; set; }

        public virtual MemberForm ReferredMember { get; set; }

        // ── REWARD ───────────────────────────────────────────────────

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Rewarded"; // set at creation — see controller notes

        public int RewardPoints { get; set; }

        public DateTime RegisteredAt { get; set; }

        public DateTime? RewardedAt { get; set; }

        // ── COMPUTED PROPERTIES (NOT MAPPED) ─────────────────────────

        [NotMapped]
        public string RegisteredAtDisplay => RegisteredAt.ToString("dd MMM yyyy");
    }
}