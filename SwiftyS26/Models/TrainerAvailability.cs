using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class TrainerAvailability
    {
        [Key]
        public int AvailabilityId { get; set; }

        [ForeignKey("Trainer")]
        public int TrainerId { get; set; }

        public string DayOfWeek { get; set; }  // Monday, Tuesday etc
        public string StartTime { get; set; }  // e.g. 06:00
        public string EndTime { get; set; }    // e.g. 14:00
        public bool IsAvailable { get; set; }

        public virtual Trainer Trainer { get; set; }
    }
}