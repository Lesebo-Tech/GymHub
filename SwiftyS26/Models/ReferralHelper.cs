using System;
using System.Linq;

namespace SwiftyS26.Models
{
    public static class ReferralHelper
    {
        // Points credited to the referrer once their friend registers and their
        // membership goes Active. Matches the scale of the existing milestone
        // bonuses in AdminController.CheckIn (10 base, 50/150/300/500 milestones).
        public const int REFERRAL_REWARD_POINTS = 500;

        private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I ambiguity

        // Generates a unique 8-character referral code, checked against existing
        // member codes to avoid collisions.
        public static string GenerateUniqueReferralCode(GymHubDB db)
        {
            var random = new Random();
            string code;
            int attempts = 0;

            do
            {
                code = new string(Enumerable.Repeat(CodeChars, 8)
                    .Select(s => s[random.Next(s.Length)]).ToArray());
                attempts++;
            }
            while (attempts < 15 && db.Members.Any(m => m.ReferralCode == code));

            return code;
        }
    }
}