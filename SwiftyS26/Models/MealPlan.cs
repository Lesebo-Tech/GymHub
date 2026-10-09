using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class MealPlan
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MealPlanId { get; set; }

        [Required]
        [ForeignKey("Member")]
        public int MemberId { get; set; }
        public virtual MemberForm Member { get; set; }

        public int TargetCalories { get; set; }
        public decimal TargetProtein { get; set; }
        public decimal TargetCarbs { get; set; }
        public decimal TargetFat { get; set; }

        [StringLength(30)]
        public string GoalType { get; set; }

        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public bool IsAccepted { get; set; } = false;

        public virtual ICollection<MealPlanItem> Items { get; set; }
    }

    public class MealPlanItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MealPlanItemId { get; set; }

        [Required]
        [ForeignKey("MealPlan")]
        public int MealPlanId { get; set; }
        public virtual MealPlan MealPlan { get; set; }

        [Required]
        public int DayNumber { get; set; } // 1-7

        [Required]
        [ForeignKey("Recipe")]
        public int RecipeId { get; set; }
        public virtual Recipe Recipe { get; set; }

        [Required]
        [StringLength(20)]
        public string MealTime { get; set; } // Breakfast | Lunch | Dinner | Snack
    }
}