using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class FoodPortion
    {
        [Key]
        public int PortionId { get; set; }

        [Required]
        [ForeignKey("Food")]
        public int FoodId { get; set; }
        public virtual Food Food { get; set; }

        // e.g. "1 piece", "Small portion", "Medium portion", "Large portion"
        [Required]
        [StringLength(50)]
        public string PortionName { get; set; }

        public int WeightGrams { get; set; }

        // Nutrition values for THIS portion (not per 100g — keeps math simple)
        public decimal Calories { get; set; }
        public decimal Protein { get; set; }
        public decimal Carbs { get; set; }
        public decimal Fat { get; set; }
        public decimal Fibre { get; set; }
    }
}