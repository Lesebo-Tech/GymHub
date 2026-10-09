
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    /// <summary>
    /// Records every workout session a member starts — whether Completed or Absent.
    /// Visible to the assigned trainer in their dashboard.
    /// </summary>
    public class WorkoutSessionLog
    {
        [Key]
        public int WorkoutSessionLogId { get; set; }

        [Required]
        [ForeignKey("MemberForm")]
        public int MemberId { get; set; }

        [Required]
        [ForeignKey("WorkoutProgram")]
        public int WorkoutProgramId { get; set; }

        [Required]
        [ForeignKey("Trainer")]
        public int TrainerId { get; set; }

        /// <summary>"Completed" or "Absent"</summary>
        [Required]
        [StringLength(20)]
        public string Status { get; set; }

        /// <summary>The calendar date of this log entry.</summary>
        public DateTime SessionDate { get; set; } = DateTime.Today;

        /// <summary>
        /// Actual seconds the member worked out (captured from the JS timer).
        /// 0 if Absent.
        /// </summary>
        public int ActualDurationSeconds { get; set; }

        /// <summary>
        /// Pipe-separated list of exercise names that were part of the program,
        /// e.g. "Push-ups|Squats|Plank"
        /// </summary>
        public string ExercisesSummary { get; set; }

        /// <summary>Optional member note logged after finishing.</summary>
        [StringLength(500)]
        public string Notes { get; set; }

        public DateTime LoggedAt { get; set; } = DateTime.Now;

        // ── Navigation ──────────────────────────────────────────────
        public virtual MemberForm MemberForm { get; set; }
        public virtual WorkoutProgram WorkoutProgram { get; set; }
        public virtual Trainer Trainer { get; set; }
    }
}