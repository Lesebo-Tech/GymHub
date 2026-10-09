using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class ShoppingCartItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ShoppingCartItemId { get; set; }

        [Required]
        [ForeignKey("Member")]
        public int MemberId { get; set; }
        public virtual MemberForm Member { get; set; }

        [Required]
        [ForeignKey("Product")]
        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        [Required]
        public int Quantity { get; set; } = 1;

        public DateTime AddedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public decimal LineTotal => Product != null ? Product.NormalPrice * Quantity : 0;
    }
}