using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace OnlineJewelryStore.Controllers
{
    using global::OnlineJewelryStore.Models;
    using System.Web.Mvc;
    namespace OnlineJewelryStore.Controllers
    {
        [Authorize]
        public class UserDashboardController : Controller
        {
            private OnlineJewelryStoreEntities db = new OnlineJewelryStoreEntities();

            [HttpGet]
            public ActionResult Index()
            {
                ViewBag.Active = "Dashboard"; 
                return View();
            }
            [HttpGet]
            public ActionResult Orders()
            {
                ViewBag.Active = "Orders"; 
                return View();
            }
            [HttpGet]
            public ActionResult PaymentMethod()
            { 
                ViewBag.Active = "payment"; 
                return View(); 
            }
            [HttpGet] 
            public ActionResult Address() 
            { 
                ViewBag.Active = "address"; 
                return View(); 
            }

            // GET: UserDashboard/AccountDetails
            public ActionResult AccountDetails()
            {
                if (Session["UserID"] == null) return RedirectToAction("Login", "Account");
                int userId = (int)Session["UserID"];
                var user = db.Users.Find(userId);
                if (user == null) return HttpNotFound();

                ViewBag.DisplayName = string.Format("{0} {1}", user.LastName, user.FirstName).Trim();
                return View(user);
            }

            [HttpPost]
            [ValidateAntiForgeryToken]
            public ActionResult AccountDetails(User form)
            {
                if (Session["UserID"] == null) return RedirectToAction("Login", "Account");

                var user = db.Users.Find(form.UserID);
                if (user == null) return HttpNotFound();

                user.FirstName = form.FirstName;
                user.LastName = form.LastName;
                db.SaveChanges();

                // Cập nhật tên hiển thị trong session = LastName + FirstName
                Session["UserName"] = (user.LastName + " " + user.FirstName).Trim();

                TempData["SuccessMessage"] = "Cập nhật thông tin thành công!";
                return RedirectToAction("AccountDetails");
            }
        }
    }
}