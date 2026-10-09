using System;
using System.ComponentModel.DataAnnotations;

namespace SwiftyS26.Models
{
    public class Vendor
    {
        [Key]
        public int VendorId { get; set; }

        public string VendorCode { get; set; }

        [Required]
        public string CompanyName { get; set; }

        public string ContactPerson { get; set; }

        [Required]
        public string Phone { get; set; }

        public string Email { get; set; }

        // e.g. "Electrical Repairs", "Cardio Equipment Specialist"
        public string ServiceType { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}