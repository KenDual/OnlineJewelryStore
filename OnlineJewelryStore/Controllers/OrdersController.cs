using OnlineJewelryStore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;

namespace OnlineJewelryStore.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private OnlineJewelryStoreEntities db = new OnlineJewelryStoreEntities();

        // GET: Orders
        // GET: Orders?status=Pending
        public ActionResult Index(string status = "All")
        {
            try
            {
                // Get current user ID from authentication
                int userId = GetCurrentUserId();

                // Base query - get all orders for current user
                var ordersQuery = db.Orders
                    .Where(o => o.UserID == userId)
                    .Include(o => o.OrderItems.Select(oi => oi.ProductVariant.Product))
                    .Include(o => o.Address)
                    .Include(o => o.Coupon)
                    .Include(o => o.Payments);

                // Apply status filter
                if (status != "All")
                {
                    ordersQuery = ordersQuery.Where(o => o.Status == status);
                }

                // Get orders sorted by date (newest first)
                var orders = ordersQuery
                    .OrderByDescending(o => o.OrderDate)
                    .ToList();

                // Pass current status to view for tab highlighting
                ViewBag.CurrentStatus = status;

                // Pass status counts for tabs
                ViewBag.AllCount = db.Orders.Count(o => o.UserID == userId);
                ViewBag.PendingCount = db.Orders.Count(o => o.UserID == userId && o.Status == "Pending");
                ViewBag.ProcessingCount = db.Orders.Count(o => o.UserID == userId && o.Status == "Processing");
                ViewBag.ShippedCount = db.Orders.Count(o => o.UserID == userId && o.Status == "Shipped");
                ViewBag.DeliveredCount = db.Orders.Count(o => o.UserID == userId && o.Status == "Delivered");
                ViewBag.CancelledCount = db.Orders.Count(o => o.UserID == userId && o.Status == "Cancelled");

                return View(orders);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading orders: " + ex.Message;
                return View();
            }
        }

        // GET: Orders/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            try
            {
                int userId = GetCurrentUserId();

                // Load order with all related data
                var order = db.Orders
                    .Include(o => o.OrderItems.Select(oi => oi.ProductVariant))
                    .Include(o => o.OrderItems.Select(oi => oi.ProductVariant.Product))
                    .Include(o => o.OrderItems.Select(oi => oi.ProductVariant.Product.ProductMedias))
                    .Include(o => o.Address)
                    .Include(o => o.Coupon)
                    .Include(o => o.Payments)
                    .Include(o => o.Invoices)
                    .Include(o => o.User)
                    .FirstOrDefault(o => o.OrderID == id);

                if (order == null)
                {
                    return HttpNotFound();
                }

                // Validate order belongs to current user
                if (order.UserID != userId)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "You don't have permission to view this order.");
                }

                return View(order);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading order details: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Orders/CancelOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelOrder(int orderId, string cancellationReason)
        {
            if (string.IsNullOrWhiteSpace(cancellationReason))
            {
                TempData["ErrorMessage"] = "Please provide a cancellation reason.";
                return RedirectToAction("Details", new { id = orderId });
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    int userId = GetCurrentUserId();

                    // Get order
                    var order = db.Orders
                        .Include(o => o.OrderItems)
                        .Include(o => o.Payments)
                        .FirstOrDefault(o => o.OrderID == orderId);

                    if (order == null)
                    {
                        TempData["ErrorMessage"] = "Order not found.";
                        return RedirectToAction("Index");
                    }

                    // Validate order belongs to current user
                    if (order.UserID != userId)
                    {
                        TempData["ErrorMessage"] = "You don't have permission to cancel this order.";
                        return RedirectToAction("Index");
                    }

                    // Check if order can be cancelled (only Pending orders)
                    if (order.Status != "Pending")
                    {
                        TempData["ErrorMessage"] = $"Cannot cancel order. Only orders with 'Pending' status can be cancelled. Current status: {order.Status}";
                        return RedirectToAction("Details", new { id = orderId });
                    }

                    // Update order status
                    order.Status = "Cancelled";
                    order.CancellationReason = cancellationReason;

                    // Restore stock quantity for each order item
                    foreach (var orderItem in order.OrderItems)
                    {
                        var variant = db.ProductVariants.Find(orderItem.VariantID);
                        if (variant != null)
                        {
                            variant.StockQuantity += orderItem.Quantity;
                        }
                    }

                    var payment = order.Payments.FirstOrDefault();

                    if (payment != null)
                    {
                        // If payment was captured, mark as refunded
                        if (payment.Status == "Captured")
                        {
                            payment.Status = "Refunded";
                        }
                        else if (payment.Status == "Pending" || payment.Status == "Authorized")
                        {
                            payment.Status = "Failed";
                        }
                    }

                    // Save changes
                    db.SaveChanges();
                    transaction.Commit();

                    TempData["SuccessMessage"] = "Order cancelled successfully. Stock has been restored.";
                    return RedirectToAction("Details", new { id = orderId });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["ErrorMessage"] = "Error cancelling order: " + ex.Message;
                    return RedirectToAction("Details", new { id = orderId });
                }
            }
        }

        // GET: Orders/TrackOrder/5
        public ActionResult TrackOrder(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            try
            {
                int userId = GetCurrentUserId();

                // Get order
                var order = db.Orders
                    .Include(o => o.Address)
                    .FirstOrDefault(o => o.OrderID == id);

                if (order == null)
                {
                    return HttpNotFound();
                }

                // Validate order belongs to current user
                if (order.UserID != userId)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "You don't have permission to track this order.");
                }

                // Create tracking timeline data
                ViewBag.OrderPlacedDate = order.OrderDate;
                ViewBag.EstimatedProcessingDate = order.OrderDate.AddDays(1);
                ViewBag.EstimatedShippingDate = order.OrderDate.AddDays(2);
                ViewBag.EstimatedDeliveryDate = order.OrderDate.AddDays(5);
                ViewBag.ActualDeliveryDate = order.DeliveredAt;

                return View(order);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading tracking information: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // GET: Orders/DownloadInvoice/5
        public ActionResult DownloadInvoice(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            try
            {
                int userId = GetCurrentUserId();

                // Get order with invoice
                var order = db.Orders
                    .Include(o => o.Invoices)
                    .Include(o => o.User)
                    .Include(o => o.Address)
                    .Include(o => o.OrderItems.Select(oi => oi.ProductVariant))
                    .Include(o => o.OrderItems.Select(oi => oi.ProductVariant.Product))
                    .Include(o => o.Coupon)
                    .Include(o => o.Payments)
                    .FirstOrDefault(o => o.OrderID == id);

                if (order == null)
                {
                    return HttpNotFound();
                }

                // Validate order belongs to current user
                if (order.UserID != userId)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "You don't have permission to download this invoice.");
                }

                // Check if invoice exists
                if (order.Invoices == null)
                {
                    TempData["ErrorMessage"] = "Invoice not found for this order.";
                    return RedirectToAction("Details", new { id = id });
                }

                // If PDF URL exists, redirect to it
                var invoice = order.Invoices.FirstOrDefault();

                if (invoice != null && !string.IsNullOrEmpty(invoice.PDF_URL))
                {
                    return Redirect(invoice.PDF_URL);
                }
                else
                {
                    // Generate invoice HTML view for printing
                    return View("InvoiceTemplate", order);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error downloading invoice: " + ex.Message;
                return RedirectToAction("Details", new { id = id });
            }
        }

        // AJAX: Get order status update
        [HttpGet]
        public JsonResult GetOrderStatus(int orderId)
        {
            try
            {
                int userId = GetCurrentUserId();

                var order = db.Orders
                    .Where(o => o.OrderID == orderId && o.UserID == userId)
                    .Select(o => new
                    {
                        o.OrderID,
                        o.Status,
                        o.OrderDate,
                        o.DeliveredAt
                    })
                    .FirstOrDefault();

                if (order == null)
                {
                    return Json(new { success = false, message = "Order not found" }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    success = true,
                    status = order.Status,
                    orderDate = order.OrderDate.ToString("dd/MM/yyyy HH:mm"),
                    deliveredAt = order.DeliveredAt?.ToString("dd/MM/yyyy HH:mm")
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // Helper method to get current user ID
        private int GetCurrentUserId()
        {
            // Method 1: From Forms Authentication
            if (User.Identity.IsAuthenticated)
            {
                // Parse UserID from Identity Name (assuming you stored UserID in Name during login)
                if (int.TryParse(User.Identity.Name, out int userId))
                {
                    return userId;
                }
            }

            // Method 2: From Session (alternative)
            if (Session["UserID"] != null)
            {
                return (int)Session["UserID"];
            }

            // If not found, throw exception or redirect to login
            throw new UnauthorizedAccessException("User not authenticated");
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