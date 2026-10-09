using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftyS26.Models
{
    public class MemberForm
    {
        // PRIMARY KEY
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MemberId { get; set; }

        // MEMBER NUMBER — e.g. GH-483920
        [StringLength(20)]
        public string MemberNumber { get; set; }

        // QR CODE — stored as base64 PNG, generated on registration
        [Column(TypeName = "nvarchar(MAX)")]
        public string QrCodeBase64 { get; set; }

        // ── STEP 1: PERSONAL INFORMATION ────────────────────────────

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(50)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(50)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "ID number is required.")]
        [StringLength(13, MinimumLength = 13)]
        [RegularExpression(@"^\d{13}$")]
        [Display(Name = "SA ID Number")]
        public string IdNumber { get; set; }

        [Required(ErrorMessage = "Date of birth is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime DateOfBirth { get; set; }

        [Required(ErrorMessage = "Gender is required.")]
        [StringLength(20)]
        public string Gender { get; set; }

        [Required(ErrorMessage = "Phone number is required.")]
        [Phone]
        [StringLength(15)]
        [Display(Name = "Mobile Number")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress]
        [StringLength(100)]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        // ── PASSWORD ─────────────────────────────────────────────────

        [StringLength(255)]
        public string PasswordHash { get; set; }

        [NotMapped]
        public string Password { get; set; }

        // ── STEP 2: EMAIL VERIFICATION ───────────────────────────────

        [StringLength(6)]
        public string OtpCode { get; set; }

        public DateTime? OtpGeneratedAt { get; set; }

        public bool IsEmailVerified { get; set; } = false;

        // ── STEP 3: TERMS & CONDITIONS ───────────────────────────────

        public bool AcceptedTerms { get; set; }
        public bool AcceptedAgeConfirmation { get; set; }
        public bool AcceptedPrivacyPolicy { get; set; }
        public DateTime? TermsAcceptedAt { get; set; }

        // ── MEMBERSHIP PLAN ──────────────────────────────────────────

        [Required]
        [StringLength(20)]
        public string Plan { get; set; }

        public decimal MonthlyFee { get; set; }

        // ── REGISTRATION METADATA ────────────────────────────────────

        public DateTime RegisteredAt { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        [StringLength(45)]
        public string IpAddress { get; set; }

        // ── LOGIN SECURITY ───────────────────────────────────────────

        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LockoutEnd { get; set; }
        public DateTime? LastLoginAt { get; set; }

        // ── PASSWORD RESET ───────────────────────────────────────────

        [NotMapped]
        public string ResetOtpCode { get; set; }

        [NotMapped]
        public DateTime? ResetOtpGeneratedAt { get; set; }

        // ── MEMBERSHIP EXPIRY ────────────────────────────────────────

        public DateTime? MembershipExpiry { get; set; }

        // ── COMPUTED PROPERTIES (NOT MAPPED) ─────────────────────────

        [NotMapped]
        public int Age => MemberHelper.CalculateAge(DateOfBirth);

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";

        [NotMapped]
        public bool IsOtpExpired =>
            OtpGeneratedAt == null ||
            (DateTime.Now - OtpGeneratedAt.Value).TotalMinutes > 10;

        [NotMapped]
        public bool IsLockedOut =>
            LockoutEnd.HasValue && LockoutEnd.Value > DateTime.Now;

        [NotMapped]
        public bool IsResetOtpExpired =>
            ResetOtpGeneratedAt == null ||
            (DateTime.Now - ResetOtpGeneratedAt.Value).TotalMinutes > 10;

        [NotMapped]
        public bool IsMembershipExpired =>
            MembershipExpiry.HasValue && MembershipExpiry.Value < DateTime.Now;

        [NotMapped]
        public int DaysUntilExpiry =>
            MembershipExpiry.HasValue
                ? Math.Max(0, (int)(MembershipExpiry.Value - DateTime.Now).TotalDays)
                : 0;

        [NotMapped]
        public string MembershipExpiryDisplay =>
            MembershipExpiry.HasValue
                ? MembershipExpiry.Value.ToString("dd MMM yyyy")
                : "—";
               [NotMapped]
        public string MembershipExpiryForJs =>
            MembershipExpiry.HasValue
                ? MembershipExpiry.Value.ToString("yyyy-MM-dd")
                : string.Empty;

        [NotMapped]
        public bool IsExpiringSoon =>
            MembershipExpiry.HasValue &&
            MembershipExpiry.Value > DateTime.Now &&
            DaysUntilExpiry <= 7;

        [NotMapped]
        public string PlanDisplay => MemberHelper.GetPlanDisplay(Plan);

        [NotMapped]
        public string MonthlyFeeDisplay => $"R{MonthlyFee:F2}";

        [NotMapped]
        public List<Payment> Payments { get; set; }
        // REFERRAL CODE — this member's own shareable code, generated on first use of
        // "Refer a Friend" (not at registration, to avoid generating codes nobody needs)
        [StringLength(20)]
        public string ReferralCode { get; set; }

        // Which referral code this NEW registrant arrived with, if any (from the
        // ?ref= query string on /Forms/Register). Not persisted directly — resolved
        // into a Referral row against the referrer once registration completes.
        [NotMapped]
        public string ReferredByCode { get; set; }


    }
}