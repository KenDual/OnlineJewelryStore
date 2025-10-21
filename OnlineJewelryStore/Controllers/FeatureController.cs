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

        [Authorize]
        public ActionResult Wishlist()
        {
            ViewBag.Active = "Wishlist"; 
            return View();
        }

        public ActionResult Blog()
        {
            ViewBag.Active = "Blog"; 
            return View();
        }

        public ActionResult Contact_us()
        {
            ViewBag.Active = "Contact_us"; 
            return View();
        }

        [Authorize]
        public ActionResult Account()
        {
            ViewBag.Active = "Account"; 
            return View();
        }

        [Authorize]
        public ActionResult Cart()
        {
            ViewBag.Active = "Cart";
            return View();
        }
    }
}
