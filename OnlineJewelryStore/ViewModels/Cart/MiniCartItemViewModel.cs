using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OnlineJewelryStore.ViewModels.Cart
{
    public class MiniCartItemViewModel
    {
        public int CartItemID { get; set; }
        public string ProductName { get; set; }
        public string ImageUrl { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}