using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class RepairAssignment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RepairAssignmentId { get; set; }

        [Required]
        [ForeignKey("Repair")]
        public int RepairId { get; set; }
        public virtual Repair Repair { get; set; }

        // "Technician" or "Vendor" — only one of TechnicianId/VendorId will be populated,
        // matching whichever this value says
        [Required]
        [StringLength(20)]
        public string AssigneeType { get; set; }

        [ForeignKey("Technician")]
        public int? TechnicianId { get; set; }
        public virtual Technician Technician { get; set; }

        [ForeignKey("Vendor")]
        public int? VendorId { get; set; }
        public virtual Vendor Vendor { get; set; }

        // Constant = 1 in the current single-admin setup, mirrors AdminId in AdminController
        public int AssignedByAdminId { get; set; }

        public DateTime AssignedDate { get; set; } = DateTime.Now;
        public DateTime? DueDate { get; set; }

        [StringLength(20)]
        public string Priority { get; set; } // Low, Medium, High, Urgent

        [StringLength(500)]
        public string AssignmentNotes { get; set; }

        // Assigned, Accepted, Declined, Escalated, Completed, Superseded
        [Required]
        [StringLength(20)]
        public string AssignmentStatus { get; set; } = "Assigned";

        public DateTime? AcceptedDate { get; set; }
        public DateTime? StartedDate { get; set; }
        public DateTime? CompletedDate { get; set; }

        [StringLength(500)]
        public string DeclineReason { get; set; }

        [StringLength(500)]
        public string EscalationReason { get; set; }

        [NotMapped]
        public bool IsActive => AssignmentStatus == "Assigned" || AssignmentStatus == "Accepted";
    }
}