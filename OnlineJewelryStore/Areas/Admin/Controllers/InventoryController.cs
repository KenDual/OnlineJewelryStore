using OnlineJewelryStore.Filters;
using OnlineJewelryStore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;


namespace OnlineJewelryStore.Areas.Admin.Controllers
{
    [AdminAuthorize]
    public class InventoryController : Controller
    {
        private readonly OnlineJewelryStoreEntities db = new OnlineJewelryStoreEntities();

        // GET: Admin/Inventory
        public ActionResult Index(string search, int? categoryId, string stockStatus, string sortBy, int page = 1)
        {
            // Base query với eager loading
            var query = db.ProductVariants
                .Include(v => v.Product)
                .Include(v => v.Product.Category)
                .Where(v => v.Product.IsActive == true) // Chỉ hiển thị products active
                .AsQueryable();

            // 1. SEARCH - Tìm theo product name hoặc SKU
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(v =>
                    v.Product.ProductName.ToLower().Contains(search) ||
                    v.SKU.ToLower().Contains(search)
                );
            }

            // 2. FILTER BY CATEGORY
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(v => v.Product.CategoryID == categoryId.Value);
            }

            // 3. FILTER BY STOCK STATUS
            switch (stockStatus)
            {
                case "low":
                    query = query.Where(v => v.StockQuantity > 0 && v.StockQuantity < 10);
                    break;
                case "out":
                    query = query.Where(v => v.StockQuantity == 0);
                    break;
                case "in":
                    query = query.Where(v => v.StockQuantity >= 10);
                    break;
                    // "all" hoặc null = không filter
            }

            // 4. SORT
            if (sortBy == "stock_asc")
            {
                query = query.OrderBy(v => v.StockQuantity);
            }
            else if (sortBy == "stock_desc")
            {
                query = query.OrderByDescending(v => v.StockQuantity);
            }
            else if (sortBy == "name_asc")
            {
                query = query.OrderBy(v => v.Product.ProductName).ThenBy(v => v.SKU);
            }
            else if (sortBy == "name_desc")
            {
                query = query.OrderByDescending(v => v.Product.ProductName).ThenByDescending(v => v.SKU);
            }
            else if (sortBy == "sku")
            {
                query = query.OrderBy(v => v.SKU);
            }
            else
            {
                // default
                query = query.OrderBy(v => v.Product.ProductName).ThenBy(v => v.SKU);
            }

            // 5. SUMMARY STATISTICS (trước khi phân trang)
            var allVariants = query.ToList(); // Execute query

            ViewBag.TotalVariants = allVariants.Count;
            ViewBag.LowStockCount = allVariants.Count(v => v.StockQuantity > 0 && v.StockQuantity < 10);
            ViewBag.OutOfStockCount = allVariants.Count(v => v.StockQuantity == 0);
            ViewBag.TotalStockQuantity = allVariants.Sum(v => v.StockQuantity);

            // Tính tổng giá trị kho (BasePrice + AdditionalPrice) * Stock
            ViewBag.TotalStockValue = allVariants.Sum(v =>
                (v.Product.BasePrice + v.AdditionalPrice) * v.StockQuantity
            );

            // 6. PAGINATION
            int pageSize = 20;
            int totalItems = allVariants.Count;
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;

            var pagedVariants = allVariants
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // 7. DROPDOWN DATA
            ViewBag.Categories = db.Categories
                .OrderBy(c => c.CategoryName)
                .ToList();

            // 8. PRESERVE FILTER VALUES
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentCategoryId = categoryId;
            ViewBag.CurrentStockStatus = stockStatus;
            ViewBag.CurrentSortBy = sortBy;

            return View(pagedVariants);
        }

        // POST: Admin/Inventory/UpdateStock (AJAX)
        [HttpPost]
        public JsonResult UpdateStock(int variantId, int newStock)
        {
            try
            {
                // Validation
                if (newStock < 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Stock quantity cannot be negative!"
                    });
                }

                // Find variant
                var variant = db.ProductVariants
                    .Include(v => v.Product)
                    .FirstOrDefault(v => v.VariantID == variantId);

                if (variant == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Variant not found!"
                    });
                }

                // Lưu old stock để log
                int oldStock = variant.StockQuantity;

                // Update stock
                variant.StockQuantity = newStock;
                db.SaveChanges();

                // Determine stock status
                string stockStatus = newStock == 0 ? "out" : (newStock < 10 ? "low" : "in");
                string statusBadge = newStock == 0
                    ? "<span class='badge bg-danger'>Out of Stock</span>"
                    : (newStock < 10
                        ? "<span class='badge bg-warning text-dark'>Low Stock</span>"
                        : "<span class='badge bg-success'>In Stock</span>");

                return Json(new
                {
                    success = true,
                    message = $"Stock updated: {oldStock} → {newStock}",
                    newStock = newStock,
                    stockStatus = stockStatus,
                    statusBadge = statusBadge,
                    productName = variant.Product.ProductName,
                    sku = variant.SKU
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error: " + ex.Message
                });
            }
        }

        // GET: Admin/Inventory/LowStock
        public ActionResult LowStock()
        {
            var lowStockVariants = db.ProductVariants
                .Include(v => v.Product)
                .Include(v => v.Product.Category)
                .Where(v => v.Product.IsActive == true
                         && v.StockQuantity > 0
                         && v.StockQuantity < 10)
                .OrderBy(v => v.StockQuantity)
                .ThenBy(v => v.Product.ProductName)
                .ToList();

            ViewBag.Title = "Low Stock Alert";
            ViewBag.AlertType = "low";

            return View("StockAlert", lowStockVariants);
        }

        // GET: Admin/Inventory/OutOfStock
        public ActionResult OutOfStock()
        {
            var outOfStockVariants = db.ProductVariants
                .Include(v => v.Product)
                .Include(v => v.Product.Category)
                .Where(v => v.Product.IsActive == true
                         && v.StockQuantity == 0)
                .OrderBy(v => v.Product.ProductName)
                .ToList();

            ViewBag.Title = "Out of Stock Alert";
            ViewBag.AlertType = "out";

            return View("StockAlert", outOfStockVariants);
        }

        // GET: Admin/Inventory/GetStockCounts (AJAX for sidebar badges)
        public JsonResult GetStockCounts()
        {
            var lowStockCount = db.ProductVariants
                .Count(v => v.Product.IsActive == true
                         && v.StockQuantity > 0
                         && v.StockQuantity < 10);

            var outOfStockCount = db.ProductVariants
                .Count(v => v.Product.IsActive == true
                         && v.StockQuantity == 0);

            return Json(new
            {
                lowStockCount = lowStockCount,
                outOfStockCount = outOfStockCount
            }, JsonRequestBehavior.AllowGet);
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