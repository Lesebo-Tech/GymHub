using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class WorkoutProgram
    {
        [Key]
        public int WorkoutProgramId { get; set; }

        [Required]
        public string ProgramName { get; set; }

        public string Description { get; set; }

        [ForeignKey("Trainer")]
        public int TrainerId { get; set; }

        [ForeignKey("MemberForm")]
        public int MemberId { get; set; }

        // ── ADD THESE TWO PROPERTIES ──────────────────────────────
        /// <summary>The date this program begins.</summary>
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        /// <summary>Optional end date for the program.</summary>
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }
        // ─────────────────────────────────────────────────────────

        public DateTime CreatedDate { get; set; } = DateTime.Today;

        public virtual Trainer Trainer { get; set; }

        public virtual MemberForm MemberForm { get; set; }

        public virtual ICollection<ProgramExercise> ProgramExercises { get; set; }
       

    }
}