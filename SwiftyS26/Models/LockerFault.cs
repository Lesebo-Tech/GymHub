using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    // A single fault against a single locker. Multiple members reporting the
    // same problem attach as LockerFaultReport rows against ONE fault rather
    // than creating duplicates — three reports is a stronger signal about one
    // problem, not three problems.
    public class LockerFault
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LockerFaultId { get; set; }

        [Required]
        [ForeignKey("Locker")]
        public int LockerId { get; set; }
        public virtual Locker Locker { get; set; }

        [StringLength(20)]
        public string ReferenceNumber { get; set; } // LF-2026-1234

        // Jammed | LockFaulty | DoorDamaged | WontClose | Other
        [Required]
        [StringLength(30)]
        public string FaultType { get; set; }

        // ── SEVERITY: reported vs triaged ─────────────────────────────
        // ReportedSeverity is what the member said — ADVISORY only. It
        // drives how urgently admin is alerted, never system state.
        // Severity is what admin decided at triage — AUTHORITATIVE. This
        // split is what stops a member taking a locker offline at will.
        [StringLength(20)]
        public string ReportedSeverity { get; set; } // Minor | Major | Critical

        [StringLength(20)]
        public string Severity { get; set; }         // set at triage

        // Reported | Triaged | Assigned | InProgress | Resolved | Unrepairable
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Reported";

        public DateTime ReportedDate { get; set; } = DateTime.Now;

        // ── BELONGINGS ────────────────────────────────────────────────
        // Set when the fault traps a member's property inside. This is the
        // flag that unlocks the override-open path — without it, staff have
        // no legitimate reason to open someone else's locker.
        public bool BelongingsInside { get; set; } = false;

        // ── TRIAGE ────────────────────────────────────────────────────
        public DateTime? TriagedDate { get; set; }
        public int? TriagedByAdminId { get; set; }

        [StringLength(500)]
        public string TriageNotes { get; set; }

        // ── ASSIGNMENT ────────────────────────────────────────────────
        [ForeignKey("Technician")]
        public int? TechnicianId { get; set; }
        public virtual Technician Technician { get; set; }

        public DateTime? AssignedDate { get; set; }
        public DateTime? StartedDate { get; set; }

        // ── RESOLUTION ────────────────────────────────────────────────
        public DateTime? ResolvedDate { get; set; }

        [StringLength(1000)]
        public string ResolutionNotes { get; set; }

        [StringLength(500)]
        public string PartsUsed { get; set; }

        public decimal? RepairCost { get; set; }

        // Navigation
        public virtual ICollection<LockerFaultReport> Reports { get; set; }

        // ── COMPUTED ──────────────────────────────────────────────────
        [NotMapped]
        public bool IsOpen => Status != "Resolved" && Status != "Unrepairable";

        [NotMapped]
        public bool AwaitingTriage => Status == "Reported";

        // The severity that actually matters for display: admin's decision
        // once triaged, the member's claim before that.
        [NotMapped]
        public string EffectiveSeverity => Severity ?? ReportedSeverity ?? "Minor";

        [NotMapped]
        public string FaultTypeDisplay
        {
            get
            {
                switch (FaultType)
                {
                    case "Jammed": return "Jammed / won't open";
                    case "LockFaulty": return "Lock or PIN pad faulty";
                    case "DoorDamaged": return "Door damaged";
                    case "WontClose": return "Won't stay closed";
                    default: return "Other";
                }
            }
        }
    }
}