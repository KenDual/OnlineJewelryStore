using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OnlineJewelryStore.ViewModels.Shop
{
    public class ShopIndexViewModel
    {
        public List<ProductListItemViewModel> Products { get; set; }
        
        // ===== FILTER DATA =====
        
        // Danh sách categories cho dropdown/sidebar filter
        public List<CategoryFilterItem> Categories { get; set; }
        
        // Filter parameters hiện tại (để giữ state khi pagination)
        public int? SelectedCategoryID { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string SelectedMetalType { get; set; }
        
        // Danh sách metal types có sẵn (từ CHECK constraint trong DB)
        public List<string> AvailableMetalTypes { get; set; }
        
        // ===== SORTING =====
        public string SortBy { get; set; }
        
        // ===== PAGINATION =====
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalProducts { get; set; }
        public int PageSize { get; set; }
        public string SearchTerm { get; set; }

        // Helper properties cho pagination UI
        public bool HasPreviousPage 
        { 
            get { return CurrentPage > 1; }
        }
        
        public bool HasNextPage 
        { 
            get { return CurrentPage < TotalPages; }
        }
        
        // Constructor với giá trị mặc định
        public ShopIndexViewModel()
        {
            Products = new List<ProductListItemViewModel>();
            Categories = new List<CategoryFilterItem>();
            AvailableMetalTypes = new List<string> 
            { 
                "Gold", 
                "Platinum", 
                "Silver", 
                "Rose Gold" 
            };
            PageSize = 20;
            CurrentPage = 1;
            SortBy = "newest"; // Default sort
        }
    }
    public class CategoryFilterItem
    {
        public int CategoryID { get; set; }
        public string CategoryName { get; set; }
        public int ProductCount { get; set; }
        public int? ParentCategoryID { get; set; }
    }
}