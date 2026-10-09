using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class Locker
    {
        [Key]
        public int LockerId { get; set; }

        [Required]
        [StringLength(10)]
        public string LockerNumber { get; set; }

        // Optional: group lockers by area, e.g. "Men's Change Room"
        [StringLength(100)]
        public string Location { get; set; }

        // ── RESERVATION STATE ─────────────────────────────────────────
        // True = currently held by a member. This is deliberately kept
        // SEPARATE from OperationalStatus below: a locker can be both
        // occupied AND out of service at the same time (the jammed-door-
        // with-belongings-inside case). Collapsing them into one flag
        // would make that state unrepresentable.
        public bool IsOccupied { get; set; } = false;

        // ── OPERATIONAL STATE ─────────────────────────────────────────
        // InService | ReportedFaulty | OutOfService | UnderRepair | Retired
        //
        // ReportedFaulty is the important one: it means "a fault has been
        // logged and is awaiting triage" — visible to admin, but NOT yet
        // blocking members. Only an admin's triage decision takes a locker
        // out of circulation, which prevents any member from disabling a
        // locker with a single unverified report.
        [Required]
        [StringLength(20)]
        public string OperationalStatus { get; set; } = "InService";

        [Required]
        [StringLength(10)]
        public string Gender { get; set; } // "Male" or "Female"

        // Navigation
        public virtual ICollection<LockerReservation> Reservations { get; set; }
        public virtual ICollection<LockerFault> Faults { get; set; }

        // ── COMPUTED ──────────────────────────────────────────────────

        // A locker is only reservable when it's both free AND healthy.
        [NotMapped]
        public bool IsReservable => !IsOccupied && OperationalStatus == "InService";

        [NotMapped]
        public string OperationalStatusDisplay
        {
            get
            {
                switch (OperationalStatus)
                {
                    case "ReportedFaulty": return "Reported Faulty";
                    case "OutOfService": return "Out of Service";
                    case "UnderRepair": return "Under Repair";
                    case "Retired": return "Retired";
                    default: return "In Service";
                }
            }
        }
    }
}