using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace OnlineJewelryStore.ViewModels
{
    public class ChatRequestViewModel
    {
        [Required(ErrorMessage = "Message is required")]
        [StringLength(500, MinimumLength = 1, ErrorMessage = "Message must be between 1 and 500 characters")]
        public string Message { get; set; }

        public string Category { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Min price must be greater than or equal to 0")]
        public decimal? MinPrice { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Max price must be greater than or equal to 0")]
        public decimal? MaxPrice { get; set; }

        public List<ConversationMessage> ConversationHistory { get; set; }
    }

    public class ConversationMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }
}