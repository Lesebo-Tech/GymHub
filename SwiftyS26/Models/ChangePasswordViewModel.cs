using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace SwiftyS26.Models
{
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Current password is required.")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "New password is required.")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Please confirm your new password.")]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }
    }
}