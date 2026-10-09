using System;
using System.Linq;
using System.Web.Http;
using SwiftyS26.Models;


namespace SwiftyS26.Controllers
{
    public class RepairRequestApiController : ApiController
    {
        private GymHubDB db = new GymHubDB();

        // POST api/repairrequests
        [HttpPost]
        [Route("api/repairrequests")]
        public IHttpActionResult CreateRepairRequest([FromBody] RepairRequestDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.EquipmentCode))
            {
                return BadRequest("Equipment code is required.");
            }

            var equipment = db.Equipment
                .FirstOrDefault(e => e.EquipmentCode == dto.EquipmentCode);

            if (equipment == null)
            {
                return NotFound();
            }

            var referenceNumber = "RF-" + DateTime.Now.ToString("yyMMdd") + "-" + new Random().Next(1000, 9999);

            var repairRequest = new RepairRequest
            {
                ReferenceNumber = referenceNumber,
                EquipmentCode = dto.EquipmentCode,
                Category = dto.Category,
                Severity = dto.Severity,
                Description = dto.Description,
                PhotoUrl = dto.PhotoUrl,
                Status = "Logged",
                ReportedAt = DateTime.Now
            };

            db.RepairRequests.Add(repairRequest);

            // ── CRITICAL LOGIC: auto-flag equipment Out of Service ──
            if (dto.Severity == "Critical")
            {
                equipment.Status = "OutOfService";
            }

            db.SaveChanges();

            var result = new
            {
                referenceNumber = repairRequest.ReferenceNumber,
                status = repairRequest.Status,
                equipmentCode = repairRequest.EquipmentCode,
                equipmentStatusUpdated = dto.Severity == "Critical"
            };

            return Ok(result);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }

    // DTO — shape of what the mobile app sends
    public class RepairRequestDto
    {
        public string EquipmentCode { get; set; }
        public string Category { get; set; }
        public string Severity { get; set; }
        public string Description { get; set; }
        public string PhotoUrl { get; set; }
    }
}