using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OnlineJewelryStore.Areas.Admin.Dashboard
{
    public class LowStockProductViewModel
    {
        public int VariantID { get; set; }
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public string SKU { get; set; }
        public string MetalType { get; set; }
        public string Purity { get; set; }
        public string RingSize { get; set; }
        public string ChainLength { get; set; }
        public int StockQuantity { get; set; }
        public decimal BasePrice { get; set; }
        public decimal AdditionalPrice { get; set; }
        public decimal FinalPrice => BasePrice + AdditionalPrice;
        public string VariantInfo
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrEmpty(MetalType)) parts.Add(MetalType);
                if (!string.IsNullOrEmpty(Purity)) parts.Add(Purity);
                if (!string.IsNullOrEmpty(RingSize)) parts.Add($"Size {RingSize}");
                if (!string.IsNullOrEmpty(ChainLength)) parts.Add($"Length {ChainLength}");

                return parts.Count > 0 ? string.Join(" - ", parts) : "Standard";
            }
        }
    }
}