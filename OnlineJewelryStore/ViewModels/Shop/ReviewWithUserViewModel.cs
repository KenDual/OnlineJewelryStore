using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OnlineJewelryStore.ViewModels.Shop
{
    public class ReviewWithUserViewModel
    {
        public int ReviewID { get; set; }
        public int UserID { get; set; }
        public int ProductID { get; set; }
        public int Rating { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public DateTime CreatedAt { get; set; }

        // Từ Users table (JOIN)
        public string UserFirstName { get; set; }
        public string UserLastName { get; set; }

        // Calculated property
        public string UserFullName
        {
            get { return $"{UserFirstName} {UserLastName}"; }
        }

        // Helper cho hiển thị rating (số sao)
        public int FullStars { get { return Rating; } }
        public int EmptyStars { get { return 5 - Rating; } }
    }
}