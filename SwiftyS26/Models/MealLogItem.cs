using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class MealLogItem
    {
        [Key]
        public int MealLogItemId { get; set; }

        [Required]
        [ForeignKey("MealLog")]
        public int MealLogId { get; set; }
        public virtual MealLog MealLog { get; set; }

        // Nullable — if the AI detected something not in our Food database,
        // we still keep the item with FoodId = null and FoodName as free text
        [ForeignKey("Food")]
        public int? FoodId { get; set; }
        public virtual Food Food { get; set; }

        [Required]
        [StringLength(100)]
        public string FoodName { get; set; }

        [StringLength(50)]
        public string PortionName { get; set; }

        public int WeightGrams { get; set; }

        // Snapshot values at the time of logging — protects historical
        // accuracy even if FoodPortion nutrition data changes later
        public decimal Calories { get; set; }
        public decimal Protein { get; set; }
        public decimal Carbs { get; set; }
        public decimal Fat { get; set; }
        public decimal Fibre { get; set; }
    }
}