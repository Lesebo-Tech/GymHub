using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class TrainingSession
    {
        [Key]
        public int SessionId { get; set; }

        [ForeignKey("Trainer")]
        public int TrainerId { get; set; }

        [ForeignKey("MemberForm")]
        public int MemberId { get; set; }

        public DateTime SessionDate { get; set; }

        public string TimeSlot { get; set; }

        public string SessionType { get; set; } // e.g. Strength, Cardio, HIIT
        public string Notes { get; set; }
        public string Duration { get; set; } // e.g. "60 min"

        public string Status { get; set; }

        public virtual Trainer Trainer { get; set; }

        public virtual MemberForm MemberForm { get; set; }
    }
}