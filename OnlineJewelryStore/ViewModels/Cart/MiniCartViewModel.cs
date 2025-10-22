using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OnlineJewelryStore.ViewModels.Cart
{
    public class MiniCartViewModel
    {
        public List<MiniCartItemViewModel> Items { get; set; }
        public int CartCount { get; set; }
        public decimal Subtotal { get; set; }
    }
}