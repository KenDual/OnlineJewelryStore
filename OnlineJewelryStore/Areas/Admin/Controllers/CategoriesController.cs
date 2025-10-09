using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using OnlineJewelryStore.Models;

namespace OnlineJewelryStore.Areas.Admin.Controllers
{
    public class CategoriesController : Controller
    {
        private OnlineJewelryStoreEntities db = new OnlineJewelryStoreEntities();

        // GET: Admin/Categories
        public ActionResult Index()
        {
            var categories = db.Categories
                .Include(c => c.Category1)
                .Include(c => c.Categories1)
                .Include(c => c.Products)
                .OrderBy(c => c.ParentCategoryID)
                .ThenBy(c => c.CategoryName)
                .ToList();

            return View(categories);
        }

        // GET: Admin/Categories/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Category category = db.Categories
                .Include(c => c.Category1)
                .Include(c => c.Categories1)
                .Include(c => c.Products)
                .FirstOrDefault(c => c.CategoryID == id);

            if (category == null)
            {
                return HttpNotFound();
            }

            return View(category);
        }

        // GET: Admin/Categories/Create
        public ActionResult Create()
        {
            PopulateParentCategoriesDropdown();
            return View();
        }

        // POST: Admin/Categories/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "CategoryID,CategoryName,ParentCategoryID")] Category category)
        {
            if (ModelState.IsValid)
            {
                db.Categories.Add(category);
                db.SaveChanges();
                TempData["SuccessMessage"] = "Category created successfully!";
                return RedirectToAction("Index");
            }

            PopulateParentCategoriesDropdown(category.ParentCategoryID);
            return View(category);
        }

        // GET: Admin/Categories/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Category category = db.Categories
                .Include(c => c.Categories1)
                .FirstOrDefault(c => c.CategoryID == id);

            if (category == null)
            {
                return HttpNotFound();
            }

            PopulateParentCategoriesDropdown(category.ParentCategoryID, category.CategoryID);
            return View(category);
        }

        // POST: Admin/Categories/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "CategoryID,CategoryName,ParentCategoryID")] Category category)
        {
            if (ModelState.IsValid)
            {
                // Prevent circular reference
                if (category.ParentCategoryID.HasValue &&
                    IsCircularReference(category.CategoryID, category.ParentCategoryID.Value))
                {
                    ModelState.AddModelError("ParentCategoryID", "Cannot set parent to itself or its sub-categories (circular reference).");
                    PopulateParentCategoriesDropdown(category.ParentCategoryID, category.CategoryID);
                    return View(category);
                }

                db.Entry(category).State = EntityState.Modified;
                db.SaveChanges();
                TempData["SuccessMessage"] = "Category updated successfully!";
                return RedirectToAction("Index");
            }

            PopulateParentCategoriesDropdown(category.ParentCategoryID, category.CategoryID);
            return View(category);
        }

        // GET: Admin/Categories/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Category category = db.Categories
                .Include(c => c.Category1)
                .Include(c => c.Categories1)
                .Include(c => c.Products)
                .FirstOrDefault(c => c.CategoryID == id);

            if (category == null)
            {
                return HttpNotFound();
            }

            return View(category);
        }

        // POST: Admin/Categories/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Category category = db.Categories
                .Include(c => c.Categories1)
                .Include(c => c.Products)
                .FirstOrDefault(c => c.CategoryID == id);

            if (category == null)
            {
                return HttpNotFound();
            }

            // Check if category has sub-categories
            if (category.Categories1.Any())
            {
                TempData["ErrorMessage"] = $"Cannot delete category '{category.CategoryName}' because it has {category.Categories1.Count()} sub-categories. Please delete sub-categories first.";
                return RedirectToAction("Index");
            }

            // Check if category has products
            if (category.Products.Any())
            {
                TempData["ErrorMessage"] = $"Cannot delete category '{category.CategoryName}' because it has {category.Products.Count()} products. Please reassign or delete products first.";
                return RedirectToAction("Index");
            }

            db.Categories.Remove(category);
            db.SaveChanges();
            TempData["SuccessMessage"] = "Category deleted successfully!";
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

        /// <summary>
        /// Populate dropdown with hierarchical display
        /// </summary>
        private void PopulateParentCategoriesDropdown(int? selectedValue = null, int? excludeCategoryId = null)
        {
            var allCategories = db.Categories
                .Include(c => c.Category1)
                .OrderBy(c => c.ParentCategoryID)
                .ThenBy(c => c.CategoryName)
                .ToList();

            var dropdownList = new List<SelectListItem>();

            // Get main categories
            var mainCategories = allCategories.Where(c => c.ParentCategoryID == null).ToList();

            foreach (var mainCat in mainCategories)
            {
                // Exclude current category and its descendants
                if (excludeCategoryId.HasValue &&
                    (mainCat.CategoryID == excludeCategoryId.Value ||
                     IsDescendant(mainCat.CategoryID, excludeCategoryId.Value, allCategories)))
                {
                    continue;
                }

                // Add main category
                dropdownList.Add(new SelectListItem
                {
                    Value = mainCat.CategoryID.ToString(),
                    Text = mainCat.CategoryName,
                    Selected = selectedValue.HasValue && selectedValue.Value == mainCat.CategoryID
                });

                // Add sub-categories with indentation
                var subCategories = allCategories.Where(c => c.ParentCategoryID == mainCat.CategoryID).ToList();
                foreach (var subCat in subCategories)
                {
                    if (excludeCategoryId.HasValue &&
                        (subCat.CategoryID == excludeCategoryId.Value ||
                         IsDescendant(subCat.CategoryID, excludeCategoryId.Value, allCategories)))
                    {
                        continue;
                    }

                    dropdownList.Add(new SelectListItem
                    {
                        Value = subCat.CategoryID.ToString(),
                        Text = "  └─ " + subCat.CategoryName,
                        Selected = selectedValue.HasValue && selectedValue.Value == subCat.CategoryID
                    });
                }
            }

            ViewBag.ParentCategories = dropdownList;
        }

        /// <summary>
        /// Check if setting parentId as parent of categoryId would create circular reference
        /// </summary>
        private bool IsCircularReference(int categoryId, int parentId)
        {
            if (categoryId == parentId)
                return true;

            var allCategories = db.Categories.ToList();
            return IsDescendant(parentId, categoryId, allCategories);
        }

        /// <summary>
        /// Check if potentialDescendantId is a descendant of ancestorId
        /// </summary>
        private bool IsDescendant(int potentialDescendantId, int ancestorId, List<Category> allCategories)
        {
            var current = allCategories.FirstOrDefault(c => c.CategoryID == potentialDescendantId);

            while (current != null && current.ParentCategoryID.HasValue)
            {
                if (current.ParentCategoryID.Value == ancestorId)
                    return true;

                current = allCategories.FirstOrDefault(c => c.CategoryID == current.ParentCategoryID.Value);
            }

            return false;
        }

        #endregion
    }
}