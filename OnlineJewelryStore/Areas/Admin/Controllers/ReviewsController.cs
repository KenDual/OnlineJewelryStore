using OnlineJewelryStore.Filters;
using OnlineJewelryStore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;

namespace OnlineJewelryStore.Areas.Admin.Controllers
{
    [AdminAuthorize]
    public class ReviewsController : Controller
    {
        private readonly OnlineJewelryStoreEntities db = new OnlineJewelryStoreEntities();

        // GET: Admin/Reviews
        public ActionResult Index(string searchTerm, int? rating, DateTime? fromDate, DateTime? toDate,
                                  string sortOrder, int page = 1, int pageSize = 20)
        {
            ViewBag.CurrentSearch = searchTerm;
            ViewBag.CurrentRating = rating;
            ViewBag.CurrentFromDate = fromDate;
            ViewBag.CurrentToDate = toDate;
            ViewBag.CurrentSort = sortOrder;

            // Sort parameters
            ViewBag.DateSort = string.IsNullOrEmpty(sortOrder) ? "date_asc" : "";
            ViewBag.RatingSort = sortOrder == "rating" ? "rating_desc" : "rating";
            ViewBag.ProductSort = sortOrder == "product" ? "product_desc" : "product";
            ViewBag.CustomerSort = sortOrder == "customer" ? "customer_desc" : "customer";

            // Base query with navigation properties
            var reviews = db.Reviews
                .Include(r => r.User)
                .Include(r => r.Product)
                .Include(r => r.Product.Category)
                .AsQueryable();

            // Search: Product name, Customer name, Review title/body
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim().ToLower();
                reviews = reviews.Where(r =>
                    r.Product.ProductName.ToLower().Contains(searchTerm) ||
                    r.User.FirstName.ToLower().Contains(searchTerm) ||
                    r.User.LastName.ToLower().Contains(searchTerm) ||
                    (r.User.FirstName + " " + r.User.LastName).ToLower().Contains(searchTerm) ||
                    (r.Title != null && r.Title.ToLower().Contains(searchTerm)) ||
                    (r.Body != null && r.Body.ToLower().Contains(searchTerm))
                );
            }

            // Filter by rating
            if (rating.HasValue && rating.Value >= 1 && rating.Value <= 5)
            {
                reviews = reviews.Where(r => r.Rating == rating.Value);
            }

            // Filter by date range
            if (fromDate.HasValue)
            {
                reviews = reviews.Where(r => r.CreatedAt >= fromDate.Value);
            }
            if (toDate.HasValue)
            {
                var endDate = toDate.Value.Date.AddDays(1).AddSeconds(-1);
                reviews = reviews.Where(r => r.CreatedAt <= endDate);
            }

            // Sorting
            switch (sortOrder)
            {
                case "date_asc":
                    reviews = reviews.OrderBy(r => r.CreatedAt);
                    break;
                case "rating":
                    reviews = reviews.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt);
                    break;
                case "rating_desc":
                    reviews = reviews.OrderBy(r => r.Rating).ThenByDescending(r => r.CreatedAt);
                    break;
                case "product":
                    reviews = reviews.OrderBy(r => r.Product.ProductName).ThenByDescending(r => r.CreatedAt);
                    break;
                case "product_desc":
                    reviews = reviews.OrderByDescending(r => r.Product.ProductName).ThenByDescending(r => r.CreatedAt);
                    break;
                case "customer":
                    reviews = reviews.OrderBy(r => r.User.FirstName).ThenBy(r => r.User.LastName).ThenByDescending(r => r.CreatedAt);
                    break;
                case "customer_desc":
                    reviews = reviews.OrderByDescending(r => r.User.FirstName).ThenByDescending(r => r.User.LastName).ThenByDescending(r => r.CreatedAt);
                    break;
                default: // Newest first
                    reviews = reviews.OrderByDescending(r => r.CreatedAt);
                    break;
            }

            // Statistics
            var totalReviews = reviews.Count();
            ViewBag.TotalReviews = totalReviews;

            if (totalReviews > 0)
            {
                ViewBag.AverageRating = reviews.Average(r => (double)r.Rating);
                ViewBag.Rating5Count = reviews.Count(r => r.Rating == 5);
                ViewBag.Rating4Count = reviews.Count(r => r.Rating == 4);
                ViewBag.Rating3Count = reviews.Count(r => r.Rating == 3);
                ViewBag.Rating2Count = reviews.Count(r => r.Rating == 2);
                ViewBag.Rating1Count = reviews.Count(r => r.Rating == 1);
            }
            else
            {
                ViewBag.AverageRating = 0;
                ViewBag.Rating5Count = 0;
                ViewBag.Rating4Count = 0;
                ViewBag.Rating3Count = 0;
                ViewBag.Rating2Count = 0;
                ViewBag.Rating1Count = 0;
            }

            // Pagination
            var totalItems = totalReviews;
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;

            var pagedReviews = reviews
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return View(pagedReviews);
        }

        // GET: Admin/Reviews/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var review = db.Reviews
                .Include(r => r.User)
                .Include(r => r.Product)
                .Include(r => r.Product.Category)
                .Include(r => r.Product.ProductMedias)
                .FirstOrDefault(r => r.ReviewID == id);

            if (review == null)
            {
                return HttpNotFound();
            }

            // Verify if user actually purchased this product (Status = Delivered)
            var hasPurchased = db.Orders
                .Where(o => o.UserID == review.UserID && o.Status == "Delivered")
                .Join(db.OrderItems, o => o.OrderID, oi => oi.OrderID, (o, oi) => oi)
                .Join(db.ProductVariants, oi => oi.VariantID, pv => pv.VariantID, (oi, pv) => pv)
                .Any(pv => pv.ProductID == review.ProductID);

            ViewBag.HasPurchased = hasPurchased;

            // Get all orders of this user for this product
            var userOrders = db.Orders
                .Where(o => o.UserID == review.UserID)
                .Where(o => o.OrderItems.Any(oi => oi.ProductVariant.ProductID == review.ProductID))
                .OrderByDescending(o => o.OrderDate)
                .ToList();

            ViewBag.UserOrders = userOrders;

            // Get user's statistics - FIXED: Pass individual properties instead of anonymous object
            ViewBag.UserTotalReviews = db.Reviews.Count(r => r.UserID == review.UserID);
            ViewBag.UserTotalOrders = db.Orders.Count(o => o.UserID == review.UserID);
            ViewBag.UserDeliveredOrders = db.Orders.Count(o => o.UserID == review.UserID && o.Status == "Delivered");
            ViewBag.UserAverageRating = db.Reviews.Where(r => r.UserID == review.UserID).Any()
                ? db.Reviews.Where(r => r.UserID == review.UserID).Average(r => (double)r.Rating)
                : 0;

            // Get product's review statistics - FIXED: Pass individual properties instead of anonymous object
            ViewBag.ProductTotalReviews = db.Reviews.Count(r => r.ProductID == review.ProductID);
            ViewBag.ProductAverageRating = db.Reviews.Where(r => r.ProductID == review.ProductID).Any()
                ? db.Reviews.Where(r => r.ProductID == review.ProductID).Average(r => (double)r.Rating)
                : 0;
            ViewBag.ProductRating5Count = db.Reviews.Count(r => r.ProductID == review.ProductID && r.Rating == 5);
            ViewBag.ProductRating4Count = db.Reviews.Count(r => r.ProductID == review.ProductID && r.Rating == 4);
            ViewBag.ProductRating3Count = db.Reviews.Count(r => r.ProductID == review.ProductID && r.Rating == 3);
            ViewBag.ProductRating2Count = db.Reviews.Count(r => r.ProductID == review.ProductID && r.Rating == 2);
            ViewBag.ProductRating1Count = db.Reviews.Count(r => r.ProductID == review.ProductID && r.Rating == 1);

            return View(review);
        }

        // GET: Admin/Reviews/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var review = db.Reviews
                .Include(r => r.User)
                .Include(r => r.Product)
                .Include(r => r.Product.Category)
                .FirstOrDefault(r => r.ReviewID == id);

            if (review == null)
            {
                return HttpNotFound();
            }

            return View(review);
        }

        // POST: Admin/Reviews/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            try
            {
                var review = db.Reviews.Find(id);
                if (review == null)
                {
                    return HttpNotFound();
                }

                var productName = review.Product?.ProductName ?? "Unknown Product";
                var customerName = review.User != null
                    ? $"{review.User.FirstName} {review.User.LastName}"
                    : "Unknown Customer";

                db.Reviews.Remove(review);
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Review from {customerName} for '{productName}' has been deleted successfully.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting review: {ex.Message}";
                return RedirectToAction("Delete", new { id = id });
            }
        }

        // GET: Admin/Reviews/ProductReviews/5
        // View all reviews for a specific product
        public ActionResult ProductReviews(int? id, string sortOrder, int page = 1, int pageSize = 10)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var product = db.Products
                .Include(p => p.Category)
                .FirstOrDefault(p => p.ProductID == id);

            if (product == null)
            {
                return HttpNotFound();
            }

            ViewBag.Product = product;
            ViewBag.CurrentSort = sortOrder;

            var reviews = db.Reviews
                .Include(r => r.User)
                .Where(r => r.ProductID == id)
                .AsQueryable();

            // Statistics
            var totalReviews = reviews.Count();
            ViewBag.TotalReviews = totalReviews;

            if (totalReviews > 0)
            {
                ViewBag.AverageRating = reviews.Average(r => (double)r.Rating);
                ViewBag.Rating5Count = reviews.Count(r => r.Rating == 5);
                ViewBag.Rating4Count = reviews.Count(r => r.Rating == 4);
                ViewBag.Rating3Count = reviews.Count(r => r.Rating == 3);
                ViewBag.Rating2Count = reviews.Count(r => r.Rating == 2);
                ViewBag.Rating1Count = reviews.Count(r => r.Rating == 1);
            }

            // Sorting
            switch (sortOrder)
            {
                case "rating_asc":
                    reviews = reviews.OrderBy(r => r.Rating).ThenByDescending(r => r.CreatedAt);
                    break;
                case "rating_desc":
                    reviews = reviews.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt);
                    break;
                case "date_asc":
                    reviews = reviews.OrderBy(r => r.CreatedAt);
                    break;
                default: // Newest first
                    reviews = reviews.OrderByDescending(r => r.CreatedAt);
                    break;
            }

            // Pagination
            var totalItems = totalReviews;
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            var pagedReviews = reviews
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return View(pagedReviews);
        }

        // GET: Admin/Reviews/CustomerReviews/5
        // View all reviews by a specific customer
        public ActionResult CustomerReviews(int? id, string sortOrder, int page = 1, int pageSize = 10)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var user = db.Users.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            ViewBag.User = user;
            ViewBag.CurrentSort = sortOrder;

            var reviews = db.Reviews
                .Include(r => r.Product)
                .Include(r => r.Product.Category)
                .Where(r => r.UserID == id)
                .AsQueryable();

            // Statistics
            var totalReviews = reviews.Count();
            ViewBag.TotalReviews = totalReviews;

            if (totalReviews > 0)
            {
                ViewBag.AverageRating = reviews.Average(r => (double)r.Rating);
                ViewBag.Rating5Count = reviews.Count(r => r.Rating == 5);
                ViewBag.Rating4Count = reviews.Count(r => r.Rating == 4);
                ViewBag.Rating3Count = reviews.Count(r => r.Rating == 3);
                ViewBag.Rating2Count = reviews.Count(r => r.Rating == 2);
                ViewBag.Rating1Count = reviews.Count(r => r.Rating == 1);
            }

            // Sorting
            switch (sortOrder)
            {
                case "rating_asc":
                    reviews = reviews.OrderBy(r => r.Rating).ThenByDescending(r => r.CreatedAt);
                    break;
                case "rating_desc":
                    reviews = reviews.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt);
                    break;
                case "date_asc":
                    reviews = reviews.OrderBy(r => r.CreatedAt);
                    break;
                case "product":
                    reviews = reviews.OrderBy(r => r.Product.ProductName);
                    break;
                default: // Newest first
                    reviews = reviews.OrderByDescending(r => r.CreatedAt);
                    break;
            }

            // Pagination
            var totalItems = totalReviews;
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;

            var pagedReviews = reviews
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return View(pagedReviews);
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