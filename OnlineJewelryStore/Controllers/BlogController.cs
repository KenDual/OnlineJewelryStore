using OnlineJewelryStore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace OnlineJewelryStore.Controllers
{
    [AllowAnonymous]
    public class BlogController : Controller
    {
        private OnlineJewelryStoreEntities db = new OnlineJewelryStoreEntities();

        // GET: Blog
        public ActionResult Blog()
        {
            var recentProducts = db.Products
                .Where(p => p.IsActive == true)
                .OrderByDescending(p => p.CreationDate)
                .Take(3)
                .ToList();

            var categories = db.Categories
                .Where(c => c.ParentCategoryID == null)
                .OrderBy(c => c.CategoryName)
                .ToList();

            ViewBag.RecentProducts = recentProducts;
            ViewBag.Categories = categories;

            return View();
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}