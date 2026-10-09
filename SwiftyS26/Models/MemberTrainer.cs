using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class MemberTrainer
    {
        [Key]
        public int Id { get; set; }
        public int MemberId { get; set; }
        public int TrainerId { get; set; }
        public DateTime AssignedDate { get; set; }

        [ForeignKey("TrainerId")]
        public virtual Trainer Trainer { get; set; }

        [ForeignKey("MemberId")]          // ← tell EF which FK to use
        public virtual MemberForm Member { get; set; }   // ← single object, not a collection
    }
}