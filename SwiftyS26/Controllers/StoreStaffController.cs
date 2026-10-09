using SwiftyS26.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;


namespace SwiftyS26.Controllers
{
    public class StoreStaffController : Controller
    {
        private GymHubDB db = new GymHubDB();

        private bool IsStoreStaffLoggedIn() => Session["LoggedInStoreStaff"] != null;

        private StoreStaff GetLoggedInStoreStaff()
        {
            if (!IsStoreStaffLoggedIn()) return null;
            int id = (int)Session["LoggedInStoreStaff"];
            return db.StoreStaffs.Find(id);
        }

        public ActionResult Dashboard()
        {
            if (!IsStoreStaffLoggedIn())
                return RedirectToAction("Login", "Admin");

            var staff = GetLoggedInStoreStaff();
            if (staff == null)
                return RedirectToAction("Login", "Admin");

            if (staff.MustChangePassword)
                return RedirectToAction("ChangePassword");

            // Run the same lazy uncollected-order check the Orders page uses,
            // so the dashboard's numbers are never stale relative to it.
            StoreOrderHelper.CheckUncollectedOrders(db);

            // ── Product stats ──
            ViewBag.StoreStaffName = staff.FullName;
            ViewBag.LowStockCount = db.Products.Count(p => p.IsActive && p.QuantityOnHand > 0 && p.QuantityOnHand <= p.ReorderLevel);
            ViewBag.OutOfStockCount = db.Products.Count(p => p.IsActive && p.QuantityOnHand <= 0);
            ViewBag.TotalProducts = db.Products.Count(p => p.IsActive);

            // ── Order stats ──
            ViewBag.PreparingCount = db.StoreOrders.Count(o => o.Status == "Paid" || o.Status == "Preparing");
            ViewBag.ReadyCount = db.StoreOrders.Count(o => o.Status == "ReadyForPickup");

            var today = DateTime.Today;
            var todayOrders = db.StoreOrders.Where(o => o.CreatedAt >= today && o.Status != "PendingPayment").ToList();
            ViewBag.TodayOrdersCount = todayOrders.Count;
            ViewBag.TodayRevenue = todayOrders.Sum(o => (decimal?)o.FinalTotal) ?? 0;
            int hour = DateTime.Now.Hour;
            ViewBag.Greeting = hour < 12 ? "Good morning" : hour < 17 ? "Good afternoon" : "Good evening";

            // ── Returns needing staff attention ──
            ViewBag.PendingReturnsCount = db.ReturnRequests.Count(r => r.Status == "Pending");

            // ── Orders at risk of going Uncollected within 24h ──
            var urgentDeadline = DateTime.Now.AddHours(24);

            ViewBag.UrgentPickups = db.StoreOrders
                .Include(o => o.Member)
                .Where(o => o.PickupDeadline.HasValue &&
                            o.PickupDeadline.Value <= urgentDeadline)
                .OrderBy(o => o.PickupDeadline)
                .Take(5)
                .ToList();

            return View();
        }

        public ActionResult ChangePassword()
        {
            if (!IsStoreStaffLoggedIn())
                return RedirectToAction("Login", "Admin");

            return View(new ChangePasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(ChangePasswordViewModel model)
        {
            if (!IsStoreStaffLoggedIn())
                return RedirectToAction("Login", "Admin");

            if (!ModelState.IsValid)
                return View(model);

            var staff = GetLoggedInStoreStaff();
            if (staff == null)
                return RedirectToAction("Login", "Admin");

            string hashedCurrent = HashPassword(model.CurrentPassword);
            if (staff.PasswordHash != hashedCurrent)
            {
                ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                return View(model);
            }

            string hashedNew = HashPassword(model.NewPassword);
            if (hashedNew == staff.PasswordHash)
            {
                ModelState.AddModelError("NewPassword", "New password must be different from your current password.");
                return View(model);
            }

            staff.PasswordHash = hashedNew;
            staff.MustChangePassword = false;
            db.SaveChanges();

            TempData["Success"] = "Password changed successfully. Welcome to GymHub!";
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            Session.Remove("LoggedInStoreStaff");
            Session.Remove("LoggedInStoreStaffName");
            Session.Remove("LoggedInStoreStaffCode");
            Session.Remove("LoggedInStoreStaffRole");
            return RedirectToAction("Index", "Home");
        }

        private string HashPassword(string password)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                var sb = new System.Text.StringBuilder();
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
