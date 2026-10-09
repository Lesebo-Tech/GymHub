using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SwiftyS26.Controllers
{
    public class MasterController : Controller
    {
        // GET: Master
        public ActionResult MasterView()
        {
            return View();
        }
        [HttpPost]
        public ActionResult Index()
        {
            return View();
        }
    }
}