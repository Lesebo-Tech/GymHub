using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class RepairRequest
    {
        // ── PRIMARY KEY ──────────────────────────────────────────────
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RepairRequestId { get; set; }

        [StringLength(20)]
        public string ReferenceNumber { get; set; }   // e.g. RF-00871, generate on insert

        // ── LINK TO EQUIPMENT ─────────────────────────────────────────
        [Required]
        [StringLength(20)]
        public string EquipmentCode { get; set; }      // FK-style link to Equipment.EquipmentCode

        // ── FAULT DETAILS (submitted by staff via mobile app) ─────────
        [Required]
        [StringLength(50)]
        public string Category { get; set; }           // "Mechanical", "Electrical", "Display / Console", "Safety hazard"

        [Required]
        [StringLength(20)]
        public string Severity { get; set; }            // "Critical" or "Minor"

        [Required]
        [StringLength(1000)]
        public string Description { get; set; }

        [StringLength(500)]
        public string PhotoUrl { get; set; }             // set after upload, nullable for now

        // ── LIFECYCLE STATUS ────────────────────────────────────────────
        [StringLength(20)]
        public string Status { get; set; } = "Logged";   // Logged, Assigned, InProgress, Resolved


        // ── ASSIGNMENT (filled in later by web admin, UC04) ─────────────
        public int? AssignedTechnicianId { get; set; }

        public DateTime? AssignedAt { get; set; }

        // ── TIMESTAMPS ────────────────────────────────────────────────
        public DateTime ReportedAt { get; set; } = DateTime.Now;

        public DateTime? ResolvedAt { get; set; }


        // ── OPTIONAL: who reported it, if you add this later ────────────
        [StringLength(100)]
        public string ReportedBy { get; set; }           // can stay null since no login yet
    }
}