using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using OnlineJewelryStore.Models;
using OnlineJewelryStore.ViewModels.Cart;

namespace OnlineJewelryStore.Controllers
{
    [Authorize] // Yêu cầu đăng nhập cho toàn bộ controller
    public class CartController : Controller
    {
        private OnlineJewelryStoreEntities db = new OnlineJewelryStoreEntities();

        // GET: Cart
        public ActionResult Index()
        {
            try
            {
                var userId = GetCurrentUserId();
                var cartViewModel = GetCartViewModel(userId);

                return View(cartViewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading cart: " + ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        // POST: Cart/AddToCart
        [HttpPost]
        public JsonResult AddToCart(int variantId, int quantity = 1)
        {
            try
            {
                // Kiểm tra login
                if (!User.Identity.IsAuthenticated)
                {
                    return Json(new { success = false, message = "Please login to add items to cart" });
                }

                var userId = GetCurrentUserId();

                // Kiểm tra variant có tồn tại và active không
                var variant = db.ProductVariants
                    .Include(v => v.Product)
                    .FirstOrDefault(v => v.VariantID == variantId);

                if (variant == null || !variant.Product.IsActive)
                {
                    return Json(new { success = false, message = "Product not found or inactive" });
                }

                // Kiểm tra stock
                if (variant.StockQuantity < quantity)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Only {variant.StockQuantity} items available in stock"
                    });
                }

                // Lấy hoặc tạo Cart
                var cart = db.Carts.FirstOrDefault(c => c.UserID == userId);
                if (cart == null)
                {
                    cart = new Cart
                    {
                        UserID = userId,
                        CreatedAt = DateTime.Now
                    };
                    db.Carts.Add(cart);
                    db.SaveChanges();
                }

                // Kiểm tra CartItem đã tồn tại chưa
                var cartItem = db.CartItems.FirstOrDefault(ci =>
                    ci.CartID == cart.CartID && ci.VariantID == variantId);

                if (cartItem != null)
                {
                    // Đã tồn tại -> Update quantity
                    int newQuantity = cartItem.Quantity + quantity;

                    // Kiểm tra stock cho new quantity
                    if (variant.StockQuantity < newQuantity)
                    {
                        return Json(new
                        {
                            success = false,
                            message = $"Cannot add more. Maximum available: {variant.StockQuantity}"
                        });
                    }

                    cartItem.Quantity = newQuantity;
                    cartItem.AddedAt = DateTime.Now;
                }
                else
                {
                    // Chưa tồn tại -> Thêm mới
                    cartItem = new CartItem
                    {
                        CartID = cart.CartID,
                        VariantID = variantId,
                        Quantity = quantity,
                        AddedAt = DateTime.Now
                    };
                    db.CartItems.Add(cartItem);
                }

                db.SaveChanges();

                // Đếm lại cart items
                int cartCount = db.CartItems
                    .Where(ci => ci.CartID == cart.CartID)
                    .Sum(ci => ci.Quantity);

                return Json(new
                {
                    success = true,
                    cartCount = cartCount,
                    message = "Product added to cart successfully"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // POST: Cart/UpdateQuantity
        [HttpPost]
        public JsonResult UpdateQuantity(int cartItemId, int quantity)
        {
            try
            {
                if (quantity <= 0)
                {
                    return Json(new { success = false, message = "Quantity must be greater than 0" });
                }

                var userId = GetCurrentUserId();

                // Lấy CartItem và verify ownership
                var cartItem = db.CartItems
                    .Include(ci => ci.Cart)
                    .Include(ci => ci.ProductVariant)
                    .Include(ci => ci.ProductVariant.Product)
                    .FirstOrDefault(ci => ci.CartItemID == cartItemId && ci.Cart.UserID == userId);

                if (cartItem == null)
                {
                    return Json(new { success = false, message = "Cart item not found" });
                }

                // Kiểm tra stock
                if (cartItem.ProductVariant.StockQuantity < quantity)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Only {cartItem.ProductVariant.StockQuantity} items available",
                        availableStock = cartItem.ProductVariant.StockQuantity
                    });
                }

                // Update quantity
                cartItem.Quantity = quantity;
                db.SaveChanges();

                // Tính toán lại
                decimal unitPrice = cartItem.ProductVariant.Product.BasePrice +
                                   cartItem.ProductVariant.AdditionalPrice;
                decimal newSubtotal = unitPrice * quantity;

                var grandTotal = CalculateCartTotal(userId);
                var cartCount = GetCartItemCount(userId);

                return Json(new
                {
                    success = true,
                    newSubtotal = newSubtotal,
                    grandTotal = grandTotal,
                    cartCount = cartCount
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // POST: Cart/RemoveItem
        [HttpPost]
        public JsonResult RemoveItem(int cartItemId)
        {
            try
            {
                var userId = GetCurrentUserId();

                var cartItem = db.CartItems
                    .Include(ci => ci.Cart)
                    .FirstOrDefault(ci => ci.CartItemID == cartItemId && ci.Cart.UserID == userId);

                if (cartItem == null)
                {
                    return Json(new { success = false, message = "Cart item not found" });
                }

                db.CartItems.Remove(cartItem);
                db.SaveChanges();

                var grandTotal = CalculateCartTotal(userId);
                var cartCount = GetCartItemCount(userId);

                return Json(new
                {
                    success = true,
                    cartCount = cartCount,
                    grandTotal = grandTotal,
                    message = "Item removed from cart"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        //// GET: Cart/GetMiniCart
        //[HttpGet]
        //public ActionResult GetMiniCart()
        //{
        //    try
        //    {
        //        if (!User.Identity.IsAuthenticated)
        //        {
        //            return PartialView("_MiniCart", new MiniCartViewModel
        //            {
        //                Items = new List<MiniCartItemViewModel>(),
        //                CartCount = 0,
        //                Subtotal = 0
        //            });
        //        }

        //        var userId = GetCurrentUserId();
        //        var cart = db.Carts.FirstOrDefault(c => c.UserID == userId);

        //        if (cart == null)
        //        {
        //            return PartialView("_MiniCart", new MiniCartViewModel
        //            {
        //                Items = new List<MiniCartItemViewModel>(),
        //                CartCount = 0,
        //                Subtotal = 0
        //            });
        //        }

        //        // Lấy top 3 items mới nhất
        //        var items = db.CartItems
        //            .Where(ci => ci.CartID == cart.CartID)
        //            .OrderByDescending(ci => ci.AddedAt)
        //            .Take(3)
        //            .Select(ci => new MiniCartItemViewModel
        //            {
        //                CartItemID = ci.CartItemID,
        //                ProductName = ci.ProductVariant.Product.ProductName,
        //                ImageUrl = ci.ProductVariant.Product.ProductMedias
        //                    .Where(pm => pm.IsMain)
        //                    .Select(pm => pm.URL)
        //                    .FirstOrDefault() ?? "/assets/img/no-image.jpg",
        //                Quantity = ci.Quantity,
        //                Price = ci.ProductVariant.Product.BasePrice + ci.ProductVariant.AdditionalPrice
        //            })
        //            .ToList();

        //        var viewModel = new MiniCartViewModel
        //        {
        //            Items = items,
        //            CartCount = GetCartItemCount(userId),
        //            Subtotal = CalculateCartTotal(userId)
        //        };

        //        return PartialView("_MiniCart", viewModel);
        //    }
        //    catch (Exception ex)
        //    {
        //        return PartialView("_MiniCart", new MiniCartViewModel
        //        {
        //            Items = new List<MiniCartItemViewModel>(),
        //            CartCount = 0,
        //            Subtotal = 0
        //        });
        //    }
        //}

        // GET: Cart/GetCartCount
        [HttpGet]
        [AllowAnonymous]
        public JsonResult GetCartCount()
        {
            try
            {
                if (!User.Identity.IsAuthenticated)
                {
                    return Json(new { count = 0 }, JsonRequestBehavior.AllowGet);
                }

                var userId = GetCurrentUserId();
                var count = GetCartItemCount(userId);

                return Json(new { count = count }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { count = 0 }, JsonRequestBehavior.AllowGet);
            }
        }

        #region Helper Methods

        private int GetCurrentUserId()
        {
            // Cách 1: Nếu bạn lưu UserID trong Claims
            var userIdClaim = User.Identity.Name; // hoặc User.Claims...

            // Cách 2: Query từ database dựa trên Email
            var email = User.Identity.Name;
            var user = db.Users.FirstOrDefault(u => u.Email == email);

            if (user == null)
                throw new Exception("User not found");

            return user.UserID;
        }

        private int GetCartItemCount(int userId)
        {
            var cart = db.Carts.FirstOrDefault(c => c.UserID == userId);
            if (cart == null) return 0;

            // Option 1: Đếm số items (mỗi variant = 1 item)
            // return db.CartItems.Count(ci => ci.CartID == cart.CartID);

            // Option 2: Đếm tổng quantity (recommended cho jewelry store)
            return db.CartItems
                .Where(ci => ci.CartID == cart.CartID)
                .Sum(ci => (int?)ci.Quantity) ?? 0;
        }

        private decimal CalculateCartTotal(int userId)
        {
            var cart = db.Carts.FirstOrDefault(c => c.UserID == userId);
            if (cart == null) return 0;

            var total = db.CartItems
                .Where(ci => ci.CartID == cart.CartID)
                .Select(ci => new
                {
                    Subtotal = (ci.ProductVariant.Product.BasePrice +
                               ci.ProductVariant.AdditionalPrice) * ci.Quantity
                })
                .Sum(x => (decimal?)x.Subtotal) ?? 0;

            return total;
        }

        private CartViewModel GetCartViewModel(int userId)
        {
            var cart = db.Carts.FirstOrDefault(c => c.UserID == userId);

            if (cart == null)
            {
                return new CartViewModel
                {
                    Items = new List<CartItemViewModel>(),
                    GrandTotal = 0,
                    TotalItemCount = 0,
                    HasItems = false
                };
            }

            var items = db.CartItems
                .Where(ci => ci.CartID == cart.CartID)
                .Select(ci => new CartItemViewModel
                {
                    CartItemID = ci.CartItemID,
                    ProductID = ci.ProductVariant.Product.ProductID,
                    ProductName = ci.ProductVariant.Product.ProductName,
                    VariantID = ci.VariantID,
                    SKU = ci.ProductVariant.SKU,
                    MetalType = ci.ProductVariant.MetalType,
                    RingSize = ci.ProductVariant.RingSize,
                    ChainLength = ci.ProductVariant.ChainLength,
                    ImageUrl = ci.ProductVariant.Product.ProductMedias
                        .Where(pm => pm.IsMain)
                        .Select(pm => pm.URL)
                        .FirstOrDefault() ?? "/assets/img/no-image.jpg",
                    Quantity = ci.Quantity,
                    UnitPrice = ci.ProductVariant.Product.BasePrice + ci.ProductVariant.AdditionalPrice,
                    Subtotal = (ci.ProductVariant.Product.BasePrice + ci.ProductVariant.AdditionalPrice) * ci.Quantity,
                    StockQuantity = ci.ProductVariant.StockQuantity,
                    InStock = ci.ProductVariant.StockQuantity >= ci.Quantity
                })
                .ToList();

            return new CartViewModel
            {
                Items = items,
                GrandTotal = items.Sum(i => i.Subtotal),
                TotalItemCount = items.Sum(i => i.Quantity),
                HasItems = items.Any()
            };
        }

        #endregion

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