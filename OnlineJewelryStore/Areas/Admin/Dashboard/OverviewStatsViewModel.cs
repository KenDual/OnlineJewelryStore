using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OnlineJewelryStore.Areas.Admin.Dashboard
{
    public class OverviewStatsViewModel
    {
        public decimal ThisMonthRevenue { get; set; }

        /// <summary>
        /// % thay đổi so với tháng trước
        /// </summary>
        public decimal RevenueChangePercent { get; set; }

        /// <summary>
        /// Tổng đơn hàng tháng này
        /// </summary>
        public int ThisMonthOrders { get; set; }

        /// <summary>
        /// % thay đổi đơn hàng so với tháng trước
        /// </summary>
        public decimal OrdersChangePercent { get; set; }

        /// <summary>
        /// Khách hàng mới tháng này
        /// </summary>
        public int NewCustomersThisMonth { get; set; }

        /// <summary>
        /// % thay đổi khách hàng mới
        /// </summary>
        public decimal CustomersChangePercent { get; set; }

        /// <summary>
        /// Số đơn hàng đang chờ xử lý
        /// </summary>
        public int PendingOrdersCount { get; set; }

        // Row 2 - Secondary Metrics

        /// <summary>
        /// Số sản phẩm sắp hết hàng (< 10)
        /// </summary>
        public int LowStockCount { get; set; }

        /// <summary>
        /// Giá trị đơn hàng trung bình tháng này
        /// </summary>
        public decimal AverageOrderValue { get; set; }

        /// <summary>
        /// % thay đổi AOV so với tháng trước
        /// </summary>
        public decimal AOVChangePercent { get; set; }

        /// <summary>
        /// Tổng sản phẩm đang hoạt động
        /// </summary>
        public int TotalActiveProducts { get; set; }

        /// <summary>
        /// Tổng số variants
        /// </summary>
        public int TotalVariants { get; set; }

        /// <summary>
        /// Tổng số khách hàng (all time)
        /// </summary>
        public int TotalCustomers { get; set; }

        /// <summary>
        /// Khách hàng hoạt động (login trong 30 ngày)
        /// </summary>
        public int ActiveCustomers { get; set; }
    }
}