using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SwiftyS26.Models
{
    public class TrainerDashboardViewModel
    {
        public List<MemberTrainer> AssignedMembers { get; set; }
        public List<TrainingSession> UpcomingSessions { get; set; }
        public List<WorkoutProgram> WorkoutPrograms { get; set; }
        public List<MemberProgress> ProgressLogs { get; set; }
        public List<TrainerAvailability> Availability { get; set; }
        public List<WorkoutSessionLog> MemberSessionLogs { get; set; }
        
    }
}