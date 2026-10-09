using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class Maintenance
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MaintenanceId { get; set; }

        [Required]
        [ForeignKey("Equipment")]
        public int EquipmentId { get; set; }
        public virtual Equipment Equipment { get; set; }

        // Whoever actually performed the maintenance — set from the
        // logged-in technician on self-service, or picked by admin
        [ForeignKey("Technician")]
        public int? TechnicianId { get; set; }
        public virtual Technician Technician { get; set; }

        public DateTime MaintenanceDate { get; set; } = DateTime.Now;

        [Required]
        [StringLength(20)]
        public string Result { get; set; } // "Completed" or "IssueFound"

        [StringLength(1000)]
        public string TasksPerformed { get; set; }

        [StringLength(500)]
        public string PartsReplaced { get; set; }

        public decimal? Cost { get; set; }

        [NotMapped]
        public bool IssueFound => Result == "IssueFound";
    }
}