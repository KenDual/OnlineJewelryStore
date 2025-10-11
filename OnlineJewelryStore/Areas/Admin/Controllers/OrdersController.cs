using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using OnlineJewelryStore.Models;
using OnlineJewelryStore.Filters;

namespace OnlineJewelryStore.Areas.Admin.Controllers
{
    [AdminAuthorize]
    public class OrdersController : Controller
    {
        private OnlineJewelryStoreEntities db = new OnlineJewelryStoreEntities();

        // GET: Admin/Orders
        public ActionResult Index(
            string searchTerm,
            string orderStatus,
            string paymentStatus,
            string paymentMethod,
            DateTime? dateFrom,
            DateTime? dateTo,
            string sortBy = "OrderDate",
            string sortOrder = "desc",
            int page = 1,
            int pageSize = 10)
        {
            try
            {
                // Base query với Include để load related data
                var query = db.Orders
                    .Include(o => o.User)
                    .Include(o => o.OrderItems)
                    .Include(o => o.Payments)
                    .Include(o => o.Address)
                    .AsQueryable();

                // === SEARCH ===
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = searchTerm.Trim().ToLower();
                    query = query.Where(o =>
                        o.OrderID.ToString().Contains(searchTerm) ||
                        o.User.FirstName.ToLower().Contains(searchTerm) ||
                        o.User.LastName.ToLower().Contains(searchTerm) ||
                        o.User.Email.ToLower().Contains(searchTerm) ||
                        (o.User.FirstName + " " + o.User.LastName).ToLower().Contains(searchTerm)
                    );
                }

                // === FILTER BY ORDER STATUS ===
                if (!string.IsNullOrEmpty(orderStatus) && orderStatus != "All")
                {
                    query = query.Where(o => o.Status == orderStatus);
                }

                // === FILTER BY PAYMENT STATUS ===
                if (!string.IsNullOrEmpty(paymentStatus) && paymentStatus != "All")
                {
                    query = query.Where(o => o.Payments.Any(p => p.Status == paymentStatus));
                }

                // === FILTER BY PAYMENT METHOD ===
                if (!string.IsNullOrEmpty(paymentMethod) && paymentMethod != "All")
                {
                    query = query.Where(o => o.Payments.Any(p => p.Method == paymentMethod));
                }

                // === FILTER BY DATE RANGE ===
                if (dateFrom.HasValue)
                {
                    query = query.Where(o => o.OrderDate >= dateFrom.Value);
                }
                if (dateTo.HasValue)
                {
                    // Include toàn bộ ngày dateTo (đến 23:59:59)
                    DateTime dateToEndOfDay = dateTo.Value.Date.AddDays(1).AddSeconds(-1);
                    query = query.Where(o => o.OrderDate <= dateToEndOfDay);
                }

                // === SORTING ===
                switch (sortBy)
                {
                    case "OrderDate":
                        query = sortOrder == "asc"
                            ? query.OrderBy(o => o.OrderDate)
                            : query.OrderByDescending(o => o.OrderDate);
                        break;
                    case "GrandTotal":
                        query = sortOrder == "asc"
                            ? query.OrderBy(o => o.GrandTotal)
                            : query.OrderByDescending(o => o.GrandTotal);
                        break;
                    case "CustomerName":
                        query = sortOrder == "asc"
                            ? query.OrderBy(o => o.User.FirstName).ThenBy(o => o.User.LastName)
                            : query.OrderByDescending(o => o.User.FirstName).ThenByDescending(o => o.User.LastName);
                        break;
                    default:
                        query = query.OrderByDescending(o => o.OrderDate);
                        break;
                }

                // === SUMMARY STATISTICS ===
                DateTime today = DateTime.Today;
                DateTime tomorrow = today.AddDays(1);

                ViewBag.TodayOrdersCount = db.Orders
                    .Count(o => o.OrderDate >= today && o.OrderDate < tomorrow);

                ViewBag.PendingOrdersCount = db.Orders
                    .Count(o => o.Status == "Pending");

                ViewBag.TodayRevenue = db.Orders
                    .Where(o => o.OrderDate >= today && o.OrderDate < tomorrow && o.Status != "Cancelled")
                    .Sum(o => (decimal?)o.GrandTotal) ?? 0;

                ViewBag.NeedProcessingCount = db.Orders
                    .Count(o => o.Status == "Pending" || o.Status == "Processing");

                // === PAGINATION ===
                int totalOrders = query.Count();
                int totalPages = (int)Math.Ceiling(totalOrders / (double)pageSize);

                // Ensure page is valid
                if (page < 1) page = 1;
                if (page > totalPages && totalPages > 0) page = totalPages;

                var orders = query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // === PASS DATA TO VIEW ===
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalOrders = totalOrders;

                // Filters
                ViewBag.SearchTerm = searchTerm;
                ViewBag.OrderStatus = orderStatus;
                ViewBag.PaymentStatus = paymentStatus;
                ViewBag.PaymentMethod = paymentMethod;
                ViewBag.DateFrom = dateFrom;
                ViewBag.DateTo = dateTo;

                // Sorting
                ViewBag.SortBy = sortBy;
                ViewBag.SortOrder = sortOrder;

                return View(orders);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading orders: " + ex.Message;
                return View(new List<Order>());
            }
        }

        // GET: Admin/Orders/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "Order ID is required.";
                return RedirectToAction("Index");
            }

            try
            {
                var order = db.Orders
                    .Include(o => o.User)
                    .Include(o => o.Address)
                    .Include(o => o.Coupon)
                    .Include(o => o.OrderItems.Select(oi => oi.ProductVariant))
                    .Include(o => o.OrderItems.Select(oi => oi.ProductVariant.Product))
                    .Include(o => o.OrderItems.Select(oi => oi.ProductVariant.Product.ProductMedias))
                    .Include(o => o.Payments)
                    .Include(o => o.Invoices)
                    .FirstOrDefault(o => o.OrderID == id);

                if (order == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    return RedirectToAction("Index");
                }

                return View(order);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading order details: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // GET: Admin/Orders/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "Order ID is required.";
                return RedirectToAction("Index");
            }

            try
            {
                var order = db.Orders
                    .Include(o => o.User)
                    .Include(o => o.Address)
                    .Include(o => o.OrderItems.Select(oi => oi.ProductVariant))
                    .Include(o => o.Payments)
                    .FirstOrDefault(o => o.OrderID == id);

                if (order == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    return RedirectToAction("Index");
                }

                // Check if order can be edited
                if (order.Status == "Delivered" || order.Status == "Cancelled")
                {
                    TempData["ErrorMessage"] = "Cannot edit order with status: " + order.Status;
                    return RedirectToAction("Details", new { id = id });
                }

                // Pass current status to view for validation
                ViewBag.CurrentStatus = order.Status;
                ViewBag.AvailableStatuses = GetAvailableStatuses(order.Status);

                return View(order);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading order: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Admin/Orders/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, string status, string trackingNumber, DateTime? deliveryDate, string cancellationReason)
        {
            try
            {
                var order = db.Orders
                    .Include(o => o.OrderItems.Select(oi => oi.ProductVariant))
                    .Include(o => o.Payments)
                    .FirstOrDefault(o => o.OrderID == id);

                if (order == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    return RedirectToAction("Index");
                }

                // Validate: Cannot edit Delivered or Cancelled orders
                if (order.Status == "Delivered" || order.Status == "Cancelled")
                {
                    TempData["ErrorMessage"] = "Cannot edit order with status: " + order.Status;
                    return RedirectToAction("Details", new { id = id });
                }

                // Validate status transition
                if (!IsValidStatusTransition(order.Status, status))
                {
                    TempData["ErrorMessage"] = $"Invalid status transition from {order.Status} to {status}.";
                    ViewBag.CurrentStatus = order.Status;
                    ViewBag.AvailableStatuses = GetAvailableStatuses(order.Status);
                    return View(order);
                }

                // Validate required fields based on status
                if (status == "Shipped" && string.IsNullOrWhiteSpace(trackingNumber))
                {
                    TempData["ErrorMessage"] = "Tracking Number is required when status is Shipped.";
                    ViewBag.CurrentStatus = order.Status;
                    ViewBag.AvailableStatuses = GetAvailableStatuses(order.Status);
                    return View(order);
                }

                if (status == "Cancelled" && string.IsNullOrWhiteSpace(cancellationReason))
                {
                    TempData["ErrorMessage"] = "Cancellation Reason is required when status is Cancelled.";
                    ViewBag.CurrentStatus = order.Status;
                    ViewBag.AvailableStatuses = GetAvailableStatuses(order.Status);
                    return View(order);
                }

                // Update order status
                string oldStatus = order.Status;
                order.Status = status;

                // Handle status-specific logic
                if (status == "Delivered")
                {
                    // Set DeliveredAt to provided date or current date
                    order.DeliveredAt = deliveryDate ?? DateTime.Now;

                    // Update payment status to Captured if not already
                    var payment = order.Payments.FirstOrDefault();
                    if (payment != null && payment.Status != "Captured")
                    {
                        payment.Status = "Captured";
                        payment.CapturedAt = DateTime.Now;
                    }
                }
                else if (status == "Cancelled")
                {
                    // Set cancellation reason
                    order.CancellationReason = cancellationReason;

                    // Restore stock for all items
                    foreach (var item in order.OrderItems)
                    {
                        item.ProductVariant.StockQuantity += item.Quantity;
                    }

                    // Update payment status to Refunded if was Captured
                    var payment = order.Payments.FirstOrDefault();
                    if (payment != null && payment.Status == "Captured")
                    {
                        payment.Status = "Refunded";
                    }
                }

                // Save changes
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Order status updated from {oldStatus} to {status} successfully!";

                // TODO: Send email notification (optional)
                // SendOrderStatusEmail(order, status);

                return RedirectToAction("Details", new { id = id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error updating order: " + ex.Message;
                return RedirectToAction("Details", new { id = id });
            }
        }

        // Helper method: Get available statuses based on current status
        private List<string> GetAvailableStatuses(string currentStatus)
        {
            var statuses = new List<string>();

            switch (currentStatus)
            {
                case "Pending":
                    statuses.Add("Pending");
                    statuses.Add("Processing");
                    statuses.Add("Cancelled");
                    break;
                case "Processing":
                    statuses.Add("Processing");
                    statuses.Add("Shipped");
                    statuses.Add("Cancelled");
                    break;
                case "Shipped":
                    statuses.Add("Shipped");
                    statuses.Add("Delivered");
                    // Cannot cancel after shipped
                    break;
                case "Delivered":
                    statuses.Add("Delivered"); // Cannot change
                    break;
                case "Cancelled":
                    statuses.Add("Cancelled"); // Cannot change
                    break;
                default:
                    statuses.Add(currentStatus);
                    break;
            }

            return statuses;
        }

        // GET: Admin/Orders/GenerateInvoice/5
        public ActionResult GenerateInvoice(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "Order ID is required.";
                return RedirectToAction("Index");
            }

            try
            {
                // ✅ KHÔNG CẦN CONFIG - Rotativa tự tìm file trong ~/Rotativa/

                var order = db.Orders
                    .Include(o => o.User)
                    .Include(o => o.Address)
                    .Include(o => o.Coupon)
                    .Include(o => o.OrderItems.Select(oi => oi.ProductVariant))
                    .Include(o => o.OrderItems.Select(oi => oi.ProductVariant.Product))
                    .Include(o => o.Payments)
                    .Include(o => o.Invoices)
                    .FirstOrDefault(o => o.OrderID == id);

                if (order == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    return RedirectToAction("Index");
                }

                // Check if invoice already exists
                var existingInvoice = order.Invoices.FirstOrDefault();
                if (existingInvoice != null)
                {
                    // Return existing PDF file
                    string existingPath = Server.MapPath(existingInvoice.PDF_URL);
                    if (System.IO.File.Exists(existingPath))
                    {
                        byte[] fileBytes = System.IO.File.ReadAllBytes(existingPath);
                        return File(fileBytes, "application/pdf", $"{existingInvoice.InvoiceNumber}.pdf");
                    }
                }

                // Generate invoice number: INV-{OrderID}-{yyyyMMdd}
                string invoiceNumber = $"INV-{id}-{DateTime.Now:yyyyMMdd}";

                // Generate PDF using Rotativa
                var pdf = new Rotativa.ViewAsPdf("Invoice", order)
                {
                    FileName = $"{invoiceNumber}.pdf",
                    PageSize = Rotativa.Options.Size.A4,
                    PageOrientation = Rotativa.Options.Orientation.Portrait,
                    PageMargins = new Rotativa.Options.Margins(10, 10, 10, 10),
                    CustomSwitches = "--enable-local-file-access"
                };

                // Create directory if not exists
                string folderPath = Server.MapPath("~/Content/Invoices/");
                if (!System.IO.Directory.Exists(folderPath))
                {
                    System.IO.Directory.CreateDirectory(folderPath);
                }

                // Generate PDF bytes
                byte[] pdfBytes = pdf.BuildFile(ControllerContext);

                // Save PDF to file
                string fileName = $"{invoiceNumber}.pdf";
                string filePath = System.IO.Path.Combine(folderPath, fileName);
                System.IO.File.WriteAllBytes(filePath, pdfBytes);

                // Save invoice record to database
                var invoice = new Invoice
                {
                    OrderID = order.OrderID,
                    InvoiceNumber = invoiceNumber,
                    InvoiceDate = DateTime.Now,
                    PDF_URL = $"~/Content/Invoices/{fileName}"
                };
                db.Invoices.Add(invoice);
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Invoice {invoiceNumber} generated successfully!";

                // Return PDF file for download
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error generating invoice: " + ex.Message;
                return RedirectToAction("Details", new { id = id });
            }
        }

        // GET: Admin/Orders/Invoice/5 (Preview invoice in browser)
        public ActionResult Invoice(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(400, "Order ID is required");
            }

            var order = db.Orders
                .Include(o => o.User)
                .Include(o => o.Address)
                .Include(o => o.Coupon)
                .Include(o => o.OrderItems.Select(oi => oi.ProductVariant))
                .Include(o => o.OrderItems.Select(oi => oi.ProductVariant.Product))
                .Include(o => o.Payments)
                .Include(o => o.Invoices)
                .FirstOrDefault(o => o.OrderID == id);

            if (order == null)
            {
                return HttpNotFound();
            }

            // Get or generate invoice number
            var invoice = order.Invoices.FirstOrDefault();
            if (invoice != null)
            {
                ViewBag.InvoiceNumber = invoice.InvoiceNumber;
                ViewBag.InvoiceDate = invoice.InvoiceDate;
            }
            else
            {
                ViewBag.InvoiceNumber = $"INV-{id}-{DateTime.Now:yyyyMMdd}";
                ViewBag.InvoiceDate = DateTime.Now;
            }

            return View(order);
        }

        // Helper method: Validate status transition
        private bool IsValidStatusTransition(string fromStatus, string toStatus)
        {
            // Same status is always valid
            if (fromStatus == toStatus) return true;

            switch (fromStatus)
            {
                case "Pending":
                    return toStatus == "Processing" || toStatus == "Cancelled";
                case "Processing":
                    return toStatus == "Shipped" || toStatus == "Cancelled";
                case "Shipped":
                    return toStatus == "Delivered"; // Cannot cancel after shipped
                case "Delivered":
                    return false; // Cannot change delivered status
                case "Cancelled":
                    return false; // Cannot change cancelled status
                default:
                    return false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}