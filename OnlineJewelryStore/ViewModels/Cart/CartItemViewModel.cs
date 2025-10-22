using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OnlineJewelryStore.ViewModels.Cart
{
    public class CartItemViewModel
    {
        public int CartItemID { get; set; }
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public int VariantID { get; set; }
        public string SKU { get; set; }
        public string MetalType { get; set; }
        public string RingSize { get; set; }
        public string ChainLength { get; set; }
        public string ImageUrl { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public int StockQuantity { get; set; }
        public bool InStock { get; set; }

        // Hiển thị thông tin variant
        public string VariantInfo
        {
            get
            {
                var info = new List<string>();
                if (!string.IsNullOrEmpty(MetalType)) info.Add(MetalType);
                if (!string.IsNullOrEmpty(RingSize)) info.Add($"Size: {RingSize}");
                if (!string.IsNullOrEmpty(ChainLength)) info.Add($"Length: {ChainLength}");
                return string.Join(" | ", info);
            }
        }
    }
}