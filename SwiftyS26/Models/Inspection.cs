using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class Inspection
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int InspectionId { get; set; }

        [Required]
        [ForeignKey("Equipment")]
        public int EquipmentId { get; set; }
        public virtual Equipment Equipment { get; set; }

        // Nullable — technician may be removed/reassigned later without losing inspection history
        [ForeignKey("Technician")]
        public int? TechnicianId { get; set; }
        public virtual Technician Technician { get; set; }

        public DateTime InspectionDate { get; set; } = DateTime.Now;

        [Required]
        [StringLength(10)]
        public string Result { get; set; }   // "Pass" or "Fail"

        [StringLength(1000)]
        public string Findings { get; set; }   // what the technician observed

        // Only relevant when Result == "Fail" — feeds into the future Fault Repair step
        [StringLength(500)]
        public string ActionTaken { get; set; }

        [NotMapped]
        public bool Passed => Result == "Pass";
        // ── POST-REPAIR LINK ─────────────────────────────────────────
        // null = routine scheduled inspection, populated = inspection
        // triggered by a completed repair
        public int? RepairId { get; set; }
        [ForeignKey("RepairId")]
        public virtual Repair Repair { get; set; }

        [StringLength(20)]
        public string InspectionType { get; set; } = "Routine"; // "Routine" or "PostRepair"
    }
}