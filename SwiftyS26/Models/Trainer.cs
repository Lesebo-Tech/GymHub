using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class Trainer
    {
        [Key]
        public int TrainerId { get; set; }
        public string TrainerCode { get; set; }

        [Required]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string PhoneNumber { get; set; } = "";

        public string Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string Specialization { get; set; }
        public string ExperienceLevel { get; set; }
        public string Qualification { get; set; }
        public string EmploymentType { get; set; }
        public int MaxCapacity { get; set; }

        // Auth
        public string PasswordHash { get; set; }
        public bool MustChangePassword { get; set; }
        public bool IsActive { get; set; }
        public string Role { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual ICollection<MemberTrainer> MemberTrainers { get; set; }
        public virtual ICollection<TrainingSession> TrainingSessions { get; set; }
        public virtual ICollection<WorkoutProgram> WorkoutPrograms { get; set; }
        public virtual ICollection<TrainerAvailability> Availabilities { get; set; }
        public virtual ICollection<MemberProgress> MemberProgresses { get; set; }
    }
}