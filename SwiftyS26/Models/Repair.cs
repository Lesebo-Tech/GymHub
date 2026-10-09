using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class Repair
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RepairId { get; set; }

        [Required]
        [ForeignKey("Equipment")]
        public int EquipmentId { get; set; }
        public virtual Equipment Equipment { get; set; }

        // Links back to the inspection that flagged the fault, if there was one —
        // nullable because a fault can also be reported directly (e.g. member complaint)
        [ForeignKey("Inspection")]
        public int? InspectionId { get; set; }
        public virtual Inspection Inspection { get; set; }

        [ForeignKey("Technician")]
        public int? TechnicianId { get; set; }
        public virtual Technician Technician { get; set; }

        public DateTime ReportedDate { get; set; } = DateTime.Now;

        [Required]
        [StringLength(500)]
        public string FaultDescription { get; set; }

        [StringLength(20)]
        public string RepairStatus { get; set; } = "Pending"; // Pending, InProgress, Resolved, Unrepairable

        [StringLength(1000)]
        public string DiagnosisNotes { get; set; }

        [StringLength(500)]
        public string PartsUsed { get; set; }

        public decimal? RepairCost { get; set; }

        public DateTime? ResolvedDate { get; set; }

        [NotMapped]
        public bool IsOpen => RepairStatus == "Pending" || RepairStatus == "InProgress";
        // ── FAULT SOURCE ──────────────────────────────────────────────
        [StringLength(20)]
        public string ReportedBySource { get; set; } = "Inspection"; // "Inspection", "Admin", "Member"

        public int? ReportedByMemberId { get; set; }
        [ForeignKey("ReportedByMemberId")]
        public virtual MemberForm ReportedByMember { get; set; }

        [StringLength(50)]
        public string Category { get; set; }          // e.g. "Mechanical" — from FaultCategoryHelper

        [StringLength(10)]
        public string Severity { get; set; }           // "Minor" or "Critical"

        [StringLength(20)]
        public string ReferenceNumber { get; set; }     // e.g. RF-2026-0001, shown to staff on submit

        [StringLength(255)]
        public string PhotoPath { get; set; }           // relative path, e.g. /Uploads/Repairs/xxx.jpg
        [ForeignKey("Vendor")]
        public int? VendorId { get; set; }
        public virtual Vendor Vendor { get; set; }


        //added
        public int? MaintenanceId { get; set; }
        [ForeignKey("MaintenanceId")]
        public virtual Maintenance Maintenance { get; set; }
        // ── ADD THIS to Repair.cs ──────────────────────────────────────
        // Captures why an admin rejected an inspection-passed repair,
        // separate from DiagnosisNotes so the rejection reason doesn't
        // get overwritten if the technician later re-completes the repair.

        [StringLength(500)]
        public string ApprovalRejectionReason { get; set; }


        public int? VerificationTechnicianId { get; set; }
        [ForeignKey("VerificationTechnicianId")]
        public virtual Technician VerificationTechnician { get; set; }

        public DateTime? VerificationAssignedDate { get; set; }

        [NotMapped]
        public string AssigneeDisplay => Vendor != null ? $"{Vendor.CompanyName} (Vendor)"
            : Technician != null ? Technician.FullName
            : "Unassigned";
    }
}