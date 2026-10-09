using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class Technician
    {
        [Key]
        public int TechnicianId { get; set; }

        public string TechnicianCode { get; set; }

        [Required]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string PhoneNumber { get; set; } = "";

        public string Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }

        // e.g. "Cardio Equipment", "Strength Equipment", "Electrical/Electronic", "General Mechanical"
        public string Specialization { get; set; }

        // e.g. "Apprentice", "Certified Technician", "Senior Technician"
        public string CertificationLevel { get; set; }

        // e.g. "Full-time", "Part-time", "Contract"
        public string EmploymentType { get; set; }

        // ── Auth (mirrors Trainer) ──
        public string PasswordHash { get; set; }
        public bool MustChangePassword { get; set; }
        public bool IsActive { get; set; }
        public string Role { get; set; }

        public DateTime CreatedAt { get; set; }

        [NotMapped]
        public string FullNameDisplay => FullName;

        // Navigation properties for later phases (Inspections, MaintenanceJobs, FaultRepairs)
        // will be added once those entities exist — not needed for this step.
    }
}