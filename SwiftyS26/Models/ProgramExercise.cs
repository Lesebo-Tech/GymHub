using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class ProgramExercise  // renamed from Exercise
    {
        [Key]
        public int ProgramExerciseId { get; set; }  // rename key too

        [Required]
        public string ExerciseName { get; set; }
        public int Sets { get; set; }
        public int Reps { get; set; }
        public int DurationMinutes { get; set; }

        [ForeignKey("WorkoutProgram")]
        public int WorkoutProgramId { get; set; }
        public virtual WorkoutProgram WorkoutProgram { get; set; }
    }
}