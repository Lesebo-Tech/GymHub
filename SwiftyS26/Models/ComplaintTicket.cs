using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class ComplaintTicket
    {
        // PRIMARY KEY
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ComplaintId { get; set; }

        // REFERENCE NUMBER — e.g. CMP-20260730-4821, shown to the member for tracking
        [Required]
        [StringLength(20)]
        public string ReferenceNumber { get; set; }

        // ── OWNER ────────────────────────────────────────────────────

        [Required]
        [ForeignKey("Member")]
        public int MemberId { get; set; }

        public virtual MemberForm Member { get; set; }

        // ── STEP 1: COMPLAINT DETAILS ───────────────────────────────

        [Required(ErrorMessage = "Please select a complaint category.")]
        [StringLength(30)]
        [Display(Name = "Category")]
        public string Category { get; set; }

        [Required(ErrorMessage = "Please enter the complaint details.")]
        [StringLength(2000)]
        [Display(Name = "Complaint Details")]
        public string Details { get; set; }

        // ── STEP 2: OPTIONAL EVIDENCE ────────────────────────────────

        [StringLength(260)]
        public string EvidenceFileName { get; set; }

        [StringLength(300)]
        public string EvidenceFilePath { get; set; }

        // ── STATUS TRACKING ──────────────────────────────────────────
        // Submitted -> Under Investigation -> Resolved -> Closed

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Submitted";

        [StringLength(2000)]
        [Display(Name = "Staff Comments")]
        public string StaffComments { get; set; }

        [StringLength(2000)]
        [Display(Name = "Resolution Details")]
        public string ResolutionDetails { get; set; }

        // ── HANDLING METADATA ────────────────────────────────────────

        public int? HandledByStaffId { get; set; }

        [StringLength(45)]
        public string IpAddress { get; set; }

        public DateTime SubmittedAt { get; set; }

        public DateTime? LastUpdatedAt { get; set; }

        public DateTime? ClosedAt { get; set; }

        // ── COMPUTED PROPERTIES (NOT MAPPED) ─────────────────────────

        [NotMapped]
        public bool IsClosed => Status == "Closed";

        [NotMapped]
        public bool HasEvidence => !string.IsNullOrWhiteSpace(EvidenceFilePath);

        [NotMapped]
        public string SubmittedAtDisplay => SubmittedAt.ToString("dd MMM yyyy, HH:mm");

        [NotMapped]
        public string LastUpdatedAtDisplay =>
            LastUpdatedAt.HasValue ? LastUpdatedAt.Value.ToString("dd MMM yyyy, HH:mm") : "—";
    }
}