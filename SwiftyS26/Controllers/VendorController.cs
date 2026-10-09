using SwiftyS26.Models;
using System.Linq;
using System.Web.Mvc;

namespace SwiftyS26.Controllers
{
    public class VendorController : Controller
    {
        private GymHubDB db = new GymHubDB();

        private bool IsAdminLoggedIn() => Session["LoggedInAdmin"] != null;

        public ActionResult Index()
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");
            var vendors = db.Vendors.OrderBy(v => v.CompanyName).ToList();
            return View(vendors);  // ← pass it to the view
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Add(string companyName, string contactPerson, string phone, string email, string serviceType)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            if (string.IsNullOrWhiteSpace(companyName) || string.IsNullOrWhiteSpace(phone))
            {
                TempData["Error"] = "Company name and phone are required.";
                return RedirectToAction("Index");
            }

            int next = db.Vendors.Any() ? db.Vendors.Max(v => v.VendorId) + 1 : 1;

            var vendor = new Vendor
            {
                VendorCode = "VEN-" + next.ToString("D4"),
                CompanyName = companyName.Trim(),
                ContactPerson = contactPerson,
                Phone = phone,
                Email = email,
                ServiceType = serviceType,
                IsActive = true
            };

            db.Vendors.Add(vendor);
            db.SaveChanges();

            TempData["Success"] = $"Vendor \"{vendor.CompanyName}\" added.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Deactivate(int id)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            var vendor = db.Vendors.Find(id);
            if (vendor == null)
            {
                TempData["Error"] = "Vendor not found.";
                return RedirectToAction("Index");
            }

            vendor.IsActive = !vendor.IsActive;
            db.Entry(vendor).State = System.Data.Entity.EntityState.Modified;
            db.SaveChanges();

            TempData["Success"] = $"Vendor \"{vendor.CompanyName}\" {(vendor.IsActive ? "reactivated" : "deactivated")}.";
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}