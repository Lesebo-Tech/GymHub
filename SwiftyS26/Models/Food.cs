using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace SwiftyS26.Models
{
    public class Food
    {
        [Key]
        public int FoodId { get; set; }

        [Required]
        [StringLength(100)]
        public string FoodName { get; set; }

        // e.g. "Protein", "Carb", "Vegetable", "Fruit", "Dairy"
        [StringLength(50)]
        public string Category { get; set; }

        // Used to match Google Vision's detected labels to this food
        // e.g. "chicken breast,chicken,poultry"
        [StringLength(300)]
        public string VisionLabelAliases { get; set; }

        public virtual ICollection<FoodPortion> Portions { get; set; }
    }
}