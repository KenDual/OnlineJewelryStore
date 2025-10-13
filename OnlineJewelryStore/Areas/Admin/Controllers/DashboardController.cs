using OnlineJewelryStore.Areas.Admin.Dashboard;
using OnlineJewelryStore.Filters;
using OnlineJewelryStore.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace OnlineJewelryStore.Areas.Admin.Controllers
{
    [AdminAuthorize]
    public class DashboardController : Controller
    {
        private readonly OnlineJewelryStoreEntities _context;

        public DashboardController()
        {
            _context = new OnlineJewelryStoreEntities(); // Thay bằng DbContext của bạn
        }

        // GET: Admin/Dashboard
        public ActionResult Index()
        {
            var viewModel = new DashboardViewModel();

            try
            {
                // Get current month boundaries
                var now = DateTime.Now;
                var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
                var firstDayOfLastMonth = firstDayOfMonth.AddMonths(-1);
                var firstDayOfNextMonth = firstDayOfMonth.AddMonths(1);

                // ============================================
                // 1. OVERVIEW STATISTICS (8 Cards)
                // ============================================
                viewModel.OverviewStats = GetOverviewStats(firstDayOfMonth, firstDayOfLastMonth, firstDayOfNextMonth);

                // ============================================
                // 2. REVENUE CHART (Last 30 Days)
                // ============================================
                viewModel.RevenueChartData = GetRevenueLast30Days();

                // ============================================
                // 3. CATEGORY REVENUE (This Month)
                // ============================================
                viewModel.CategoryRevenueData = GetCategoryRevenue(firstDayOfMonth, firstDayOfNextMonth);

                // ============================================
                // 4. TOP SELLING PRODUCTS (This Month)
                // ============================================
                viewModel.TopProductsData = GetTopSellingProducts(firstDayOfMonth, firstDayOfNextMonth);

                // ============================================
                // 5. ORDER STATUS DISTRIBUTION (This Month)
                // ============================================
                viewModel.OrderStatusData = GetOrderStatusDistribution(firstDayOfMonth, firstDayOfNextMonth);

                // ============================================
                // 6. PAYMENT METHODS (This Month)
                // ============================================
                viewModel.PaymentMethodsData = GetPaymentMethodsDistribution(firstDayOfMonth, firstDayOfNextMonth);

                // ============================================
                // 7. RECENT ORDERS (Latest 10)
                // ============================================
                viewModel.RecentOrders = GetRecentOrders();

                // ============================================
                // 8. LOW STOCK ALERTS (Stock < 10)
                // ============================================
                viewModel.LowStockProducts = GetLowStockProducts();
            }
            catch (Exception ex)
            {
                // Log error
                TempData["Error"] = "Error loading dashboard: " + ex.Message;
            }

            return View(viewModel);
        }

        #region Private Helper Methods

        /// <summary>
        /// 1. Get Overview Statistics (8 Cards)
        /// </summary>
        private OverviewStatsViewModel GetOverviewStats(DateTime firstDayOfMonth, DateTime firstDayOfLastMonth, DateTime firstDayOfNextMonth)
        {
            var stats = new OverviewStatsViewModel();

            // ----- THIS MONTH REVENUE -----
            var thisMonthOrders = _context.Orders
                .Where(o => o.OrderDate >= firstDayOfMonth
                    && o.OrderDate < firstDayOfNextMonth
                    && o.Status != "Cancelled")
                .ToList();

            stats.ThisMonthRevenue = thisMonthOrders.Sum(o => o.GrandTotal);
            stats.ThisMonthOrders = thisMonthOrders.Count;

            // Average Order Value
            stats.AverageOrderValue = stats.ThisMonthOrders > 0
                ? stats.ThisMonthRevenue / stats.ThisMonthOrders
                : 0;

            // ----- LAST MONTH REVENUE (for comparison) -----
            var lastMonthOrders = _context.Orders
                .Where(o => o.OrderDate >= firstDayOfLastMonth
                    && o.OrderDate < firstDayOfMonth
                    && o.Status != "Cancelled")
                .ToList();

            var lastMonthRevenue = lastMonthOrders.Sum(o => o.GrandTotal);
            var lastMonthOrderCount = lastMonthOrders.Count;
            var lastMonthAOV = lastMonthOrderCount > 0
                ? lastMonthRevenue / lastMonthOrderCount
                : 0;

            // Calculate % changes
            stats.RevenueChangePercent = CalculatePercentChange(lastMonthRevenue, stats.ThisMonthRevenue);
            stats.OrdersChangePercent = CalculatePercentChange(lastMonthOrderCount, stats.ThisMonthOrders);
            stats.AOVChangePercent = CalculatePercentChange(lastMonthAOV, stats.AverageOrderValue);

            // ----- NEW CUSTOMERS THIS MONTH -----
            stats.NewCustomersThisMonth = _context.Users
                .Count(u => u.Role == "Customer"
                    && u.RegistrationDate >= firstDayOfMonth
                    && u.RegistrationDate < firstDayOfNextMonth);

            var lastMonthCustomers = _context.Users
                .Count(u => u.Role == "Customer"
                    && u.RegistrationDate >= firstDayOfLastMonth
                    && u.RegistrationDate < firstDayOfMonth);

            stats.CustomersChangePercent = CalculatePercentChange(lastMonthCustomers, stats.NewCustomersThisMonth);

            // ----- PENDING ORDERS -----
            stats.PendingOrdersCount = _context.Orders
                .Count(o => o.Status == "Pending");

            // ----- LOW STOCK ALERTS -----
            stats.LowStockCount = _context.ProductVariants
                .Count(pv => pv.StockQuantity < 10 && pv.StockQuantity > 0);

            // ----- TOTAL PRODUCTS -----
            stats.TotalActiveProducts = _context.Products
                .Count(p => p.IsActive);

            stats.TotalVariants = _context.ProductVariants.Count();

            // ----- TOTAL CUSTOMERS -----
            stats.TotalCustomers = _context.Users
                .Count(u => u.Role == "Customer");

            var thirtyDaysAgo = DateTime.Now.AddDays(-30);
            stats.ActiveCustomers = _context.Users
                .Count(u => u.Role == "Customer"
                    && u.LastLogin.HasValue
                    && u.LastLogin >= thirtyDaysAgo);

            return stats;
        }

        /// <summary>
        /// 2. Get Revenue Chart Data (Last 30 Days)
        /// </summary>
        private List<RevenueChartDataPoint> GetRevenueLast30Days()
        {
            var thirtyDaysAgo = DateTime.Now.Date.AddDays(-29); // 30 days including today
            var today = DateTime.Now.Date.AddDays(1); // End of today

            var revenueData = _context.Orders
                .Where(o => o.OrderDate >= thirtyDaysAgo
                    && o.OrderDate < today
                    && o.Status != "Cancelled")
                .GroupBy(o => DbFunctions.TruncateTime(o.OrderDate))
                .Select(g => new RevenueChartDataPoint
                {
                    Date = g.Key.Value,
                    Revenue = g.Sum(o => o.GrandTotal),
                    OrderCount = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToList();

            return revenueData;
        }

        /// <summary>
        /// 3. Get Category Revenue (This Month)
        /// </summary>
        private List<CategoryRevenueData> GetCategoryRevenue(DateTime firstDayOfMonth, DateTime firstDayOfNextMonth)
        {
            var categoryData = (from o in _context.Orders
                                join oi in _context.OrderItems on o.OrderID equals oi.OrderID
                                join pv in _context.ProductVariants on oi.VariantID equals pv.VariantID
                                join p in _context.Products on pv.ProductID equals p.ProductID
                                join c in _context.Categories on p.CategoryID equals c.CategoryID
                                where o.OrderDate >= firstDayOfMonth
                                    && o.OrderDate < firstDayOfNextMonth
                                    && o.Status != "Cancelled"
                                group new { oi, o } by c.CategoryName into g
                                select new CategoryRevenueData
                                {
                                    CategoryName = g.Key,
                                    Revenue = g.Sum(x => x.oi.Quantity * x.oi.UnitPrice),
                                    OrderCount = g.Select(x => x.o.OrderID).Distinct().Count()
                                })
                               .OrderByDescending(x => x.Revenue)
                               .Take(7) // Top 7 categories
                               .ToList();

            // Calculate percentages
            var totalRevenue = categoryData.Sum(x => x.Revenue);
            foreach (var item in categoryData)
            {
                item.Percentage = totalRevenue > 0
                    ? Math.Round((item.Revenue / totalRevenue) * 100, 2)
                    : 0;
            }

            return categoryData;
        }

        /// <summary>
        /// 4. Get Top Selling Products (This Month)
        /// </summary>
        private List<TopProductData> GetTopSellingProducts(DateTime firstDayOfMonth, DateTime firstDayOfNextMonth)
        {
            var topProducts = (from oi in _context.OrderItems
                               join o in _context.Orders on oi.OrderID equals o.OrderID
                               join pv in _context.ProductVariants on oi.VariantID equals pv.VariantID
                               join p in _context.Products on pv.ProductID equals p.ProductID
                               where o.OrderDate >= firstDayOfMonth
                                   && o.OrderDate < firstDayOfNextMonth
                                   && o.Status != "Cancelled"
                               group oi by new { p.ProductID, p.ProductName } into g
                               select new TopProductData
                               {
                                   ProductID = g.Key.ProductID,
                                   ProductName = g.Key.ProductName,
                                   TotalSold = g.Sum(x => x.Quantity),
                                   Revenue = g.Sum(x => x.Quantity * x.UnitPrice)
                               })
                              .OrderByDescending(x => x.TotalSold)
                              .Take(10)
                              .ToList();

            return topProducts;
        }

        /// <summary>
        /// 5. Get Order Status Distribution (This Month)
        /// </summary>
        private List<OrderStatusData> GetOrderStatusDistribution(DateTime firstDayOfMonth, DateTime firstDayOfNextMonth)
        {
            var statusData = _context.Orders
                .Where(o => o.OrderDate >= firstDayOfMonth
                    && o.OrderDate < firstDayOfNextMonth)
                .GroupBy(o => o.Status)
                .Select(g => new OrderStatusData
                {
                    Status = g.Key,
                    Count = g.Count(),
                    Revenue = g.Sum(o => o.GrandTotal)
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            // Calculate percentages
            var totalOrders = statusData.Sum(x => x.Count);
            foreach (var item in statusData)
            {
                item.Percentage = totalOrders > 0
                    ? Math.Round((decimal)item.Count / totalOrders * 100, 2)
                    : 0;
            }

            return statusData;
        }

        /// <summary>
        /// 6. Get Payment Methods Distribution (This Month)
        /// </summary>
        private List<PaymentMethodData> GetPaymentMethodsDistribution(DateTime firstDayOfMonth, DateTime firstDayOfNextMonth)
        {
            var paymentData = (from p in _context.Payments
                               join o in _context.Orders on p.OrderID equals o.OrderID
                               where o.OrderDate >= firstDayOfMonth
                                   && o.OrderDate < firstDayOfNextMonth
                                   && p.Status == "Captured" // Only successful payments
                               group p by p.Method into g
                               select new PaymentMethodData
                               {
                                   Method = g.Key,
                                   Count = g.Count(),
                                   TotalAmount = g.Sum(x => x.Amount)
                               })
                              .OrderByDescending(x => x.Count)
                              .ToList();

            // Calculate percentages
            var totalPayments = paymentData.Sum(x => x.Count);
            foreach (var item in paymentData)
            {
                item.Percentage = totalPayments > 0
                    ? Math.Round((decimal)item.Count / totalPayments * 100, 2)
                    : 0;
            }

            return paymentData;
        }

        /// <summary>
        /// 7. Get Recent Orders (Latest 10)
        /// </summary>
        private List<RecentOrderViewModel> GetRecentOrders()
        {
            var recentOrders = (from o in _context.Orders
                                join u in _context.Users on o.UserID equals u.UserID
                                orderby o.OrderDate descending
                                select new RecentOrderViewModel
                                {
                                    OrderID = o.OrderID,
                                    CustomerName = u.FirstName + " " + u.LastName,
                                    CustomerEmail = u.Email,
                                    OrderDate = o.OrderDate,
                                    Status = o.Status,
                                    GrandTotal = o.GrandTotal,
                                    ItemsCount = _context.OrderItems.Count(oi => oi.OrderID == o.OrderID)
                                })
                               .Take(10)
                               .ToList();

            return recentOrders;
        }

        /// <summary>
        /// 8. Get Low Stock Products (Stock < 10)
        /// </summary>
        private List<LowStockProductViewModel> GetLowStockProducts()
        {
            var lowStockProducts = (from pv in _context.ProductVariants
                                    join p in _context.Products on pv.ProductID equals p.ProductID
                                    where pv.StockQuantity < 10
                                        && pv.StockQuantity > 0
                                        && p.IsActive
                                    orderby pv.StockQuantity ascending
                                    select new LowStockProductViewModel
                                    {
                                        VariantID = pv.VariantID,
                                        ProductID = p.ProductID,
                                        ProductName = p.ProductName,
                                        SKU = pv.SKU,
                                        MetalType = pv.MetalType,
                                        Purity = pv.Purity,
                                        RingSize = pv.RingSize,
                                        ChainLength = pv.ChainLength,
                                        StockQuantity = pv.StockQuantity,
                                        BasePrice = p.BasePrice,
                                        AdditionalPrice = pv.AdditionalPrice
                                    })
                                   .Take(20) // Top 20 low stock
                                   .ToList();

            return lowStockProducts;
        }

        /// <summary>
        /// Calculate percentage change between two values
        /// </summary>
        private decimal CalculatePercentChange(decimal oldValue, decimal newValue)
        {
            if (oldValue == 0)
            {
                return newValue > 0 ? 100 : 0;
            }

            var change = ((newValue - oldValue) / oldValue) * 100;
            return Math.Round(change, 2);
        }

        /// <summary>
        /// Calculate percentage change (overload for integers)
        /// </summary>
        private decimal CalculatePercentChange(int oldValue, int newValue)
        {
            return CalculatePercentChange((decimal)oldValue, (decimal)newValue);
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}