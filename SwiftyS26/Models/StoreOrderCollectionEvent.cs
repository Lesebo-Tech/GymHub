using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class StoreOrderCollectionEvent
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int StoreOrderCollectionEventId { get; set; }

        [Required]
        [ForeignKey("StoreOrder")]
        public int StoreOrderId { get; set; }
        public virtual StoreOrder StoreOrder { get; set; }

        [Required]
        [StringLength(50)]
        public string EventType { get; set; }

        [Required]
        [StringLength(500)]
        public string Description { get; set; }

        [StringLength(30)]
        public string PerformedByRole { get; set; }

        [StringLength(80)]
        public string PerformedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
