// Models/NotificationService.cs
using System;
using System.Linq;

namespace SwiftyS26.Models
{
    public class NotificationService
    {
        private readonly GymHubDB _db;

        public NotificationService(GymHubDB db)
        {
            _db = db;
        }

        public void Notify(string recipientType, int recipientId, string title, string message,
            string type = "Info", string linkUrl = null)
        {
            _db.Notifications.Add(new Notification
            {
                RecipientType = recipientType,
                RecipientId = recipientId,
                Title = title,
                Message = message,
                Type = type,
                LinkUrl = linkUrl,
                CreatedAt = DateTime.Now
            });
            _db.SaveChanges();
        }

        // Convenience: notify every active Technician (repair queue, etc.)
        public void NotifyAllTechnicians(string title, string message, string type = "Info", string linkUrl = null)
        {
            var techIds = _db.Technicians.Where(t => t.IsActive).Select(t => t.TechnicianId).ToList();
            foreach (var id in techIds)
                Notify("Technician", id, title, message, type, linkUrl);
        }

        // ── ADDED — mirrors NotifyAllTechnicians exactly, so whoever's
        // on shift in the store actually sees a new order arrive. ──
        public void NotifyAllStoreStaff(string title, string message, string type = "Info", string linkUrl = null)
        {
            var staffIds = _db.StoreStaffs.Where(s => s.IsActive).Select(s => s.StoreStaffId).ToList();
            foreach (var id in staffIds)
                Notify("StoreStaff", id, title, message, type, linkUrl);
        }

        // Convenience: notify Admin (always AdminId = 1 per your AdminController constant)
        public void NotifyAdmin(string title, string message, string type = "Info", string linkUrl = null)
        {
            Notify("Admin", 1, title, message, type, linkUrl);
        }
    }
}