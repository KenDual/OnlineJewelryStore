using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OnlineJewelryStore.Areas.Admin.Dashboard
{
    public class RevenueChartDataPoint
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
    }

    /// <summary>
    /// Data cho Category Revenue Pie Chart
    /// </summary>
    public class CategoryRevenueData
    {
        public string CategoryName { get; set; }
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
        public decimal Percentage { get; set; }
    }

    /// <summary>
    /// Data cho Top Selling Products Bar Chart
    /// </summary>
    public class TopProductData
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public int TotalSold { get; set; }
        public decimal Revenue { get; set; }
    }

    /// <summary>
    /// Data cho Order Status Donut Chart
    /// </summary>
    public class OrderStatusData
    {
        public string Status { get; set; }
        public int Count { get; set; }
        public decimal Revenue { get; set; }
        public decimal Percentage { get; set; }
    }

    /// <summary>
    /// Data cho Payment Methods Pie Chart
    /// </summary>
    public class PaymentMethodData
    {
        public string Method { get; set; }
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Percentage { get; set; }
    }
}