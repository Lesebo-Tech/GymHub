using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SwiftyS26.Models
{
    public class SessionRowVm
    {
        public int SessionId { get; set; }
        public string PlanName { get; set; }
        public string CategoryName { get; set; }
        public string CategoryColor { get; set; }
        public string StartedAt { get; set; }
        public int DurationSecs { get; set; }
        public string Status { get; set; }

        // Computed helpers used directly in the view
        public int DurationMins { get { return DurationSecs / 60; } }
        public string StatusClass
        {
            get { return Status == "Completed" ? "completed" : "in-progress"; }
        }
    }
}