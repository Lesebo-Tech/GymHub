using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class Notification
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int NotificationId { get; set; }

        // "Member", "Admin", "Trainer", "Technician"
        [Required, StringLength(20)]
        public string RecipientType { get; set; }

        // MemberId / TrainerId / TechnicianId / AdminId (Admin is always 1)
        [Required]
        public int RecipientId { get; set; }

        [Required, StringLength(150)]
        public string Title { get; set; }

        [Required, StringLength(500)]
        public string Message { get; set; }

        [StringLength(20)]
        public string Type { get; set; } = "Info"; // Info, Success, Warning, Alert

        [StringLength(200)]
        public string LinkUrl { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public string TimeAgoDisplay
        {
            get
            {
                var span = DateTime.Now - CreatedAt;
                if (span.TotalMinutes < 1) return "just now";
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
                if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
                return CreatedAt.ToString("dd MMM");
            }
        }
    }
}