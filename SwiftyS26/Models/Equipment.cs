using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class Equipment
    {
        // ── PRIMARY KEY ──────────────────────────────────────────────
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int EquipmentId { get; set; }

        [StringLength(20)]
        public string EquipmentCode { get; set; }   // e.g. EQ-0001

        // ── QR CODE — scanned by technicians for quick lookup ───────
        [Column(TypeName = "nvarchar(MAX)")]
        public string QrCodeBase64 { get; set; }

        // ── IDENTITY ─────────────────────────────────────────────────
        [Required]
        [StringLength(100)]
        public string Name { get; set; }               // e.g. "Treadmill"

        [StringLength(50)]
        public string Category { get; set; }           // Cardio, Strength Machines, Free Weights, Functional Training, Other

        [StringLength(80)]
        public string Brand { get; set; }

        [StringLength(80)]
        public string Model { get; set; }

        [StringLength(100)]
        public string SerialNumber { get; set; }

        // ── PURCHASE & VENDOR ────────────────────────────────────────
        public DateTime? PurchaseDate { get; set; }

        public decimal? PurchaseCost { get; set; }

        [StringLength(100)]
        public string VendorName { get; set; }

        [StringLength(100)]
        public string VendorContact { get; set; }

        public DateTime? WarrantyExpiryDate { get; set; }

        // ── LOCATION ─────────────────────────────────────────────────
        [StringLength(50)]
        public string GymArea { get; set; }             // Cardio Zone, Free Weights Area, etc.

        // ── LIFECYCLE STATUS ─────────────────────────────────────────
        [StringLength(20)]
        public string Status { get; set; } = "Available";  // Available, OutOfService, UnderMaintenance, Retired

        public DateTime RegisteredAt { get; set; } = DateTime.Now;

        public DateTime? RetiredAt { get; set; }

        [StringLength(500)]
        public string RetirementReason { get; set; }


        public int? PendingInspectionTechnicianId { get; set; }
        [ForeignKey("PendingInspectionTechnicianId")]
        public virtual Technician PendingInspectionTechnician { get; set; }

        public DateTime? PendingInspectionAssignedDate { get; set; }

        public int? PendingMaintenanceTechnicianId { get; set; }
        [ForeignKey("PendingMaintenanceTechnicianId")]
        public virtual Technician PendingMaintenanceTechnician { get; set; }

        public DateTime? PendingMaintenanceAssignedDate { get; set; }



        // ── INSPECTION TRACKING ──────────────────────────────────────
        public DateTime? LastInspectionDate { get; set; }

        public int InspectionIntervalDays { get; set; } = 30;

        public DateTime? NextInspectionDueDate { get; set; }

        // ── PREVENTIVE MAINTENANCE TRACKING ──────────────────────────
        public DateTime? LastMaintenanceDate { get; set; }

        public int MaintenanceIntervalDays { get; set; } = 90;

        public DateTime? NextMaintenanceDueDate { get; set; }
        // ── SOFT DELETE ──────────────────────────────────────────────
        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        [StringLength (255)]
        public string ImagePath {  get; set; }


        // ── MISC ─────────────────────────────────────────────────────
        [StringLength(1000)]
        public string Notes { get; set; }

        // ── COMPUTED PROPERTIES (NOT MAPPED) ─────────────────────────
        [NotMapped]
        public bool IsInspectionDue =>
            NextInspectionDueDate.HasValue && NextInspectionDueDate.Value <= DateTime.Today;

        [NotMapped]
        public bool IsMaintenanceDue =>
            NextMaintenanceDueDate.HasValue && NextMaintenanceDueDate.Value <= DateTime.Today;

        [NotMapped]
        public bool IsUnderWarranty =>
            WarrantyExpiryDate.HasValue && WarrantyExpiryDate.Value >= DateTime.Today;

        [NotMapped]
        public string StatusDisplay
        {
            get
            {
                switch (Status)
                {
                    case "OutOfService": return "Out of Service";
                    case "UnderMaintenance": return "Under Maintenance";
                    case "Retired": return "Retired";
                    default: return "Available";
                }
            }
        }
    }
}