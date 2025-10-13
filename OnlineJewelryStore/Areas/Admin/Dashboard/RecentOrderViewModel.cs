using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OnlineJewelryStore.Areas.Admin.Dashboard
{
    public class RecentOrderViewModel
    {
        public int OrderID { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; }
        public decimal GrandTotal { get; set; }
        public int ItemsCount { get; set; }
    }
}