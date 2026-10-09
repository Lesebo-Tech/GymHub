using System;

using System.Collections.Generic;

using System.Linq;

using System.Web;

using System.ComponentModel.DataAnnotations;

namespace SwiftyS26.Models

{

    public class LoginViewModel

    {

        [Required(ErrorMessage = "Email address is required.")]

        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]

        [Display(Name = "Email Address")]

        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]

        [DataType(DataType.Password)]

        [Display(Name = "Password")]

        public string Password { get; set; }

        [Display(Name = "Remember Me")]

        public bool RememberMe { get; set; }

    }

}
