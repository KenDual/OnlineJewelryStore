using OnlineJewelryStore.Filters;
using OnlineJewelryStore.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;

namespace OnlineJewelryStore.Areas.Admin.Controllers
{
    [AdminAuthorize]
    public class ProductsController : Controller
    {
        private OnlineJewelryStoreEntities db = new OnlineJewelryStoreEntities();

        // GET: Admin/Products
        public ActionResult Index(string searchString, int? categoryId, bool? isActive)
        {
            ViewBag.ActiveMenu = "Products";

            // Include all related data
            var products = db.Products
                .Include(p => p.Category)
                .Include(p => p.ProductVariants)
                .Include(p => p.ProductMedias) // ← Nếu lỗi, xem giải pháp bên dưới
                .Include(p => p.Reviews)
                .AsQueryable();

            // Search by name
            if (!string.IsNullOrEmpty(searchString))
            {
                products = products.Where(p => p.ProductName.Contains(searchString));
                ViewBag.SearchString = searchString;
            }

            // Filter by category
            if (categoryId.HasValue)
            {
                products = products.Where(p => p.CategoryID == categoryId.Value);
                ViewBag.CategoryId = categoryId;
            }

            // Filter by active status
            if (isActive.HasValue)
            {
                products = products.Where(p => p.IsActive == isActive.Value);
                ViewBag.IsActive = isActive;
            }

            // Populate category dropdown for filter
            ViewBag.Categories = new SelectList(db.Categories.OrderBy(c => c.CategoryName), "CategoryID", "CategoryName");

            return View(products.OrderByDescending(p => p.CreationDate).ToList());
        }

        // GET: Admin/Products/Details/5
        public ActionResult Details(int? id)
        {
            ViewBag.ActiveMenu = "Products";

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Product product = db.Products
                .Include(p => p.Category)
                .Include(p => p.ProductVariants.Select(v => v.Gemstones))
                .Include(p => p.ProductVariants.Select(v => v.Certifications))
                .Include(p => p.ProductMedias) // ← Nếu lỗi, xem giải pháp bên dưới
                .Include(p => p.Reviews.Select(r => r.User))
                .FirstOrDefault(p => p.ProductID == id);

            if (product == null)
            {
                return HttpNotFound();
            }

            return View(product);
        }

        // GET: Admin/Products/Create
        public ActionResult Create()
        {
            ViewBag.ActiveMenu = "Products";
            PopulateCategoriesDropdown();
            return View();
        }

        // POST: Admin/Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "ProductName,Description,CategoryID,BasePrice,IsActive")] Product product)
        {
            if (ModelState.IsValid)
            {
                // Set default values
                product.CreationDate = DateTime.Now;
                product.IsActive = true;

                db.Products.Add(product);
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Product '{product.ProductName}' has been created successfully!";
                return RedirectToAction("Details", new { id = product.ProductID });
            }

            PopulateCategoriesDropdown(product.CategoryID);
            return View(product);
        }

        // GET: Admin/Products/Edit/5
        public ActionResult Edit(int? id)
        {
            ViewBag.ActiveMenu = "Products";

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Product product = db.Products
                .Include(p => p.Category)
                .Include(p => p.ProductVariants)
                .FirstOrDefault(p => p.ProductID == id);

            if (product == null)
            {
                return HttpNotFound();
            }

            PopulateCategoriesDropdown(product.CategoryID);
            return View(product);
        }

        // POST: Admin/Products/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "ProductID,ProductName,Description,CategoryID,BasePrice,CreationDate,IsActive")] Product product)
        {
            if (ModelState.IsValid)
            {
                db.Entry(product).State = EntityState.Modified;
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Product '{product.ProductName}' has been updated successfully!";
                return RedirectToAction("Details", new { id = product.ProductID });
            }

            PopulateCategoriesDropdown(product.CategoryID);
            return View(product);
        }

        // GET: Admin/Products/Delete/5
        public ActionResult Delete(int? id)
        {
            ViewBag.ActiveMenu = "Products";

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Product product = db.Products
                .Include(p => p.Category)
                .Include(p => p.ProductVariants)
                .Include(p => p.ProductMedias) // ← Nếu lỗi, comment dòng này
                .Include(p => p.Reviews)
                .FirstOrDefault(p => p.ProductID == id);

            if (product == null)
            {
                return HttpNotFound();
            }

            return View(product);
        }

        // POST: Admin/Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Product product = db.Products
                .Include(p => p.ProductVariants)
                .Include(p => p.Reviews)
                .FirstOrDefault(p => p.ProductID == id);

            if (product == null)
            {
                return HttpNotFound();
            }

            // ✅ ĐÚNG: Check OrderItems qua ProductVariants
            var hasOrderItems = product.ProductVariants.Any(v => v.OrderItems.Any());
            if (hasOrderItems)
            {
                var orderCount = product.ProductVariants.SelectMany(v => v.OrderItems).Count();
                TempData["ErrorMessage"] = $"Cannot delete product '{product.ProductName}' because it has {orderCount} order(s). You can deactivate it instead.";
                return RedirectToAction("Index");
            }

            // ✅ ĐÚNG: Check Reviews trực tiếp
            if (product.Reviews != null && product.Reviews.Any())
            {
                TempData["ErrorMessage"] = $"Cannot delete product '{product.ProductName}' because it has {product.Reviews.Count()} review(s).";
                return RedirectToAction("Index");
            }

            // ✅ ĐÚNG: Remove CartItems qua ProductVariants
            var cartItems = product.ProductVariants.SelectMany(v => v.CartItems).ToList();
            if (cartItems.Any())
            {
                db.CartItems.RemoveRange(cartItems);
            }

            // ✅ ĐÚNG: Remove Wishlists qua ProductVariants
            var wishlists = product.ProductVariants.SelectMany(v => v.Wishlists).ToList();
            if (wishlists.Any())
            {
                db.Wishlists.RemoveRange(wishlists);
            }

            // Remove product (cascade delete sẽ xóa variants)
            db.Products.Remove(product);
            db.SaveChanges();

            TempData["SuccessMessage"] = $"Product '{product.ProductName}' has been deleted successfully!";
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Helper Methods

        private void PopulateCategoriesDropdown(int? selectedValue = null)
        {
            var allCategories = db.Categories
                .Include(c => c.Category1)
                .OrderBy(c => c.ParentCategoryID)
                .ThenBy(c => c.CategoryName)
                .ToList();

            var dropdownList = new List<SelectListItem>();
            var mainCategories = allCategories.Where(c => c.ParentCategoryID == null).ToList();

            foreach (var mainCat in mainCategories)
            {
                dropdownList.Add(new SelectListItem
                {
                    Value = mainCat.CategoryID.ToString(),
                    Text = mainCat.CategoryName,
                    Selected = selectedValue.HasValue && selectedValue.Value == mainCat.CategoryID
                });

                var subCategories = allCategories.Where(c => c.ParentCategoryID == mainCat.CategoryID).ToList();
                foreach (var subCat in subCategories)
                {
                    dropdownList.Add(new SelectListItem
                    {
                        Value = subCat.CategoryID.ToString(),
                        Text = "  └─ " + subCat.CategoryName,
                        Selected = selectedValue.HasValue && selectedValue.Value == subCat.CategoryID
                    });
                }
            }

            ViewBag.CategoryID = dropdownList;
        }

        #endregion
    }
}