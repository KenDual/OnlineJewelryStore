using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OnlineJewelryStore.ViewModels.Cart
{
    public class CartViewModel
    {
        public List<CartItemViewModel> Items { get; set; }
        public decimal GrandTotal { get; set; }
        public int TotalItemCount { get; set; }
        public bool HasItems { get; set; }
    }
}