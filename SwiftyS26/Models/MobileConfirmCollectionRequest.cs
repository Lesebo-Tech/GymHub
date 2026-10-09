using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace SwiftyS26.Models
{
    public class MobileConfirmCollectionRequest
    {
        [Required]
        public int OrderId { get; set; }

        [Required]
        public int StoreStaffId { get; set; }

        [Required]
        public string SignatureBase64 { get; set; }
    }
}