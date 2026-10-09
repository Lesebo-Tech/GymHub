//using System.Linq;
//using System.Web.Http;
//using SwiftyS26.Models;

//namespace SwiftyS26.Controllers
//{
//    public class EquipmentApiController : ApiController
//    {
//        private GymHubDB db = new GymHubDB();

//        //// GET api/equipment/EQ-0001
//        //[HttpGet]
//        //[Route("api/equipment/{code}")]
//        //public IHttpActionResult GetEquipment(string code)
//        //{
//        //    var equipment = db.Equipment
//        //        .FirstOrDefault(e => e.EquipmentCode == code);

//        //    if (equipment == null)
//        //    {
//        //        return NotFound();
//        //    }

//        //    var result = new
//        //    {
//        //        equipmentId = equipment.EquipmentId,
//        //        equipmentCode = equipment.EquipmentCode,
//        //        name = equipment.Name,
//        //        category = equipment.Category,
//        //        brand = equipment.Brand,
//        //        model = equipment.Model,
//        //        gymArea = equipment.GymArea,
//        //        status = equipment.Status,
//        //        statusDisplay = equipment.StatusDisplay
//        //    };

//        //    return Ok(result);
//        //}

//        [HttpGet]
//        [Route("api/equipment/{code}")]
//        public IHttpActionResult GetEquipment(string code)
//        {
//            var totalEquipment = db.Equipment.Count();

//            var equipment = db.Equipment
//                .FirstOrDefault(e => e.EquipmentCode == code);

//            if (equipment == null)
//            {
//                return Ok(new
//                {
//                    searched = code,
//                    totalEquipment = totalEquipment,
//                    found = false
//                });
//            }

//            return Ok(new
//            {
//                found = true,
//                searched = code,
//                totalEquipment = totalEquipment,

//                equipmentId = equipment.EquipmentId,
//                equipmentCode = equipment.EquipmentCode,
//                name = equipment.Name
//            });
//        }

//        protected override void Dispose(bool disposing)
//        {
//            if (disposing)
//            {
//                db.Dispose();
//            }
//            base.Dispose(disposing);
//        }
//    }
//}

using System.Web.Http;

namespace SwiftyS26.Controllers
{
    public class EquipmentApiController : ApiController
    {
        // ==========================================================
        // TEST API
        // This does NOT touch the database.
        // It only proves whether Android is reaching the API.
        // ==========================================================
        [HttpGet]
        [Route("api/equipment/{code}")]
        public IHttpActionResult GetEquipment(string code)
        {
            return Ok(new
            {
                apiReached = true,
                searched = code
            });
        }
    }
}