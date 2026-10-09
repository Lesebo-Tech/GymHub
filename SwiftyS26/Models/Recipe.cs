using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class Recipe
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RecipeId { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; }

        // Breakfast | Lunch | Dinner | Snack
        [Required]
        [StringLength(20)]
        public string MealTime { get; set; }

        [Required]
        public int Calories { get; set; }

        [Required]
        public decimal Protein { get; set; }

        [Required]
        public decimal Carbs { get; set; }

        [Required]
        public decimal Fat { get; set; }

        [StringLength(200)]
        public string PortionDescription { get; set; } // "1 bowl (250g)"

        // Comma-separated: "None,Vegetarian,Vegan" — a recipe can satisfy
        // multiple preferences (e.g. vegan food is also vegetarian)
        [StringLength(100)]
        public string DietaryTags { get; set; }

        // Comma-separated allergens THIS recipe contains — used to
        // EXCLUDE it for members with a matching allergy
        [StringLength(100)]
        public string Allergens { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public bool IsActive { get; set; } = true;
    }
}