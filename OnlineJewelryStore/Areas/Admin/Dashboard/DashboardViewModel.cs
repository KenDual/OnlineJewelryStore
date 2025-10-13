using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OnlineJewelryStore.Areas.Admin.Dashboard
{
    public class DashboardViewModel
    {
        public string CurrentMonthName { get; set; }
        public int CurrentYear { get; set; }
        public OverviewStatsViewModel OverviewStats { get; set; }
        public List<RevenueChartDataPoint> RevenueChartData { get; set; }
        public List<CategoryRevenueData> CategoryRevenueData { get; set; }
        public List<TopProductData> TopProductsData { get; set; }
        public List<OrderStatusData> OrderStatusData { get; set; }
        public List<PaymentMethodData> PaymentMethodsData { get; set; }
        public List<RecentOrderViewModel> RecentOrders { get; set; }
        public List<LowStockProductViewModel> LowStockProducts { get; set; }

        // Constructor - Initialize lists
        public DashboardViewModel()
        {
            OverviewStats = new OverviewStatsViewModel();
            RevenueChartData = new List<RevenueChartDataPoint>();
            CategoryRevenueData = new List<CategoryRevenueData>();
            TopProductsData = new List<TopProductData>();
            OrderStatusData = new List<OrderStatusData>();
            PaymentMethodsData = new List<PaymentMethodData>();
            RecentOrders = new List<RecentOrderViewModel>();
            LowStockProducts = new List<LowStockProductViewModel>();

            // Set current month
            var now = DateTime.Now;
            CurrentMonthName = now.ToString("MMMM yyyy");
            CurrentYear = now.Year;
        }
    }
}