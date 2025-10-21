using OnlineJewelryStore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OnlineJewelryStore.ViewModels.Shop
{
    public class ProductDetailsViewModel
    {
        // ===== PRODUCT MAIN INFO =====
        // Sử dụng Model từ Database First
        public Product Product { get; set; }

        // ===== VARIANTS & OPTIONS =====
        // Tất cả variants của sản phẩm này (để chọn size, metal, etc.)
        public List<ProductVariant> Variants { get; set; }

        // ===== MEDIA FILES =====
        // Tất cả ảnh/video của sản phẩm
        public List<ProductMedia> MediaFiles { get; set; }

        // Ảnh chính (IsMain = 1)
        public ProductMedia MainImage { get; set; }

        // ===== GEMSTONE INFO =====
        // Thông tin đá quý (có thể null nếu không phải jewelry có đá)
        public List<Gemstone> Gemstones { get; set; }

        // ===== CERTIFICATIONS =====
        // Chứng chỉ GIA/AGS (có thể null)
        public List<Certification> Certifications { get; set; }

        // ===== REVIEWS =====
        public List<ReviewWithUserViewModel> Reviews { get; set; }

        // Aggregated review data
        public decimal AverageRating { get; set; }
        public int TotalReviews { get; set; }

        // Review rating distribution (để hiển thị bar chart)
        public Dictionary<int, int> RatingDistribution { get; set; }

        // ===== RELATED PRODUCTS =====
        // Sản phẩm cùng category
        public List<ProductListItemViewModel> RelatedProducts { get; set; }

        // ===== UI STATE (cho current user) =====
        // Check xem user hiện tại đã thêm vào wishlist chưa
        public bool IsInWishlist { get; set; }

        // Check xem user có thể review không (đã mua và nhận hàng)
        public bool CanReview { get; set; }

        // Check xem user đã review chưa (1 user chỉ review 1 lần)
        public bool HasReviewed { get; set; }

        // Constructor
        public ProductDetailsViewModel()
        {
            Variants = new List<ProductVariant>();
            MediaFiles = new List<ProductMedia>();
            Gemstones = new List<Gemstone>();
            Certifications = new List<Certification>();
            Reviews = new List<ReviewWithUserViewModel>();
            RelatedProducts = new List<ProductListItemViewModel>();
            RatingDistribution = new Dictionary<int, int>
            {
                { 5, 0 },
                { 4, 0 },
                { 3, 0 },
                { 2, 0 },
                { 1, 0 }
            };
        }
    }
}