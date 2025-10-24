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
        public int CategoryID { get; set; }
        public string CategoryName { get; set; }
        public decimal BasePrice { get; set; }
        public decimal MinAdditionalPrice { get; set; }
        public decimal FinalPrice
        {
            get { return BasePrice + MinAdditionalPrice; }
        }
        public string MainImageURL { get; set; }
        public bool HasStock { get; set; }
        public int AvailableVariantsCount { get; set; }
        public DateTime CreationDate { get; set; }
        public decimal AverageRating { get; set; }
        public int TotalReviews { get; set; }
    }
}