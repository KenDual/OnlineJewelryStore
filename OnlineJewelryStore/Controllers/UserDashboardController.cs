using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace OnlineJewelryStore.Controllers
{
    using System.Web.Mvc;
    namespace OnlineJewelryStore.Controllers
    {
        public class UserDashboardController : Controller
        {
            [HttpGet]
            public ActionResult Index()
            {
                ViewBag.Active = "Dashboard"; return View();
            }
            [HttpGet]
            public ActionResult Orders()
            {
                ViewBag.Active = "Orders"; return View();
            }
            [HttpGet]
            public ActionResult PaymentMethod()
            { 
                ViewBag.Active = "payment"; return View(); 
            }
            [HttpGet] 
            public ActionResult Address() 
            { 
                ViewBag.Active = "address"; return View(); 
            }
            [HttpGet] 
            public ActionResult AccountDetails() 
            { 
                ViewBag.Active = "account"; return View(); 
            }
        }
    }
}