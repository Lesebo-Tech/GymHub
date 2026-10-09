using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SwiftyS26.Models
{
    public static class LockerFaultHelper
    {
        public static readonly string[] FaultTypes = new[]
        {
            "Jammed", "LockFaulty", "DoorDamaged", "WontClose", "Other"
        };

        public static readonly string[] Severities = new[] { "Minor", "Major", "Critical" };

        public static readonly string[] ValidStatuses = new[]
        {
            "Reported", "Triaged", "Assigned", "InProgress", "Resolved", "Unrepairable"
        };

        public static readonly string[] OpenStatuses = new[]
        {
            "Reported", "Triaged", "Assigned", "InProgress"
        };

        public static readonly string[] OperationalStatuses = new[]
        {
            "InService", "ReportedFaulty", "OutOfService", "UnderRepair", "Retired"
        };

        public static bool IsValidFaultType(string t) => FaultTypes.Contains(t);
        public static bool IsValidSeverity(string s) => Severities.Contains(s);
        public static bool IsValidStatus(string s) => ValidStatuses.Contains(s);

        // LF-2026-4821 — same shape as the equipment repair reference, so
        // staff read them the same way.
        public static string GenerateReferenceNumber(GymHubDB db)
        {
            var rng = new Random();
            string refNumber;
            int attempts = 0;
            do
            {
                refNumber = "LF-" + DateTime.Now.Year + "-" + rng.Next(1000, 9999);
                attempts++;
            }
            while (attempts < 15 && db.LockerFaults.Any(f => f.ReferenceNumber == refNumber));

            return refNumber;
        }

        // Which operational status a locker should be in, given a triage
        // severity. Minor faults deliberately do NOT pull a locker from
        // service — a peeling sticker shouldn't cost the gym a locker.
        public static string OperationalStatusForSeverity(string severity)
        {
            switch (severity)
            {
                case "Critical":
                case "Major":
                    return "OutOfService";
                default:
                    return "InService";
            }
        }

        // Notification urgency mirrors severity so admin's bell reflects
        // real operational impact rather than treating everything equally.
        public static string NotificationTypeForSeverity(string severity)
        {
            switch (severity)
            {
                case "Critical": return "Alert";
                case "Major": return "Warning";
                default: return "Info";
            }
        }
    }
}