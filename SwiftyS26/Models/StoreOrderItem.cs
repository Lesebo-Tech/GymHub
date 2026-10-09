using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class StoreOrderItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int StoreOrderItemId { get; set; }

        [Required]
        [ForeignKey("StoreOrder")]
        public int StoreOrderId { get; set; }
        public virtual StoreOrder StoreOrder { get; set; }

        [Required]
        [ForeignKey("Product")]
        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        public decimal UnitPriceAtPurchase { get; set; }

        [NotMapped]
        public decimal LineTotal => Quantity * UnitPriceAtPurchase;
    }
}