using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OnlineJewelryStore.ViewModels.Shop
{
    public class ProductListItemViewModel
    {
        public int ProductID { get; set; }

        public string ProductName { get; set; }

        public string Description { get; set; }

        // Từ bảng Categories (JOIN)
        public int CategoryID { get; set; }
        public string CategoryName { get; set; }

        // Giá gốc từ Products table
        public decimal BasePrice { get; set; }

        // Giá thấp nhất của các variants (calculated)
        public decimal MinAdditionalPrice { get; set; }

        // Giá cuối cùng để hiển thị = BasePrice + MinAdditionalPrice
        public decimal FinalPrice
        {
            get { return BasePrice + MinAdditionalPrice; }
        }

        // URL ảnh chính (IsMain = 1) từ ProductMedia
        public string MainImageURL { get; set; }

        // Check có variant nào còn hàng không
        public bool HasStock { get; set; }

        // Số lượng variants còn hàng
        public int AvailableVariantsCount { get; set; }

        // Ngày tạo sản phẩm (để sort "Newest")
        public DateTime CreationDate { get; set; }
    }
}