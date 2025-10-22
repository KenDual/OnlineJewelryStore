using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace OnlineJewelryStore.ViewModels.Checkout
{
    public class CheckoutViewModel
    {
        public List<CartItemViewModel> CartItems { get; set; }
        public List<AddressViewModel> Addresses { get; set; }
        public OrderSummaryViewModel OrderSummary { get; set; }
        public List<string> PaymentMethods { get; set; }

        public CheckoutViewModel()
        {
            CartItems = new List<CartItemViewModel>();
            Addresses = new List<AddressViewModel>();
            OrderSummary = new OrderSummaryViewModel();
            PaymentMethods = new List<string> { "COD", "Card", "VNPay", "MoMo", "Bank" };
        }
    }

    // CartItem với thông tin chi tiết cho checkout
    public class CartItemViewModel
    {
        public int CartItemID { get; set; }
        public int VariantID { get; set; }
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public string SKU { get; set; }
        public string MetalType { get; set; }
        public string RingSize { get; set; }
        public string ChainLength { get; set; }
        public int Quantity { get; set; }
        public decimal BasePrice { get; set; }
        public decimal AdditionalPrice { get; set; }
        public decimal UnitPrice => BasePrice + AdditionalPrice;
        public decimal TotalPrice => UnitPrice * Quantity;
        public string ImageUrl { get; set; }
        public int StockQuantity { get; set; }
    }

    // Address với thông tin đầy đủ
    public class AddressViewModel
    {
        public int AddressID { get; set; }
        public string StreetAddress { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }
        public string Phone { get; set; }
        public bool IsDefault { get; set; }

        public string FullAddress
        {
            get
            {
                return $"{StreetAddress}, {City}, {State} {PostalCode}, {Country}";
            }
        }
    }

    // Order Summary với các tính toán
    public class OrderSummaryViewModel
    {
        public decimal Subtotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public string CouponCode { get; set; }
        public decimal? PercentOff { get; set; }

        public decimal TaxRate { get; set; } = 0.10m; // 10%

        public void CalculateTotals()
        {
            TaxTotal = Math.Round(Subtotal * TaxRate, 2);
            GrandTotal = Subtotal - DiscountTotal + TaxTotal + ShippingFee;
        }
    }

    // Request model cho PlaceOrder
    public class PlaceOrderRequest
    {
        [Required(ErrorMessage = "Vui lòng chọn địa chỉ giao hàng")]
        public int ShippingAddressID { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán")]
        public string PaymentMethod { get; set; }

        public string CouponCode { get; set; }

        [StringLength(500, ErrorMessage = "Tin nhắn quà tặng không được vượt quá 500 ký tự")]
        public string GiftMessage { get; set; }
    }

    // Response cho ApplyCoupon AJAX
    public class ApplyCouponResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public decimal Discount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PercentOff { get; set; }
    }

    // ViewModel cho Order Confirmation page
    public class OrderConfirmationViewModel
    {
        public int OrderID { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; }
        public decimal GrandTotal { get; set; }
        public List<OrderItemViewModel> OrderItems { get; set; }
        public AddressViewModel ShippingAddress { get; set; }
        public PaymentInfoViewModel Payment { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal DiscountTotal { get; set; }
        public string GiftMessage { get; set; }

        public OrderConfirmationViewModel()
        {
            OrderItems = new List<OrderItemViewModel>();
        }
    }

    // OrderItem cho confirmation
    public class OrderItemViewModel
    {
        public int OrderItemID { get; set; }
        public string ProductName { get; set; }
        public string SKU { get; set; }
        public string MetalType { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice => UnitPrice * Quantity;
        public string ImageUrl { get; set; }
    }

    // Payment info cho confirmation
    public class PaymentInfoViewModel
    {
        public string Method { get; set; }
        public string Status { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string TransactionRef { get; set; }
    }
}