using OnlineJewelryStore.Models;
using OnlineJewelryStore.ViewModels.Checkout;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace OnlineJewelryStore.Controllers
{
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly OnlineJewelryStoreEntities db;

        public CheckoutController()
        {
            db = new OnlineJewelryStoreEntities();
        }

        // GET: /Checkout
        [HttpGet]
        public ActionResult Index()
        {
            try
            {
                int userId = GetCurrentUserId();

                var cart = db.Carts
                    .Include(c => c.CartItems.Select(ci => ci.ProductVariant.Product))
                    .Include(c => c.CartItems.Select(ci => ci.ProductVariant.Product.ProductMedias))
                    .FirstOrDefault(c => c.UserID == userId);

                if (cart == null || !cart.CartItems.Any())
                {
                    TempData["ErrorMessage"] = "Giỏ hàng của bạn đang trống. Vui lòng thêm sản phẩm trước khi thanh toán.";
                    return RedirectToAction("Index", "Cart");
                }

                foreach (var cartItem in cart.CartItems)
                {
                    if (cartItem.ProductVariant.StockQuantity < cartItem.Quantity)
                    {
                        TempData["ErrorMessage"] = $"Sản phẩm {cartItem.ProductVariant.Product.ProductName} chỉ còn {cartItem.ProductVariant.StockQuantity} sản phẩm trong kho.";
                        return RedirectToAction("Index", "Cart");
                    }
                }

                // Lấy danh sách addresses của user (IsDefault đầu tiên)
                var addresses = db.Addresses
                    .Where(a => a.UserID == userId)
                    .OrderByDescending(a => a.IsDefault)
                    .Select(a => new AddressViewModel
                    {
                        AddressID = a.AddressID,
                        StreetAddress = a.StreetAddress,
                        City = a.City,
                        State = a.State,
                        PostalCode = a.PostalCode,
                        Country = a.Country,
                        Phone = a.Phone,
                        IsDefault = a.IsDefault
                    })
                    .ToList();

                // Nếu user chưa có địa chỉ, redirect đến trang thêm địa chỉ
                if (!addresses.Any())
                {
                    TempData["ErrorMessage"] = "Vui lòng thêm địa chỉ giao hàng trước khi thanh toán.";
                    return RedirectToAction("Create", "Addresses", new { returnUrl = Url.Action("Index", "Checkout") });
                }

                // Map CartItems sang ViewModel
                var cartItemsViewModel = cart.CartItems.Select(ci => new CartItemViewModel
                {
                    CartItemID = ci.CartItemID,
                    VariantID = ci.VariantID,
                    ProductID = ci.ProductVariant.ProductID,
                    ProductName = ci.ProductVariant.Product.ProductName,
                    SKU = ci.ProductVariant.SKU,
                    MetalType = ci.ProductVariant.MetalType,
                    RingSize = ci.ProductVariant.RingSize,
                    ChainLength = ci.ProductVariant.ChainLength,
                    Quantity = ci.Quantity,
                    BasePrice = ci.ProductVariant.Product.BasePrice,
                    AdditionalPrice = ci.ProductVariant.AdditionalPrice,
                    StockQuantity = ci.ProductVariant.StockQuantity,
                    ImageUrl = ci.ProductVariant.Product.ProductMedias
                        .Where(pm => pm.IsMain)
                        .Select(pm => pm.URL)
                        .FirstOrDefault() ?? "/Content/images/no-image.jpg"
                }).ToList();

                // Tính Order Summary
                var orderSummary = new OrderSummaryViewModel
                {
                    Subtotal = cartItemsViewModel.Sum(ci => ci.TotalPrice),
                    ShippingFee = 30000m, // 30,000 VND cố định
                    DiscountTotal = 0,
                    CouponCode = null,
                    PercentOff = null
                };
                orderSummary.CalculateTotals();

                // Tạo ViewModel
                var viewModel = new CheckoutViewModel
                {
                    CartItems = cartItemsViewModel,
                    Addresses = addresses,
                    OrderSummary = orderSummary
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                // Log error
                System.Diagnostics.Debug.WriteLine($"Checkout Index Error: {ex.Message}");
                TempData["ErrorMessage"] = "Có lỗi xảy ra. Vui lòng thử lại.";
                return RedirectToAction("Index", "Cart");
            }
        }

        /// <summary>
        /// POST: /Checkout/ApplyCoupon (AJAX)
        /// Áp dụng mã giảm giá và tính lại tổng tiền
        /// </summary>
        [HttpPost]
        public JsonResult ApplyCoupon(string code, decimal subtotal)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(code))
                {
                    return Json(new ApplyCouponResponse
                    {
                        Success = false,
                        Message = "Vui lòng nhập mã giảm giá."
                    });
                }

                // Tìm coupon trong database
                var coupon = db.Coupons
                    .FirstOrDefault(c => c.Code.ToUpper() == code.ToUpper() && c.IsActive);

                if (coupon == null)
                {
                    return Json(new ApplyCouponResponse
                    {
                        Success = false,
                        Message = "Mã giảm giá không hợp lệ hoặc đã hết hạn."
                    });
                }

                // Tính discount
                decimal discount = Math.Round(subtotal * (coupon.PercentOff / 100m), 2);
                if (discount > subtotal)
                {
                    discount = subtotal;
                }

                // Tính grand total mới
                decimal taxTotal = Math.Round(subtotal * 0.10m, 2);
                decimal shippingFee = 30000m;
                decimal grandTotal = subtotal - discount + taxTotal + shippingFee;

                return Json(new ApplyCouponResponse
                {
                    Success = true,
                    Message = $"Áp dụng mã giảm giá thành công! Giảm {coupon.PercentOff}%",
                    Discount = discount,
                    GrandTotal = grandTotal,
                    PercentOff = coupon.PercentOff
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyCoupon Error: {ex.Message}");
                return Json(new ApplyCouponResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi áp dụng mã giảm giá."
                });
            }
        }

        /// <summary>
        /// POST: /Checkout/PlaceOrder
        /// Xử lý đặt hàng với transaction
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PlaceOrder(PlaceOrderRequest request)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Validate ModelState
                    if (!ModelState.IsValid)
                    {
                        TempData["ErrorMessage"] = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại.";
                        return RedirectToAction("Index");
                    }

                    int userId = GetCurrentUserId();

                    // 1. Validate Address thuộc về user
                    var address = db.Addresses.FirstOrDefault(a => a.AddressID == request.ShippingAddressID && a.UserID == userId);
                    if (address == null)
                    {
                        TempData["ErrorMessage"] = "Địa chỉ giao hàng không hợp lệ.";
                        return RedirectToAction("Index");
                    }

                    // 2. Validate Payment Method
                    var validPaymentMethods = new[] { "COD", "Card", "VNPay", "MoMo", "Bank" };
                    if (!validPaymentMethods.Contains(request.PaymentMethod))
                    {
                        TempData["ErrorMessage"] = "Phương thức thanh toán không hợp lệ.";
                        return RedirectToAction("Index");
                    }

                    // 3. Lấy Cart và CartItems
                    var cart = db.Carts
                        .Include(c => c.CartItems.Select(ci => ci.ProductVariant.Product))
                        .FirstOrDefault(c => c.UserID == userId);

                    if (cart == null || !cart.CartItems.Any())
                    {
                        TempData["ErrorMessage"] = "Giỏ hàng trống.";
                        return RedirectToAction("Index", "Cart");
                    }

                    // 4. Kiểm tra Stock Availability
                    foreach (var cartItem in cart.CartItems)
                    {
                        if (cartItem.ProductVariant.StockQuantity < cartItem.Quantity)
                        {
                            TempData["ErrorMessage"] = $"Sản phẩm {cartItem.ProductVariant.Product.ProductName} không đủ số lượng trong kho.";
                            transaction.Rollback();
                            return RedirectToAction("Index", "Cart");
                        }
                    }

                    // 5. Tính Order Summary
                    decimal subtotal = cart.CartItems.Sum(ci =>
                        (ci.ProductVariant.Product.BasePrice + ci.ProductVariant.AdditionalPrice) * ci.Quantity);
                    decimal taxTotal = Math.Round(subtotal * 0.10m, 2);
                    decimal shippingFee = 30000m;
                    decimal discountTotal = 0;
                    int? couponId = null;

                    // 6. Apply Coupon nếu có
                    if (!string.IsNullOrWhiteSpace(request.CouponCode))
                    {
                        var coupon = db.Coupons
                            .FirstOrDefault(c => c.Code.ToUpper() == request.CouponCode.ToUpper() && c.IsActive);

                        if (coupon != null)
                        {
                            discountTotal = Math.Round(subtotal * (coupon.PercentOff / 100m), 2);
                            if (discountTotal > subtotal) discountTotal = subtotal;
                            couponId = coupon.CouponID;
                        }
                    }

                    decimal grandTotal = subtotal - discountTotal + taxTotal + shippingFee;

                    // 7. Tạo Order
                    var order = new Order
                    {
                        UserID = userId,
                        OrderDate = DateTime.Now,
                        Status = "Pending",
                        ShippingAddressID = request.ShippingAddressID,
                        CouponID = couponId,
                        GiftMessage = request.GiftMessage,
                        Subtotal = subtotal,
                        TaxTotal = taxTotal,
                        ShippingFee = shippingFee,
                        DiscountTotal = discountTotal,
                        GrandTotal = grandTotal,
                        DeliveredAt = null,
                        CancellationReason = null
                    };

                    db.Orders.Add(order);
                    db.SaveChanges(); // Save để có OrderID

                    // 8. Tạo OrderItems
                    foreach (var cartItem in cart.CartItems)
                    {
                        var orderItem = new OrderItem
                        {
                            OrderID = order.OrderID,
                            VariantID = cartItem.VariantID,
                            Quantity = cartItem.Quantity,
                            UnitPrice = cartItem.ProductVariant.Product.BasePrice + cartItem.ProductVariant.AdditionalPrice,
                            CreatedAt = DateTime.Now
                        };
                        db.OrderItems.Add(orderItem);
                    }

                    // 9. Giảm Stock Quantity
                    foreach (var cartItem in cart.CartItems)
                    {
                        var variant = cartItem.ProductVariant;
                        variant.StockQuantity -= cartItem.Quantity;

                        if (variant.StockQuantity < 0)
                        {
                            throw new Exception($"Lỗi: Stock quantity âm cho SKU {variant.SKU}");
                        }

                        db.Entry(variant).State = EntityState.Modified;
                    }

                    // 10. Xóa CartItems
                    db.CartItems.RemoveRange(cart.CartItems);

                    // 11. Tạo Payment Record
                    var payment = new Payment
                    {
                        OrderID = order.OrderID,
                        Amount = grandTotal,
                        Currency = "VND",
                        Method = request.PaymentMethod,
                        Status = request.PaymentMethod == "COD" ? "Pending" : "Authorized",
                        TransactionRef = null,
                        CreatedAt = DateTime.Now,
                        CapturedAt = null,
                        Provider = request.PaymentMethod == "VNPay" ? "VNPay" :
                                   request.PaymentMethod == "MoMo" ? "MoMo" : null
                    };
                    db.Payments.Add(payment);

                    // 12. Save Changes
                    db.SaveChanges();

                    // 13. Commit Transaction
                    transaction.Commit();

                    // 14. Set success flag
                    TempData["OrderSuccess"] = true;
                    TempData["SuccessMessage"] = "Đặt hàng thành công!";

                    // 15. Redirect based on payment method
                    if (request.PaymentMethod == "VNPay" || request.PaymentMethod == "MoMo")
                    {
                        // TODO: Redirect to payment gateway
                        // return RedirectToAction("VNPayPayment", new { orderId = order.OrderID });

                        // Tạm thời redirect về confirmation
                        return RedirectToAction("OrderConfirmation", new { orderId = order.OrderID });
                    }

                    return RedirectToAction("OrderConfirmation", new { orderId = order.OrderID });
                }
                catch (Exception ex)
                {
                    // Rollback transaction
                    transaction.Rollback();

                    // Log error
                    System.Diagnostics.Debug.WriteLine($"PlaceOrder Error: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");

                    TempData["ErrorMessage"] = "Có lỗi xảy ra khi đặt hàng. Vui lòng thử lại.";
                    return RedirectToAction("Index");
                }
            }
        }

        /// <summary>
        /// GET: /Checkout/OrderConfirmation
        /// Hiển thị trang xác nhận đơn hàng
        /// </summary>
        [HttpGet]
        public ActionResult OrderConfirmation(int orderId)
        {
            try
            {
                int userId = GetCurrentUserId();

                // Kiểm tra OrderSuccess flag
                if (TempData["OrderSuccess"] == null || !(bool)TempData["OrderSuccess"])
                {
                    TempData["ErrorMessage"] = "Truy cập không hợp lệ.";
                    return RedirectToAction("Index", "Home");
                }

                // Lấy Order details
                var order = db.Orders
                    .Include(o => o.OrderItems.Select(oi => oi.ProductVariant.Product))
                    .Include(o => o.OrderItems.Select(oi => oi.ProductVariant.Product.ProductMedias))
                    .Include(o => o.Address)
                    .FirstOrDefault(o => o.OrderID == orderId && o.UserID == userId);

                if (order == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy đơn hàng.";
                    return RedirectToAction("Index", "Home");
                }

                var payment = db.Payments.FirstOrDefault(p => p.OrderID == orderId);

                // Map sang ViewModel
                var viewModel = new OrderConfirmationViewModel
                {
                    OrderID = order.OrderID,
                    OrderDate = order.OrderDate,
                    Status = order.Status,
                    GrandTotal = order.GrandTotal,
                    Subtotal = order.Subtotal,
                    TaxTotal = order.TaxTotal,
                    ShippingFee = order.ShippingFee,
                    DiscountTotal = order.DiscountTotal,
                    GiftMessage = order.GiftMessage,
                    ShippingAddress = new AddressViewModel
                    {
                        AddressID = order.Address.AddressID,
                        StreetAddress = order.Address.StreetAddress,
                        City = order.Address.City,
                        State = order.Address.State,
                        PostalCode = order.Address.PostalCode,
                        Country = order.Address.Country,
                        Phone = order.Address.Phone
                    },
                    OrderItems = order.OrderItems.Select(oi => new OrderItemViewModel
                    {
                        OrderItemID = oi.OrderItemID,
                        ProductName = oi.ProductVariant.Product.ProductName,
                        SKU = oi.ProductVariant.SKU,
                        MetalType = oi.ProductVariant.MetalType,
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice,
                        ImageUrl = oi.ProductVariant.Product.ProductMedias
                            .Where(pm => pm.IsMain)
                            .Select(pm => pm.URL)
                            .FirstOrDefault() ?? "/Content/images/no-image.jpg"
                    }).ToList(),
                    Payment = payment != null ? new PaymentInfoViewModel
                    {
                        Method = payment.Method,
                        Status = payment.Status,
                        Amount = payment.Amount,
                        Currency = payment.Currency,
                        TransactionRef = payment.TransactionRef
                    } : null
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OrderConfirmation Error: {ex.Message}");
                TempData["ErrorMessage"] = "Có lỗi xảy ra. Vui lòng thử lại.";
                return RedirectToAction("Index", "Home");
            }
        }

        /// <summary>
        /// Helper method để lấy UserID từ authentication
        /// </summary>
        private int GetCurrentUserId()
        {
            // Giả sử bạn lưu UserID trong Claims hoặc Session
            // Thay đổi implementation tùy theo cách bạn handle authentication

            if (User.Identity.IsAuthenticated)
            {
                // Option 1: Từ Claims
                var userIdClaim = User.Identity.Name; // hoặc lấy từ claim khác
                if (int.TryParse(userIdClaim, out int userId))
                {
                    return userId;
                }

                // Option 2: Query từ database bằng email/username
                var email = User.Identity.Name;
                var user = db.Users.FirstOrDefault(u => u.Email == email);
                if (user != null)
                {
                    return user.UserID;
                }
            }

            throw new UnauthorizedAccessException("User not authenticated");
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