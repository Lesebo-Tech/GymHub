using SwiftyS26.Models;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SwiftyS26.Controllers
{
    public class NotificationController : Controller
    {
        private readonly GymHubDB db = new GymHubDB();

        public JsonResult GetRecent()
        {
            var recipient = SessionHelper.GetCurrentRecipient(Session);
            if (recipient == null)
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            var notifications = db.Notifications
                .Where(n => n.RecipientType == recipient.Type && n.RecipientId == recipient.Id)
                .OrderByDescending(n => n.CreatedAt)
                .Take(8)
                .ToList()
                .Select(n => new
                {
                    n.NotificationId,
                    n.Title,
                    n.Message,
                    n.IsRead,
                    n.LinkUrl,
                    n.Type,
                    timeAgo = n.TimeAgoDisplay
                });

            int unreadCount = db.Notifications.Count(n =>
                n.RecipientType == recipient.Type && n.RecipientId == recipient.Id && !n.IsRead);

            return Json(new { success = true, notifications, unreadCount }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult MarkAsRead(int id)
        {
            var recipient = SessionHelper.GetCurrentRecipient(Session);
            if (recipient == null) return Json(new { success = false });

            var n = db.Notifications.FirstOrDefault(x =>
                x.NotificationId == id && x.RecipientType == recipient.Type && x.RecipientId == recipient.Id);

            if (n != null) { n.IsRead = true; db.SaveChanges(); }
            return Json(new { success = true });
        }

        [HttpPost]
        public JsonResult MarkAllAsRead()
        {
            var recipient = SessionHelper.GetCurrentRecipient(Session);
            if (recipient == null) return Json(new { success = false });

            var unread = db.Notifications.Where(x =>
                x.RecipientType == recipient.Type && x.RecipientId == recipient.Id && !x.IsRead);

            foreach (var n in unread) n.IsRead = true;
            db.SaveChanges();

            return Json(new { success = true });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}