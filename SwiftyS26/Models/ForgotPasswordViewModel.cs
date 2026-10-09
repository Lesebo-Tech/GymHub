using System;

using System.Collections.Generic;

using System.Linq;

using System.Web;

using System.ComponentModel.DataAnnotations;

namespace SwiftyS26.Models

{

    public class ForgotPasswordViewModel

    {

        [Required(ErrorMessage = "Email address is required.")]

        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]

        [Display(Name = "Email Address")]

        public string Email { get; set; }

    }

}
