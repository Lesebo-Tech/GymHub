using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class MemberProgress
    {
        [Key]
        public int ProgressId { get; set; }

        [ForeignKey("MemberForm")]
        public int MemberId { get; set; }


        [ForeignKey("Trainer")]
        public int TrainerId { get; set; }

        public double Weight { get; set; }

        public string Notes { get; set; }

        public DateTime RecordedDate { get; set; }

        public virtual MemberForm MemberForm { get; set; }

        public virtual Trainer Trainer { get; set; }
    }
}