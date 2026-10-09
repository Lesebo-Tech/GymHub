using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class InventoryMovement
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int InventoryMovementId { get; set; }

        [Required]
        [ForeignKey("Product")]
        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        public int QuantityChange { get; set; }

        [Required]
        [StringLength(40)]
        public string MovementType { get; set; }

        [StringLength(40)]
        public string ReferenceType { get; set; }

        public int? ReferenceId { get; set; }

        [StringLength(30)]
        public string ReferenceNumber { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }

        [StringLength(80)]
        public string PerformedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
