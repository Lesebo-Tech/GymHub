using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;


namespace SwiftyS26.Models
{
    // ============================================================
    // Used by: Views/MemberTrainer/TrainerHub.cshtml
    // Controller: MemberTrainerController.TrainerHub()
    // ============================================================
    public class MemberTrainerHubViewModel
    {
        public MemberForm Member { get; set; }
        public MemberTrainer Assignment { get; set; }       // null = no trainer yet
        public FitnessProfile FitnessProfile { get; set; }  // null = no profile set
        public List<TrainingSession> UpcomingSessions { get; set; }
        public List<WorkoutProgram> WorkoutPrograms { get; set; }


        // Convenience so views don't need null checks everywhere
        public bool HasTrainer => Assignment != null;
        public bool HasFitnessProfile => FitnessProfile != null;
        public int DaysTogether => HasTrainer
            ? (DateTime.Today - Assignment.AssignedDate).Days
            : 0;
    }

    // ============================================================
    // Used by: Views/MemberTrainer/FindTrainer.cshtml
    // Controller: MemberTrainerController.FindTrainer()
    // ============================================================
    public class TrainerScoreItem
    {
        public Trainer Trainer { get; set; }
        public bool IsMatch { get; set; }       // specialization matches member goal
        public int ExperienceRank { get; set; } // used for sorting only
        public int CurrentLoad { get; set; }    // how many members assigned
    }
}