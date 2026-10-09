using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class StoreStaff
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int StoreStaffId { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; }

        [Required]
        [StringLength(150)]
        public string Email { get; set; }

        [StringLength(20)]
        public string PhoneNumber { get; set; }

        [StringLength(10)]
        public string Gender { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [StringLength(20)]
        public string StoreStaffCode { get; set; }

        [StringLength(200)]
        public string PasswordHash { get; set; }

        public bool MustChangePassword { get; set; } = true;
        public bool IsActive { get; set; } = true;

        [StringLength(20)]
        public string Role { get; set; } = "StoreStaff";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}