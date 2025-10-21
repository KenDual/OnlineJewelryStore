using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace OnlineJewelryStore.Controllers
{
    public class FeatureController : Controller
    {
        // GET: Feature
        public ActionResult Home()
        {
            ViewBag.Active = "Home"; 
            return View();
        }

        [HttpGet]
        public ActionResult Shop()
        {
            ViewBag.Active = "Shop"; 
            return View();
        }

        [HttpGet]
        public ActionResult Wishlist()
        {
            ViewBag.Active = "Wishlist"; 
            return View();
        }

        [HttpGet]
        public ActionResult Blog()
        {
            ViewBag.Active = "Blog"; 
            return View();
        }

        [HttpGet]
        public ActionResult Contact_us()
        {
            ViewBag.Active = "Contact_us"; 
            return View();
        }
        [HttpGet]
        public ActionResult Account()
        {
            ViewBag.Active = "Account"; 
            return View();
        }

        [HttpGet]
        public ActionResult Cart()
        {
            ViewBag.Active = "Cart";
            return View();
        }
    }
}
