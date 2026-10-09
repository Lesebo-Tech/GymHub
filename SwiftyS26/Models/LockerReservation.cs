using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class LockerReservation
    {
        [Key]
        public int ReservationId { get; set; }

        [Required]
        [ForeignKey("Locker")]
        public int LockerId { get; set; }
        public virtual Locker Locker { get; set; }

        [Required]
        [ForeignKey("Member")]
        public int MemberId { get; set; }
        public virtual MemberForm Member { get; set; }

        // One-way BCrypt hash of the member's PIN. Never store the raw PIN.
        [StringLength(200)]
        public string AccessCodeHash { get; set; }

        public DateTime ReservedAt { get; set; } = DateTime.Now;

        // Null while reservation is active; set when member ends it
        public DateTime? EndedAt { get; set; }

        // True while the reservation is active (locker occupied by this member)
        public bool IsActive { get; set; } = true;
        public int FailedAttempts { get; set; } = 0;
        public DateTime? LockedUntil { get; set; }

        // True once the member has successfully entered their PIN at least once this reservation
        public bool HasBeenUnlocked { get; set; } = false;
        // How many times this reservation has hit the 5-attempt lockout.
        // Used to notify admin only on repeat lockouts (a first lockout is
        // usually just a forgetful member; repeated ones may signal tampering).
        public int LockoutCount { get; set; } = 0;

        // ── FORCE RELEASE (admin) ──────────────────────────────────────
        // Set when an admin ends the reservation on the member's behalf,
        // rather than the member ending it themselves.
        public bool WasForceReleased { get; set; } = false;

        [StringLength(300)]
        public string ForceReleaseReason { get; set; }
    }
}
