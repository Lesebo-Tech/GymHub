using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace SwiftyS26.Models
{
    public class MobileOrderQrRequest
    {
        [Required]
        public string QrData { get; set; }

        [Required]
        public int StoreStaffId { get; set; }
    }
}