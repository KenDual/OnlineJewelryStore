using OnlineJewelryStore.Models;
using OnlineJewelryStore.ViewModels.Shop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;
using System.Web.Mvc;


namespace OnlineJewelryStore.Controllers
{
    public class ShopController : Controller
    {
        private OnlineJewelryStoreEntities db = new OnlineJewelryStoreEntities();

        public ActionResult Index(
            int? categoryId,
            decimal? minPrice,      // Filter by min price
            decimal? maxPrice,      // Filter by max price
            string metalType,       // Filter by metal type
            string sortBy = "newest", // Sort option
            int page = 1)           // Current page
        {
            const int pageSize = 20;

            // ===== QUERY CHÍNH =====
            // JOIN Products với Categories và ProductVariants
            var query = from p in db.Products
                        join c in db.Categories on p.CategoryID equals c.CategoryID
                        where p.IsActive == true
                        select new
                        {
                            Product = p,
                            Category = c,
                            MinAdditionalPrice = p.ProductVariants.Any()
                                ? p.ProductVariants.Min(v => v.AdditionalPrice)
                                : 0,
                            MainImageURL = p.ProductMedias
                                .Where(m => m.IsMain == true)
                                .Select(m => m.URL)
                                .FirstOrDefault(),
                            HasStock = p.ProductVariants.Any(v => v.StockQuantity > 0),
                            AvailableVariantsCount = p.ProductVariants.Count(v => v.StockQuantity > 0)
                        };

            // ===== APPLY FILTERS =====

            // Filter by Category
            if (categoryId.HasValue)
            {
                query = query.Where(x => x.Product.CategoryID == categoryId.Value);
            }

            // Filter by Metal Type
            if (!string.IsNullOrEmpty(metalType))
            {
                query = query.Where(x => x.Product.ProductVariants
                    .Any(v => v.MetalType == metalType && v.StockQuantity > 0));
            }

            // Chỉ hiển thị sản phẩm có stock
            query = query.Where(x => x.HasStock == true);

            // ===== PROJECT TO VIEWMODEL =====
            var productsQuery = query.Select(x => new ProductListItemViewModel
            {
                ProductID = x.Product.ProductID,
                ProductName = x.Product.ProductName,
                Description = x.Product.Description,
                CategoryID = x.Category.CategoryID,
                CategoryName = x.Category.CategoryName,
                BasePrice = x.Product.BasePrice,
                MinAdditionalPrice = x.MinAdditionalPrice,
                MainImageURL = x.MainImageURL,
                HasStock = x.HasStock,
                AvailableVariantsCount = x.AvailableVariantsCount,
                CreationDate = x.Product.CreationDate
            });

            // ===== APPLY PRICE FILTER (after projection) =====
            if (minPrice.HasValue)
            {
                productsQuery = productsQuery.Where(p =>
                    (p.BasePrice + p.MinAdditionalPrice) >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                productsQuery = productsQuery.Where(p =>
                    (p.BasePrice + p.MinAdditionalPrice) <= maxPrice.Value);
            }

            // ===== APPLY SORTING =====
            switch (sortBy?.ToLower())
            {
                case "price_asc":
                    productsQuery = productsQuery.OrderBy(p => p.BasePrice + p.MinAdditionalPrice);
                    break;

                case "price_desc":
                    productsQuery = productsQuery.OrderByDescending(p => p.BasePrice + p.MinAdditionalPrice);
                    break;

                case "name":
                    productsQuery = productsQuery.OrderBy(p => p.ProductName);
                    break;

                case "newest":
                default:
                    productsQuery = productsQuery.OrderByDescending(p => p.CreationDate);
                    break;
            }

            // ===== PAGINATION =====
            var totalProducts = productsQuery.Count();
            var totalPages = (int)Math.Ceiling(totalProducts / (double)pageSize);

            // Validate page number
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var products = productsQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // ===== GET CATEGORIES FOR FILTER =====
            var categories = db.Categories
                .Select(c => new CategoryFilterItem
                {
                    CategoryID = c.CategoryID,
                    CategoryName = c.CategoryName,
                    ParentCategoryID = c.ParentCategoryID,
                    ProductCount = db.Products.Count(p =>
                        p.CategoryID == c.CategoryID &&
                        p.IsActive == true &&
                        p.ProductVariants.Any(v => v.StockQuantity > 0))
                })
                .Where(c => c.ProductCount > 0)
                .OrderBy(c => c.CategoryName)
                .ToList();

            // ===== BUILD VIEWMODEL =====
            var viewModel = new ShopIndexViewModel
            {
                Products = products,
                Categories = categories,
                SelectedCategoryID = categoryId,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                SelectedMetalType = metalType,
                SortBy = sortBy,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalProducts = totalProducts,
                PageSize = pageSize
            };

            return View(viewModel);
        }

        // ========================================
        // ACTION: Details - Chi tiết sản phẩm
        // ========================================
        public ActionResult Details(int id)
        {
            // Lấy thông tin product chính
            var product = db.Products
                .Include(p => p.Category)
                .FirstOrDefault(p => p.ProductID == id && p.IsActive == true);

            if (product == null)
            {
                return HttpNotFound();
            }

            // Lấy variants
            var variants = db.ProductVariants
                .Where(v => v.ProductID == id)
                .OrderBy(v => v.AdditionalPrice)
                .ToList();

            // Lấy media files
            var mediaFiles = db.ProductMedias
                .Where(m => m.ProductID == id)
                .OrderByDescending(m => m.IsMain)
                .ThenBy(m => m.MediaID)
                .ToList();

            var mainImage = mediaFiles.FirstOrDefault(m => m.IsMain == true);

            // Lấy gemstones
            var variantIds = variants.Select(v => v.VariantID).ToList();
            var gemstones = db.Gemstones
                .Where(g => variantIds.Contains(g.VariantID))
                .ToList();

            // Lấy certifications
            var certifications = db.Certifications
                .Where(c => variantIds.Contains(c.VariantID))
                .ToList();

            // Lấy reviews với user info
            var reviews = (from r in db.Reviews
                           join u in db.Users on r.UserID equals u.UserID
                           where r.ProductID == id
                           orderby r.CreatedAt descending
                           select new ReviewWithUserViewModel
                           {
                               ReviewID = r.ReviewID,
                               UserID = r.UserID,
                               ProductID = r.ProductID,
                               Rating = r.Rating,
                               Title = r.Title,
                               Body = r.Body,
                               CreatedAt = r.CreatedAt,
                               UserFirstName = u.FirstName,
                               UserLastName = u.LastName
                           }).ToList();

            // Calculate average rating
            decimal averageRating = 0;
            int totalReviews = reviews.Count;

            if (totalReviews > 0)
            {
                averageRating = (decimal)reviews.Average(r => r.Rating);
            }

            // Calculate rating distribution
            var ratingDistribution = new Dictionary<int, int>
            {
                { 5, reviews.Count(r => r.Rating == 5) },
                { 4, reviews.Count(r => r.Rating == 4) },
                { 3, reviews.Count(r => r.Rating == 3) },
                { 2, reviews.Count(r => r.Rating == 2) },
                { 1, reviews.Count(r => r.Rating == 1) }
            };

            // Lấy related products (cùng category, khác product)
            var relatedProducts = (from p in db.Products
                                   join c in db.Categories on p.CategoryID equals c.CategoryID
                                   where p.CategoryID == product.CategoryID
                                       && p.ProductID != id
                                       && p.IsActive == true
                                   select new
                                   {
                                       Product = p,
                                       Category = c,
                                       MinAdditionalPrice = p.ProductVariants.Any()
                                           ? p.ProductVariants.Min(v => v.AdditionalPrice)
                                           : 0,
                                       MainImageURL = p.ProductMedias
                                           .Where(m => m.IsMain == true)
                                           .Select(m => m.URL)
                                           .FirstOrDefault(),
                                       HasStock = p.ProductVariants.Any(v => v.StockQuantity > 0)
                                   })
                                  .Where(x => x.HasStock == true)
                                  .OrderBy(x => Guid.NewGuid()) // Random
                                  .Take(4)
                                  .Select(x => new ProductListItemViewModel
                                  {
                                      ProductID = x.Product.ProductID,
                                      ProductName = x.Product.ProductName,
                                      CategoryName = x.Category.CategoryName,
                                      BasePrice = x.Product.BasePrice,
                                      MinAdditionalPrice = x.MinAdditionalPrice,
                                      MainImageURL = x.MainImageURL,
                                      HasStock = x.HasStock
                                  })
                                  .ToList();

            // Check user state (nếu đã login)
            bool isInWishlist = false;
            bool canReview = false;
            bool hasReviewed = false;

            if (User.Identity.IsAuthenticated)
            {
                // Lấy UserID từ authentication (bạn cần implement helper này)
                int currentUserId = GetCurrentUserId();

                // Check wishlist
                isInWishlist = db.Wishlists.Any(w =>
                    w.UserID == currentUserId &&
                    variantIds.Contains(w.VariantID));

                // Check can review (đã mua và nhận hàng)
                canReview = db.Orders.Any(o =>
                    o.UserID == currentUserId &&
                    o.Status == "Delivered" &&
                    o.OrderItems.Any(oi => variantIds.Contains(oi.VariantID)));

                // Check has reviewed
                hasReviewed = db.Reviews.Any(r =>
                    r.UserID == currentUserId &&
                    r.ProductID == id);
            }

            // Build ViewModel
            var viewModel = new ProductDetailsViewModel
            {
                Product = product,
                Variants = variants,
                MediaFiles = mediaFiles,
                MainImage = mainImage,
                Gemstones = gemstones,
                Certifications = certifications,
                Reviews = reviews,
                AverageRating = averageRating,
                TotalReviews = totalReviews,
                RatingDistribution = ratingDistribution,
                RelatedProducts = relatedProducts,
                IsInWishlist = isInWishlist,
                CanReview = canReview,
                HasReviewed = hasReviewed
            };

            return View(viewModel);
        }

        // ========================================
        // ACTION: QuickView - AJAX Modal
        // ========================================
        public ActionResult QuickView(int id)
        {
            var product = db.Products
                .Where(p => p.ProductID == id && p.IsActive == true)
                .Select(p => new ProductQuickViewModel
                {
                    ProductID = p.ProductID,
                    ProductName = p.ProductName,
                    Description = p.Description,
                    CategoryName = p.Category.CategoryName,
                    BasePrice = p.BasePrice,
                    MinAdditionalPrice = p.ProductVariants.Any()
                        ? p.ProductVariants.Min(v => v.AdditionalPrice)
                        : 0,
                    MainImageURL = p.ProductMedias
                        .Where(m => m.IsMain == true)
                        .Select(m => m.URL)
                        .FirstOrDefault(),
                    Variants = p.ProductVariants.ToList(),
                    AverageRating = p.Reviews.Any()
                        ? (decimal)p.Reviews.Average(r => r.Rating)
                        : 0,
                    TotalReviews = p.Reviews.Count(),
                    HasStock = p.ProductVariants.Any(v => v.StockQuantity > 0)
                })
                .FirstOrDefault();

            if (product == null)
            {
                return HttpNotFound();
            }

            return PartialView("_QuickView", product);
        }

        // ========================================
        // ACTION: Autocomplete - AJAX Search
        // ========================================
        [HttpGet]
        public JsonResult Autocomplete(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return Json(new { success = false, products = new List<object>() },
                    JsonRequestBehavior.AllowGet);
            }

            term = term.Trim().ToLower();

            var results = db.Products
                .Where(p => p.IsActive == true &&
                    (p.ProductName.ToLower().Contains(term) ||
                     p.Description.ToLower().Contains(term)))
                .OrderBy(p => p.ProductName)
                .Take(10)
                .Select(p => new
                {
                    id = p.ProductID,
                    name = p.ProductName,
                    price = p.BasePrice + (p.ProductVariants.Any()
                        ? p.ProductVariants.Min(v => v.AdditionalPrice)
                        : 0),
                    image = p.ProductMedias
                        .Where(m => m.IsMain == true)
                        .Select(m => m.URL)
                        .FirstOrDefault(),
                    categoryName = p.Category.CategoryName
                })
                .ToList();

            return Json(new { success = true, products = results },
                JsonRequestBehavior.AllowGet);
        }

        // ========================================
        // HELPER METHOD: Get Current User ID
        // ========================================
        private int GetCurrentUserId()
        {
            // TODO: Implement này dựa trên authentication system của bạn
            // Ví dụ với Forms Authentication:

            if (User.Identity.IsAuthenticated)
            {
                // Nếu bạn lưu UserID trong Identity.Name
                int userId;
                if (int.TryParse(User.Identity.Name, out userId))
                {
                    return userId;
                }

                // Hoặc lấy từ database based on username/email
                var username = User.Identity.Name;
                var user = db.Users.FirstOrDefault(u => u.Email == username);
                return user?.UserID ?? 0;
            }

            return 0;
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