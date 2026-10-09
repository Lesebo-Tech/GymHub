using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class Supplier
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SupplierId { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; }

        [StringLength(100)]
        public string ContactPerson { get; set; }

        [StringLength(150)]
        public string Email { get; set; }

        [StringLength(20)]
        public string PhoneNumber { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}