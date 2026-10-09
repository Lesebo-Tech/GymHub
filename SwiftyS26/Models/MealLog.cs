using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class MealLog
    {
        [Key]
        public int MealLogId { get; set; }

        [Required]
        [ForeignKey("Member")]
        public int MemberId { get; set; }
        public virtual MemberForm Member { get; set; }

        // "Breakfast" / "Lunch" / "Dinner" / "Snack"
        [StringLength(20)]
        public string MealType { get; set; }

        // Path to the uploaded meal photo (e.g. /Content/meal-photos/xyz.jpg)
        [StringLength(300)]
        public string PhotoUrl { get; set; }

        public DateTime LoggedAt { get; set; } = DateTime.Now;

        // ── Totals — calculated once when the meal is saved ──
        public decimal TotalCalories { get; set; }
        public decimal TotalProtein { get; set; }
        public decimal TotalCarbs { get; set; }
        public decimal TotalFat { get; set; }
        public decimal TotalFibre { get; set; }

        // Snapshot of the member's goal at the time this meal was logged
        // ("Weight Loss" / "Muscle Gain" / "Endurance" / "Maintenance")
        [StringLength(30)]
        public string GoalTypeAtLogTime { get; set; }

        // "On Track" / "Off Track" / "Neutral" — the verdict shown to the member
        [StringLength(20)]
        public string GoalVerdict { get; set; }

        // Short human-readable explanation shown alongside the verdict
        [StringLength(300)]
        public string GoalVerdictMessage { get; set; }

        public virtual ICollection<MealLogItem> Items { get; set; }
    }
}