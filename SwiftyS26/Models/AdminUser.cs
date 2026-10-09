using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace SwiftyS26.Models
{
    public class AdminUser
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AdminId { get; set; }

        [Required]
        [StringLength(50)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(50)]
        public string LastName { get; set; }

        [Required]
        [StringLength(100)]
        public string Email { get; set; }

        [Required]
        [StringLength(255)]
        public string PasswordHash { get; set; }

        [StringLength(20)]
        public string Role { get; set; } = "Admin";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? LastLoginAt { get; set; }

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";
    }
}