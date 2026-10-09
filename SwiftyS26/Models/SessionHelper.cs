using System.Web;

namespace SwiftyS26.Models
{
    public static class SessionHelper
    {
        public class Recipient
        {
            public string Type { get; set; }
            public int Id { get; set; }
        }

        public static Recipient GetCurrentRecipient(HttpSessionStateBase session)
        {
            if (session["LoggedInMember"] != null)
                return new Recipient { Type = "Member", Id = (int)session["LoggedInMember"] };

            if (session["LoggedInAdmin"] != null)
                return new Recipient { Type = "Admin", Id = (int)session["LoggedInAdmin"] };

            if (session["LoggedInTrainer"] != null)
                return new Recipient { Type = "Trainer", Id = (int)session["LoggedInTrainer"] };

            if (session["LoggedInTechnician"] != null)
                return new Recipient { Type = "Technician", Id = (int)session["LoggedInTechnician"] };

            // FIX — was missing, this is why the StoreStaff bell showed nothing
            if (session["LoggedInStoreStaff"] != null)
                return new Recipient { Type = "StoreStaff", Id = (int)session["LoggedInStoreStaff"] };

            return null;
        }
    }
}