using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OnlineJewelryStore.ViewModels
{
    public class ChatResponseViewModel
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<ProductSuggestionViewModel> Products { get; set; }
        public string Timestamp { get; set; }
        public string Error { get; set; }

        public ChatResponseViewModel()
        {
            Products = new List<ProductSuggestionViewModel>();
        }
    }
}