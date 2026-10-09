using QRCoder;
using SwiftyS26.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SwiftyS26.Controllers
{
    public class EquipmentController : Controller
    {
        private GymHubDB db = new GymHubDB();

        private bool IsAdminLoggedIn() => Session["LoggedInAdmin"] != null;

        // ============================================================
        // EQUIPMENT LIST
        // ============================================================
        public ActionResult Index()
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            // ── Fully-automatic maintenance assignment — no admin click
            // required. Runs lazily on page load; skips equipment that's
            // already assigned, so this is safe to call every time. ──
            //MaintenanceHelper.AutoAssignDueMaintenance(db);
            InspectionHelper.AutoAssignDueInspection(db);

            var vm = new EquipmentManagementViewModel
            {
                AllEquipment = db.Equipment
                    .Where(e => !e.IsDeleted)
                    .OrderByDescending(e => e.RegisteredAt)
                    .ToList(),

                DueForInspection = db.Equipment
                    .Include(e => e.PendingInspectionTechnician)
                    .Where(e => !e.IsDeleted
                             && e.Status != "Retired"
                             && e.NextInspectionDueDate != null
                             && e.NextInspectionDueDate <= DateTime.Today)
                    .OrderBy(e => e.NextInspectionDueDate)
                    .ToList(),

                InspectionHistory = db.Inspections
                    .Include(i => i.Equipment)
                    .Include(i => i.Technician)
                    .Where(i => !i.Equipment.IsDeleted)
                    .OrderByDescending(i => i.InspectionDate)
                    .Take(100)
                    .ToList(),

                OpenRepairs = db.Repairs
                    .Include(r => r.Equipment)
                    .Include(r => r.Technician)
                    .Where(r => !r.Equipment.IsDeleted
                             && RepairHelper.OpenStatuses.Contains(r.RepairStatus))
                    .OrderByDescending(r => r.ReportedDate)
                    .ToList(),
                Vendors = db.Vendors.Where(v => v.IsActive).OrderBy(v => v.CompanyName).ToList(),

                Technicians = db.Technicians.ToList(),

                // ── THE FIX — this was missing entirely from the real controller,
                // which is why nothing ever showed up in Inspections Due no matter
                // how many repairs sat at AwaitingInspection in the database.
                // Covers BOTH technician-completed and vendor-completed repairs,
                // since both routes converge on RepairStatus == "AwaitingInspection". ──
                AwaitingInspectionRepairs = db.Repairs
                    .Include(r => r.Equipment)
                    .Include(r => r.Technician)
                    .Include(r => r.VerificationTechnician)
                    .Where(r => !r.Equipment.IsDeleted && r.RepairStatus == "AwaitingInspection")
                    .OrderByDescending(r => r.ReportedDate)
                    .ToList(),

                DueForMaintenance = db.Equipment
                    .Include(e => e.PendingMaintenanceTechnician)
                    .Where(e => !e.IsDeleted
                             && e.Status != "Retired"
                             && e.NextMaintenanceDueDate != null
                             && e.NextMaintenanceDueDate <= DateTime.Today)
                    .OrderBy(e => e.NextMaintenanceDueDate)
                    .ToList(),

                MaintenanceHistory = db.Maintenances
                    .Include(m => m.Equipment)
                    .Include(m => m.Technician)
                    .Where(m => !m.Equipment.IsDeleted)
                    .OrderByDescending(m => m.MaintenanceDate)
                    .Take(100)
                    .ToList()
            };

            // ── Phase 3: technician recommendation for repairs still awaiting assignment ──
            var allTechnicians = db.Technicians.ToList();
            var activeAssignments = db.RepairAssignments
                .Where(a => a.AssignmentStatus == "Assigned" || a.AssignmentStatus == "Accepted")
                .ToList();


            vm.RecommendedTechnicians = vm.OpenRepairs
                .Where(r => r.RepairStatus == "Pending")
                .ToDictionary(
                    r => r.RepairId,
                    r => RepairHelper.GetRecommendedTechnician(r.Equipment, allTechnicians, activeAssignments)

                );

            return View(vm);
        }
        // ============================================================
        // REGISTER EQUIPMENT — POST
        // (Steps 1-2 of Equipment Registration: purchase details captured,
        //  then registered with generated code + QR)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(Equipment equipment, HttpPostedFileBase imageFile)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill in all required fields correctly.";
                return RedirectToAction("Index");
            }

            int next = db.Equipment.Any()
                ? db.Equipment.Max(e => e.EquipmentId) + 1
                : 1;
            equipment.EquipmentCode = "EQ-" + next.ToString("D4");

            equipment.RegisteredAt = DateTime.Now;
            equipment.Status = "Available";

            if (equipment.InspectionIntervalDays <= 0) equipment.InspectionIntervalDays = 30;
            if (equipment.MaintenanceIntervalDays <= 0) equipment.MaintenanceIntervalDays = 90;
            equipment.NextInspectionDueDate = DateTime.Today.AddDays(equipment.InspectionIntervalDays);
            equipment.NextMaintenanceDueDate = DateTime.Today.AddDays(equipment.MaintenanceIntervalDays);

            // ── NEW: handle the uploaded image ──────────────────────────
            if (imageFile != null && imageFile.ContentLength > 0)
            {
                equipment.ImagePath = SaveEquipmentImage(imageFile, equipment.EquipmentCode);
            }

            db.Equipment.Add(equipment);
            db.SaveChanges();

            equipment.QrCodeBase64 = GenerateEquipmentQrBase64(equipment.EquipmentCode, equipment.Name, equipment.GymArea);
            db.Entry(equipment).State = EntityState.Modified;
            db.SaveChanges();

            TempData["Success"] = $"Equipment \"{equipment.Name}\" registered as {equipment.EquipmentCode} and marked Available.";
            return RedirectToAction("Index");
        }

        // ============================================================
        // HELPER — saves the uploaded image to /Content/uploads/equipment/
        // and returns the relative path to store in the DB
        // ============================================================
        private string SaveEquipmentImage(HttpPostedFileBase file, string equipmentCode)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(ext))
                return null; // silently skip invalid files — or set TempData["Error"] if you'd rather block it

            var folder = Server.MapPath("~/Content/uploads/equipment/");
            if (!System.IO.Directory.Exists(folder))
                System.IO.Directory.CreateDirectory(folder);

            var fileName = $"{equipmentCode}_{Guid.NewGuid().ToString("N").Substring(0, 8)}{ext}";
            var fullPath = System.IO.Path.Combine(folder, fileName);
            file.SaveAs(fullPath);

            return "/Content/uploads/equipment/" + fileName;
        }

        // ============================================================
        // HELPER — QR generation (mirrors MemberHelper's approach)
        // ============================================================
        private string GenerateEquipmentQrBase64(string equipmentCode, string name, string gymArea)
        {
            string qrContent = $"GYMHUB EQUIPMENT | Code: {equipmentCode} | Name: {name} | Area: {gymArea}";

            using (var qrGenerator = new QRCodeGenerator())
            using (var qrData = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q))
            using (var qrCode = new QRCode(qrData))
            using (var bitmap = qrCode.GetGraphic(10))
            using (var ms = new System.IO.MemoryStream())
            {
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        public ActionResult RegenerateAllEquipmentQr()
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            var equipmentWithoutQr = db.Equipment
                .Where(e => !e.IsDeleted && (e.QrCodeBase64 == null || e.QrCodeBase64 == ""))
                .ToList();

            foreach (var e in equipmentWithoutQr)
            {
                e.QrCodeBase64 = GenerateEquipmentQrBase64(e.EquipmentCode, e.Name, e.GymArea);
                db.Entry(e).State = EntityState.Modified;
            }

            db.SaveChanges();

            TempData["Success"] = $"Generated QR codes for {equipmentWithoutQr.Count} equipment records.";
            return RedirectToAction("Index");
        }

        // ============================================================
        // VERIFY EQUIPMENT QR — called from the member-side scanner
        // GET/POST: /Equipment/VerifyEquipmentQr
        // Member-facing, so no admin check — just requires a logged-in member.
        // ============================================================
        [HttpPost]
        public ActionResult VerifyEquipmentQr(string qrData)
        {
            if (Session["LoggedInMember"] == null)
                return Json(new { success = false, message = "Please log in first." });

            if (string.IsNullOrWhiteSpace(qrData))
                return Json(new { success = false, message = "No QR data received." });

            // Expected format: "GYMHUB EQUIPMENT | Code: EQ-0001 | Name: Treadmill | Area: Cardio Zone"
            string code = ExtractField(qrData, "Code:");
            if (string.IsNullOrWhiteSpace(code))
                return Json(new { success = false, message = "Unrecognised QR code. Please scan a valid GymHub equipment code." });

            var equipment = db.Equipment.FirstOrDefault(e => e.EquipmentCode == code && !e.IsDeleted);
            if (equipment == null)
                return Json(new { success = false, message = $"No equipment found for code {code}." });

            if (equipment.Status == "Retired")
                return Json(new { success = false, message = $"\"{equipment.Name}\" has been retired and can no longer be reported." });

            return Json(new
            {
                success = true,
                equipmentId = equipment.EquipmentId,
                equipmentCode = equipment.EquipmentCode,
                name = equipment.Name,
                category = equipment.Category,
                gymArea = equipment.GymArea,
                status = equipment.Status,
                statusDisplay = equipment.StatusDisplay
            });
        }

        // ── HELPER — pull a "Key: value" segment out of the pipe-delimited QR string ──
        private string ExtractField(string qrData, string key)
        {
            var parts = qrData.Split('|');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.StartsWith(key))
                    return trimmed.Substring(key.Length).Trim();
            }
            return null;
        }

        // ============================================================
        // EDIT EQUIPMENT — POST
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Equipment equipment, HttpPostedFileBase imageFile)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            var existing = db.Equipment.Find(equipment.EquipmentId);
            if (existing == null)
            {
                TempData["Error"] = "Equipment not found.";
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill in all required fields correctly.";
                return RedirectToAction("Index");
            }

            existing.Name = equipment.Name;
            existing.Category = equipment.Category;
            existing.Brand = equipment.Brand;
            existing.Model = equipment.Model;
            existing.SerialNumber = equipment.SerialNumber;
            existing.GymArea = equipment.GymArea;
            existing.PurchaseDate = equipment.PurchaseDate;
            existing.PurchaseCost = equipment.PurchaseCost;
            existing.VendorName = equipment.VendorName;
            existing.VendorContact = equipment.VendorContact;
            existing.WarrantyExpiryDate = equipment.WarrantyExpiryDate;
            existing.Notes = equipment.Notes;
            existing.Status = equipment.Status;

            if (imageFile != null && imageFile.ContentLength > 0)
            {
                existing.ImagePath = SaveEquipmentImage(imageFile, existing.EquipmentCode);
            }

            if (equipment.InspectionIntervalDays > 0 && equipment.InspectionIntervalDays != existing.InspectionIntervalDays)
            {
                existing.InspectionIntervalDays = equipment.InspectionIntervalDays;
                existing.NextInspectionDueDate = DateTime.Today.AddDays(equipment.InspectionIntervalDays);
            }

            if (equipment.MaintenanceIntervalDays > 0 && equipment.MaintenanceIntervalDays != existing.MaintenanceIntervalDays)
            {
                existing.MaintenanceIntervalDays = equipment.MaintenanceIntervalDays;
                existing.NextMaintenanceDueDate = DateTime.Today.AddDays(equipment.MaintenanceIntervalDays);
            }

            db.Entry(existing).State = EntityState.Modified;
            db.SaveChanges();

            TempData["Success"] = $"Equipment \"{existing.Name}\" ({existing.EquipmentCode}) updated.";
            return RedirectToAction("Index");
        }


        // ============================================================
        // DELETE EQUIPMENT — POST
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            if (!IsAdminLoggedIn())
                return RedirectToAction("Login", "Admin");

            var equipment = db.Equipment.Find(id);
            if (equipment == null)
            {
                TempData["Error"] = "Equipment not found.";
                return RedirectToAction("Index");
            }

            equipment.IsDeleted = true;
            equipment.DeletedAt = DateTime.Now;

            db.Entry(equipment).State = EntityState.Modified;
            db.SaveChanges();

            TempData["Success"] = $"Equipment \"{equipment.Name}\" ({equipment.EquipmentCode}) removed.";
            return RedirectToAction("Index");
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
