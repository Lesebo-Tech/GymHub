using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    // ── WORKOUT CATEGORY ─────────────────────────────────────────────
    public class WorkoutCategory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CategoryId { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; }

        [StringLength(20)]
        public string Slug { get; set; }

        [StringLength(200)]
        public string Description { get; set; }

        [StringLength(100)]
        public string IconClass { get; set; }

        [StringLength(20)]
        public string ColorAccent { get; set; }

        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }

        // Converts #RRGGBB → rgba(R,G,B,0.15) for inline background styles
        [NotMapped]
        public string ColorAccent20
        {
            get
            {
                if (string.IsNullOrEmpty(ColorAccent))
                    return "rgba(59,130,246,0.15)";
                try
                {
                    string hex = ColorAccent.TrimStart('#');
                    int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                    int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                    int b = Convert.ToInt32(hex.Substring(4, 2), 16);
                    return string.Format("rgba({0},{1},{2},0.15)", r, g, b);
                }
                catch { return "rgba(59,130,246,0.15)"; }
            }
        }

        public virtual ICollection<WorkoutPlan> WorkoutPlans { get; set; }
    }

    // ── WORKOUT PLAN ─────────────────────────────────────────────────
    public class WorkoutPlan
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PlanId { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [StringLength(20)]
        public string Difficulty { get; set; }

        public int EstimatedMinutes { get; set; }

        [StringLength(100)]
        public string Equipment { get; set; }

        [StringLength(255)]
        public string ThumbnailUrl { get; set; }

        public bool IsActive { get; set; }

        [ForeignKey("CategoryId")]
        public virtual WorkoutCategory Category { get; set; }

        public virtual ICollection<WorkoutPlanExercise> PlanExercises { get; set; }
    }

    // ── EXERCISE LIBRARY ─────────────────────────────────────────────
    public class Exercise
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ExerciseId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(1000)]
        public string Description { get; set; }

        [StringLength(1000)]
        public string Instructions { get; set; }

        [StringLength(255)]
        public string MediaUrl { get; set; }

        [StringLength(20)]
        public string MediaType { get; set; }

        [StringLength(50)]
        public string MuscleGroup { get; set; }

        [StringLength(100)]
        public string Equipment { get; set; }

        public bool IsActive { get; set; }

        public virtual ICollection<WorkoutPlanExercise> PlanExercises { get; set; }
    }

    // ── WORKOUT PLAN ↔ EXERCISE ───────────────────────────────────────
    public class WorkoutPlanExercise
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PlanExerciseId { get; set; }

        [Required]
        public int PlanId { get; set; }

        [Required]
        public int ExerciseId { get; set; }

        public int OrderIndex { get; set; }
        public int? Reps { get; set; }
        public int? Sets { get; set; }
        public int? DurationSeconds { get; set; }
        public int RestSeconds { get; set; }

        [StringLength(200)]
        public string Notes { get; set; }

        [ForeignKey("PlanId")]
        public virtual WorkoutPlan Plan { get; set; }

        [ForeignKey("ExerciseId")]
        public virtual Exercise Exercise { get; set; }
    }

    // ── WORKOUT SESSION ───────────────────────────────────────────────
    public class WorkoutSession
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SessionId { get; set; }

        [Required]
        public int MemberId { get; set; }

        [Required]
        public int PlanId { get; set; }

        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        [StringLength(20)]
        public string Status { get; set; }

        public int TotalDurationSeconds { get; set; }
        public int ExercisesCompleted { get; set; }
        public int CaloriesBurned { get; set; }

        [ForeignKey("MemberId")]
        public virtual MemberForm Member { get; set; }

        [ForeignKey("PlanId")]
        public virtual WorkoutPlan Plan { get; set; }

        public virtual ICollection<SessionExerciseLog> ExerciseLogs { get; set; }
    }

    // ── SESSION EXERCISE LOG ──────────────────────────────────────────
    public class SessionExerciseLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LogId { get; set; }

        [Required]
        public int SessionId { get; set; }

        [Required]
        public int ExerciseId { get; set; }

        public int OrderIndex { get; set; }
        public int RepsCompleted { get; set; }
        public int SetsCompleted { get; set; }
        public int DurationSeconds { get; set; }
        public bool Skipped { get; set; }
        public DateTime LoggedAt { get; set; }

        [ForeignKey("SessionId")]
        public virtual WorkoutSession Session { get; set; }

        [ForeignKey("ExerciseId")]
        public virtual Exercise Exercise { get; set; }
    }

    // ── USER BADGE ────────────────────────────────────────────────────
    public class UserBadge
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BadgeId { get; set; }

        [Required]
        public int MemberId { get; set; }

        [Required]
        [StringLength(50)]
        public string BadgeType { get; set; }

        [StringLength(100)]
        public string BadgeName { get; set; }

        [StringLength(255)]
        public string Description { get; set; }

        [StringLength(100)]
        public string IconClass { get; set; }

        public DateTime EarnedAt { get; set; }

        [ForeignKey("MemberId")]
        public virtual MemberForm Member { get; set; }
    }

    // ── AJAX DTOs ─────────────────────────────────────────────────────
    public class SaveSessionDto
    {
        public int PlanId { get; set; }
        public int TotalDurationSeconds { get; set; }
        public string Status { get; set; }
        public List<ExerciseLogDto> Exercises { get; set; }
    }

    public class ExerciseLogDto
    {
        public int ExerciseId { get; set; }
        public int OrderIndex { get; set; }
        public int RepsCompleted { get; set; }
        public int SetsCompleted { get; set; }
        public int DurationSeconds { get; set; }
        public bool Skipped { get; set; }
    }

    // ── VIEW MODELS ───────────────────────────────────────────────────

    public class WorkoutSummaryVm
    {
        public WorkoutSession Session { get; set; }
        public List<SessionExerciseLog> Logs { get; set; }
        public List<UserBadge> NewBadges { get; set; }
        public int TotalWorkouts { get; set; }
        public int CurrentStreak { get; set; }
    }

    // Used by History view — flat data, no navigation properties accessed in view
    
}