using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class Feedback
    {
        // PRIMARY KEY
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int FeedbackId { get; set; }

        // ── OWNER ────────────────────────────────────────────────────

        [Required]
        [ForeignKey("Member")]
        public int MemberId { get; set; }

        public virtual MemberForm Member { get; set; }

        // ── FEEDBACK DETAILS ─────────────────────────────────────────

        [Required(ErrorMessage = "Please select a feedback category.")]
        [StringLength(30)]
        [Display(Name = "Category")]
        public string Category { get; set; }

        [Required(ErrorMessage = "Please enter your comments.")]
        [StringLength(1000)]
        [Display(Name = "Comments")]
        public string Comments { get; set; }

        [Required(ErrorMessage = "Please provide a rating.")]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public int Rating { get; set; }

        // ── METADATA ─────────────────────────────────────────────────

        [StringLength(45)]
        public string IpAddress { get; set; }

        public DateTime SubmittedAt { get; set; }

        // ── COMPUTED PROPERTIES (NOT MAPPED) ─────────────────────────

        [NotMapped]
        public string SubmittedAtDisplay => SubmittedAt.ToString("dd MMM yyyy, HH:mm");

        [NotMapped]
        public string StarDisplay => new string('★', Rating) + new string('☆', 5 - Rating);
    }
}