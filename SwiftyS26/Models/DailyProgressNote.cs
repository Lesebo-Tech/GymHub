using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class DailyProgressNote
    {
        [Key]
        public int DailyProgressNoteId { get; set; }

        [Required]
        public int WorkoutSessionLogId { get; set; }

        [Required]
        public int MemberId { get; set; }

        [Required]
        public int TrainerId { get; set; }

        [Required, MaxLength(500)]
        public string TrainerDescription { get; set; }

        public DateTime SavedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("WorkoutSessionLogId")]
        public virtual WorkoutSessionLog WorkoutSessionLog { get; set; }

        [ForeignKey("MemberId")]
        public virtual MemberForm MemberForm { get; set; }
    }
}
