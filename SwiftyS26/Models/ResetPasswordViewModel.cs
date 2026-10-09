
using System;

using System.Collections.Generic;

using System.Linq;

using System.Web;

using System.ComponentModel.DataAnnotations;
 
namespace SwiftyS26.Models

{

    public class ResetPasswordViewModel

    {

        [Required]

        public string Email { get; set; }

        [Required(ErrorMessage = "Please enter the 6-digit code.")]

        [StringLength(6, MinimumLength = 6, ErrorMessage = "Code must be 6 digits.")]

        public string OtpCode { get; set; }

        [Required(ErrorMessage = "Please enter a new password.")]

        [StringLength(100, MinimumLength = 8,

            ErrorMessage = "Password must be at least 8 characters.")]

        [DataType(DataType.Password)]

        [Display(Name = "New Password")]

        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Please confirm your new password.")]

        [DataType(DataType.Password)]

        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]

        [Display(Name = "Confirm Password")]

        public string ConfirmPassword { get; set; }

    }

}
