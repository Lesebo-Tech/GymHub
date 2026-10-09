using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace SwiftyS26.Models
{
    public class Attendance
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AttendanceId { get; set; }

        [Required]
        public int MemberId { get; set; }

        [StringLength(20)]
        public string MemberNumber { get; set; }

        [StringLength(100)]
        public string MemberName { get; set; }

        [StringLength(20)]
        public string Plan { get; set; }

        public DateTime CheckInTime { get; set; }

        public DateTime? CheckOutTime { get; set; }

        [StringLength(45)]
        public string IpAddress { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }

        [ForeignKey("MemberId")]
        public virtual MemberForm Member { get; set; }

        [NotMapped]
        public string Duration
        {
            get
            {
                if (!CheckOutTime.HasValue) return "In gym";
                var span = CheckOutTime.Value - CheckInTime;
                if (span.TotalHours >= 1)
                    return $"{(int)span.TotalHours}h {span.Minutes}m";
                return $"{span.Minutes}m";
            }
        }

        [NotMapped]
        public bool IsCheckedOut => CheckOutTime.HasValue;
    }
}