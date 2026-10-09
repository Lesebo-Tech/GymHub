using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    // ============================================================
    // UC09 — FitnessProfile
    // One per member — created on first submission, updated anytime
    // ============================================================
    public class FitnessProfile
    {
        [Key]
        public int FitnessProfileId { get; set; }

        [Required]
        public int MemberId { get; set; }

        // ── Body Measurements ──
        public decimal HeightCm { get; set; }       // e.g. 175.0
        public decimal WeightKg { get; set; }       // e.g. 82.0
        public decimal BodyFatPercent { get; set; } // e.g. 22.5
        public int Age { get; set; }
        public string Gender { get; set; }          // "Male" / "Female"

        // ── Calculated by server ──
        public decimal BMI { get; set; }
        public string BMICategory { get; set; }     // Underweight/Normal/Overweight/Obese
        public decimal BMR { get; set; }            // Basal Metabolic Rate (kcal/day)
        public decimal TDEE { get; set; }           // Total Daily Energy Expenditure

        // ── Activity Level ──
        // Sedentary / Lightly Active / Moderately Active / Very Active / Extremely Active
        public string ActivityLevel { get; set; }

        // ── Goal (auto-recommended + member can override) ──
        // Weight Loss / Muscle Gain / Endurance / Maintenance
        public string GoalType { get; set; }

        // ── Targets ──
        public decimal TargetWeightKg { get; set; }
        public decimal TargetBodyFatPercent { get; set; }

        // ── Timeline ──
        public int ProjectedWeeks { get; set; }
        public DateTime? ProjectedGoalDate { get; set; }

        // ── Alert tracking ──
        public bool IsOnTrack { get; set; } = true;
        public int AlertsSentCount { get; set; } = 0;
        public DateTime? LastAlertSentAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        [ForeignKey("MemberId")]
        public virtual MemberForm Member { get; set; }
        // ── ADD to FitnessProfile.cs ──
        public string DietaryPreference { get; set; } // None | Vegetarian | Vegan | Halal | GlutenFree
        public string Allergies { get; set; }          // comma-separated: "Nuts,Dairy,Shellfish,Gluten"
    }

    // ============================================================
    // UC09 — FitnessProgressLog
    // One entry per weekly check-in by the member
    // ============================================================
    public class FitnessProgressLog
    {
        [Key]
        public int ProgressLogId { get; set; }

        [Required]
        public int MemberId { get; set; }

        // ── What the member logs each week ──
        public decimal WeightKg { get; set; }
        public decimal BodyFatPercent { get; set; }
        public int WorkoutsCompleted { get; set; }   // sessions that week
        public string Notes { get; set; }            // optional freetext

        // ── Server-calculated on save ──
        public decimal BMI { get; set; }
        public decimal WeightChangeSinceStart { get; set; }   // negative = loss
        public decimal BodyFatChangeSinceStart { get; set; }
        public bool IsUnderperforming { get; set; } = false;

        public int WeekNumber { get; set; }          // 1, 2, 3...
        public DateTime LoggedAt { get; set; } = DateTime.Now;

        [ForeignKey("MemberId")]
        public virtual MemberForm Member { get; set; }
    }
}