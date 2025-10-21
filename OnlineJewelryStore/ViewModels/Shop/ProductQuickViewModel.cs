using OnlineJewelryStore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OnlineJewelryStore.ViewModels.Shop
{
    public class ProductQuickViewModel
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public string Description { get; set; }
        public string CategoryName { get; set; }

        // Giá
        public decimal BasePrice { get; set; }
        public decimal MinAdditionalPrice { get; set; }
        public decimal FinalPrice
        {
            get { return BasePrice + MinAdditionalPrice; }
        }

        // Ảnh chính
        public string MainImageURL { get; set; }

        // Variants để user chọn (size, metal, etc.)
        public List<ProductVariant> Variants { get; set; }

        // Rating trung bình
        public decimal AverageRating { get; set; }
        public int TotalReviews { get; set; }

        // Check stock
        public bool HasStock { get; set; }

        // Constructor
        public ProductQuickViewModel()
        {
            Variants = new List<ProductVariant>();
        }
    }
}