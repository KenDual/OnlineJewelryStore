using OnlineJewelryStore.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace OnlineJewelryStore.Controllers
{
    [Authorize]
    public class WishlistController : Controller
    {
        private OnlineJewelryStoreEntities db = new OnlineJewelryStoreEntities();

        // ========================================
        // INDEX - Hiển thị danh sách Wishlist
        // ========================================
        public ActionResult Wishlist()
        {
            int userId = GetCurrentUserId();

            var wishlistItems = db.Wishlists
                .Where(w => w.UserID == userId)
                .Select(w => new
                {
                    w.WishlistItemID,
                    w.AddedAt,
                    w.VariantID,

                    // Product Info
                    ProductID = w.ProductVariant.ProductID,
                    ProductName = w.ProductVariant.Product.ProductName,
                    BasePrice = w.ProductVariant.Product.BasePrice,

                    // Variant Info
                    SKU = w.ProductVariant.SKU,
                    MetalType = w.ProductVariant.MetalType,
                    Purity = w.ProductVariant.Purity,
                    RingSize = w.ProductVariant.RingSize,
                    ChainLength = w.ProductVariant.ChainLength,
                    AdditionalPrice = w.ProductVariant.AdditionalPrice,
                    StockQuantity = w.ProductVariant.StockQuantity,

                    // Main Image
                    MainImageURL = w.ProductVariant.Product.ProductMedias
                        .Where(pm => pm.IsMain)
                        .Select(pm => pm.URL)
                        .FirstOrDefault()
                })
                .OrderByDescending(w => w.AddedAt)
                .ToList()
                .Select(w => new WishlistItemViewModel
                {
                    WishlistItemID = w.WishlistItemID,
                    AddedAt = w.AddedAt,

                    ProductID = w.ProductID,
                    ProductName = w.ProductName,

                    VariantID = w.VariantID,
                    SKU = w.SKU,
                    MetalType = w.MetalType,
                    Purity = w.Purity,
                    RingSize = w.RingSize,
                    ChainLength = w.ChainLength,
                    StockQuantity = w.StockQuantity,

                    // Tính giá final
                    FinalPrice = w.BasePrice + w.AdditionalPrice,

                    MainImageURL = w.MainImageURL ?? "/Content/images/no-image.jpg",
                    IsInStock = w.StockQuantity > 0
                })
                .ToList();

            return View(wishlistItems);
        }

        // ========================================
        // ADD - Thêm vào Wishlist (AJAX)
        // ========================================
        [HttpPost]
        public JsonResult Add(int variantId)
        {
            try
            {
                int userId = GetCurrentUserId();

                var variant = db.ProductVariants
                    .Include(v => v.Product)
                    .FirstOrDefault(v => v.VariantID == variantId);

                if (variant == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Product variant not found."
                    });
                }

                if (!variant.Product.IsActive)
                {
                    return Json(new
                    {
                        success = false,
                        message = "This product is no longer available."
                    });
                }

                bool alreadyExists = db.Wishlists.Any(w =>
                    w.UserID == userId &&
                    w.VariantID == variantId
                );

                if (alreadyExists)
                {
                    return Json(new
                    {
                        success = false,
                        message = "This item is already in your wishlist."
                    });
                }

                var wishlistItem = new Wishlist
                {
                    UserID = userId,
                    VariantID = variantId,
                    AddedAt = DateTime.Now
                };

                db.Wishlists.Add(wishlistItem);
                db.SaveChanges();

                int count = db.Wishlists.Count(w => w.UserID == userId);

                return Json(new
                {
                    success = true,
                    message = "Added to wishlist successfully!",
                    count = count
                });
            }
            catch (Exception ex)
            {
                // Log error here
                return Json(new
                {
                    success = false,
                    message = "An error occurred. Please try again."
                });
            }
        }

        // ========================================
        // REMOVE - Xóa khỏi Wishlist (AJAX)
        // ========================================
        [HttpPost]
        public JsonResult Remove(int wishlistItemId)
        {
            try
            {
                int userId = GetCurrentUserId();

                // ✅ Tìm item và verify ownership (Security check)
                var item = db.Wishlists.FirstOrDefault(w =>
                    w.WishlistItemID == wishlistItemId &&
                    w.UserID == userId
                );

                if (item == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Wishlist item not found or access denied."
                    });
                }
                db.Wishlists.Remove(item);
                db.SaveChanges();

                int count = db.Wishlists.Count(w => w.UserID == userId);

                return Json(new
                {
                    success = true,
                    message = "Removed from wishlist.",
                    count = count
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "An error occurred. Please try again."
                });
            }
        }

        // ========================================
        // GET COUNT - Lấy số lượng Wishlist (AJAX)
        // ========================================
        [HttpGet]
        [AllowAnonymous]
        public JsonResult GetCount()
        {
            try
            {
                if (!User.Identity.IsAuthenticated)
                {
                    return Json(new { count = 0 }, JsonRequestBehavior.AllowGet);
                }

                int userId = GetCurrentUserId();
                int count = db.Wishlists.Count(w => w.UserID == userId);

                return Json(new { count = count }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { count = 0 }, JsonRequestBehavior.AllowGet);
            }
        }

        // ========================================
        // HELPER METHOD - Lấy UserID hiện tại
        // ========================================
        private int GetCurrentUserId()
        {
            // Lấy Email từ User.Identity.Name (Forms Authentication)
            string email = User.Identity.Name;

            // Tìm UserID từ email
            var user = db.Users.FirstOrDefault(u => u.Email == email);

            if (user == null)
            {
                throw new Exception("User not found");
            }

            return user.UserID;
        }

        // ========================================
        // DISPOSE
        // ========================================
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // ========================================
    // VIEW MODEL cho Wishlist
    // ========================================
    public class WishlistItemViewModel
    {
        public int WishlistItemID { get; set; }
        public DateTime AddedAt { get; set; }
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public int VariantID { get; set; }
        public string SKU { get; set; }
        public string MetalType { get; set; }
        public string Purity { get; set; }
        public string RingSize { get; set; }
        public string ChainLength { get; set; }
        public int StockQuantity { get; set; }
        public decimal FinalPrice { get; set; }
        public string MainImageURL { get; set; }
        public bool IsInStock { get; set; }
        public string FormattedPrice => FinalPrice.ToString("N0") + " ₫";

        public string StockStatus => IsInStock
            ? $"In Stock ({StockQuantity} available)"
            : "Out of Stock";

        public string VariantDescription
        {
            get
            {
                var parts = new[]
                {
                    MetalType,
                    Purity,
                    RingSize != null ? $"Size {RingSize}" : null,
                    ChainLength != null ? $"Length {ChainLength}" : null
                }.Where(p => !string.IsNullOrEmpty(p));

                return string.Join(" | ", parts);
            }
        }
    }
}