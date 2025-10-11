using OnlineJewelryStore.Filters;
using OnlineJewelryStore.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;

namespace OnlineJewelryStore.Areas.Admin.Controllers
{
    [AdminAuthorize]
    public class PaymentsController : Controller
    {
        private readonly OnlineJewelryStoreEntities db = new OnlineJewelryStoreEntities();

        #region INDEX - List Payments with Filter/Search

        // GET: Admin/Payments
        public ActionResult Index(PaymentFilterViewModel filter)
        {
            // Query với Include related entities
            var query = db.Payments
                .Include(p => p.Order)
                .Include(p => p.Order.User)
                .AsQueryable();

            // Search by TransactionRef, OrderID, Customer Name
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var searchTerm = filter.SearchTerm.Trim().ToLower();
                query = query.Where(p =>
                    p.TransactionRef.ToLower().Contains(searchTerm) ||
                    p.OrderID.ToString().Contains(searchTerm) ||
                    p.Order.User.FirstName.ToLower().Contains(searchTerm) ||
                    p.Order.User.LastName.ToLower().Contains(searchTerm)
                );
            }

            // Filter by Payment Method
            if (!string.IsNullOrWhiteSpace(filter.Method) && filter.Method != "All")
            {
                query = query.Where(p => p.Method == filter.Method);
            }

            // Filter by Payment Status
            if (!string.IsNullOrWhiteSpace(filter.Status) && filter.Status != "All")
            {
                query = query.Where(p => p.Status == filter.Status);
            }

            // Filter by Date Range
            if (filter.FromDate.HasValue)
            {
                query = query.Where(p => p.CreatedAt >= filter.FromDate.Value);
            }
            if (filter.ToDate.HasValue)
            {
                var toDate = filter.ToDate.Value.AddDays(1); // Include entire day
                query = query.Where(p => p.CreatedAt < toDate);
            }

            // Sorting
            switch (filter.SortBy)
            {
                case "amount_asc":
                    query = query.OrderBy(p => p.Amount);
                    break;
                case "amount_desc":
                    query = query.OrderByDescending(p => p.Amount);
                    break;
                case "date_asc":
                    query = query.OrderBy(p => p.CreatedAt);
                    break;
                case "status":
                    query = query.OrderBy(p => p.Status).ThenByDescending(p => p.CreatedAt);
                    break;
                case "method":
                    query = query.OrderBy(p => p.Method).ThenByDescending(p => p.CreatedAt);
                    break;
                default: // date_desc
                    query = query.OrderByDescending(p => p.CreatedAt);
                    break;
            }

            // Pagination
            filter.Page = filter.Page < 1 ? 1 : filter.Page;
            filter.PageSize = filter.PageSize < 10 ? 20 : filter.PageSize;

            var totalRecords = query.Count();
            filter.TotalPages = (int)Math.Ceiling((double)totalRecords / filter.PageSize);
            filter.TotalRecords = totalRecords;

            var payments = query
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToList();

            filter.Payments = payments;

            return View(filter);
        }

        #endregion

        #region DETAILS - View Payment Details

        // GET: Admin/Payments/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var payment = db.Payments
                .Include(p => p.Order)
                .Include(p => p.Order.User)
                .Include(p => p.Order.Address)
                .Include(p => p.Order.OrderItems)
                .Include(p => p.Order.OrderItems.Select(oi => oi.ProductVariant))
                .Include(p => p.Order.OrderItems.Select(oi => oi.ProductVariant.Product))
                .Include(p => p.Order.Coupon)
                .FirstOrDefault(p => p.PaymentID == id);

            if (payment == null)
            {
                return HttpNotFound();
            }

            return View(payment);
        }

        #endregion

        #region EDIT - Update Payment Status

        // GET: Admin/Payments/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var payment = db.Payments
                .Include(p => p.Order)
                .Include(p => p.Order.User)
                .FirstOrDefault(p => p.PaymentID == id);

            if (payment == null)
            {
                return HttpNotFound();
            }

            // Prepare status dropdown based on current status
            ViewBag.AllowedStatuses = GetAllowedStatusTransitions(payment.Status);
            ViewBag.PaymentMethods = GetPaymentMethods();

            return View(payment);
        }

        // POST: Admin/Payments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Payment payment, string newStatus)
        {
            if (ModelState.IsValid)
            {
                var existingPayment = db.Payments
                    .Include(p => p.Order)
                    .FirstOrDefault(p => p.PaymentID == payment.PaymentID);

                if (existingPayment == null)
                {
                    return HttpNotFound();
                }

                // Validate status transition
                if (!string.IsNullOrEmpty(newStatus) && newStatus != existingPayment.Status)
                {
                    var allowedStatuses = GetAllowedStatusTransitions(existingPayment.Status);
                    if (!allowedStatuses.Contains(newStatus))
                    {
                        TempData["ErrorMessage"] = $"Cannot change status from '{existingPayment.Status}' to '{newStatus}'.";
                        return RedirectToAction("Edit", new { id = payment.PaymentID });
                    }

                    existingPayment.Status = newStatus;

                    // Update CapturedAt when status becomes Captured
                    if (newStatus == "Captured")
                    {
                        existingPayment.CapturedAt = DateTime.Now;
                    }
                }

                // Update other fields
                existingPayment.Method = payment.Method;
                existingPayment.Provider = payment.Provider;
                existingPayment.TransactionRef = payment.TransactionRef;

                try
                {
                    db.SaveChanges();
                    TempData["SuccessMessage"] = "Payment updated successfully.";
                    return RedirectToAction("Details", new { id = payment.PaymentID });
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error updating payment: {ex.Message}";
                }
            }

            ViewBag.AllowedStatuses = GetAllowedStatusTransitions(payment.Status);
            ViewBag.PaymentMethods = GetPaymentMethods();
            return View(payment);
        }

        #endregion

        #region CAPTURE PAYMENT

        // POST: Admin/Payments/Capture/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Capture(int id)
        {
            var payment = db.Payments
                .Include(p => p.Order)
                .FirstOrDefault(p => p.PaymentID == id);

            if (payment == null)
            {
                return HttpNotFound();
            }

            // Validate: Can only capture Pending or Authorized payments
            if (payment.Status != "Pending" && payment.Status != "Authorized")
            {
                TempData["ErrorMessage"] = $"Cannot capture payment with status '{payment.Status}'. Only Pending or Authorized payments can be captured.";
                return RedirectToAction("Details", new { id });
            }

            // Validate: Amount must equal Order.GrandTotal
            if (Math.Abs(payment.Amount - payment.Order.GrandTotal) > 0.01m)
            {
                TempData["ErrorMessage"] = "Payment amount does not match order grand total. Please update the amount first.";
                return RedirectToAction("Details", new { id });
            }

            try
            {
                payment.Status = "Captured";
                payment.CapturedAt = DateTime.Now;

                // If TransactionRef is empty, generate one
                if (string.IsNullOrWhiteSpace(payment.TransactionRef))
                {
                    payment.TransactionRef = $"TXN-{payment.PaymentID}-{DateTime.Now:yyyyMMddHHmmss}";
                }

                db.SaveChanges();

                TempData["SuccessMessage"] = $"Payment #{payment.PaymentID} has been captured successfully. Amount: {payment.Amount:N0} {payment.Currency}";
                return RedirectToAction("Details", new { id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error capturing payment: {ex.Message}";
                return RedirectToAction("Details", new { id });
            }
        }

        #endregion

        #region AUTHORIZE PAYMENT

        // POST: Admin/Payments/Authorize/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Authorize(int id)
        {
            var payment = db.Payments.Find(id);

            if (payment == null)
            {
                return HttpNotFound();
            }

            // Validate: Can only authorize Pending payments
            if (payment.Status != "Pending")
            {
                TempData["ErrorMessage"] = $"Cannot authorize payment with status '{payment.Status}'. Only Pending payments can be authorized.";
                return RedirectToAction("Details", new { id });
            }

            try
            {
                payment.Status = "Authorized";

                // Generate TransactionRef if empty
                if (string.IsNullOrWhiteSpace(payment.TransactionRef))
                {
                    payment.TransactionRef = $"AUTH-{payment.PaymentID}-{DateTime.Now:yyyyMMddHHmmss}";
                }

                db.SaveChanges();

                TempData["SuccessMessage"] = $"Payment #{payment.PaymentID} has been authorized successfully.";
                return RedirectToAction("Details", new { id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error authorizing payment: {ex.Message}";
                return RedirectToAction("Details", new { id });
            }
        }

        #endregion

        #region REFUND PAYMENT

        // POST: Admin/Payments/Refund/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Refund(int id, string refundReason)
        {
            var payment = db.Payments
                .Include(p => p.Order)
                .FirstOrDefault(p => p.PaymentID == id);

            if (payment == null)
            {
                return HttpNotFound();
            }

            // Validate: Can only refund Captured payments
            if (payment.Status != "Captured")
            {
                TempData["ErrorMessage"] = $"Cannot refund payment with status '{payment.Status}'. Only Captured payments can be refunded.";
                return RedirectToAction("Details", new { id });
            }

            try
            {
                payment.Status = "Refunded";

                if (payment.Order != null && payment.Order.Status != "Cancelled")
                {
                    payment.Order.Status = "Cancelled";
                    payment.Order.CancellationReason = string.IsNullOrWhiteSpace(refundReason)
                        ? "Payment refunded"
                        : $"Payment refunded: {refundReason}";
                }

                db.SaveChanges();

                TempData["SuccessMessage"] = $"Payment #{payment.PaymentID} has been refunded successfully. Amount: {payment.Amount:N0} {payment.Currency}";
                return RedirectToAction("Details", new { id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error refunding payment: {ex.Message}";
                return RedirectToAction("Details", new { id });
            }
        }

        #endregion

        #region MARK AS FAILED

        // POST: Admin/Payments/MarkAsFailed/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkAsFailed(int id, string failureReason)
        {
            var payment = db.Payments.Find(id);

            if (payment == null)
            {
                return HttpNotFound();
            }

            if (payment.Status == "Captured" || payment.Status == "Refunded")
            {
                TempData["ErrorMessage"] = $"Cannot mark '{payment.Status}' payment as Failed. Please refund instead.";
                return RedirectToAction("Details", new { id });
            }

            try
            {
                payment.Status = "Failed";

                if (!string.IsNullOrWhiteSpace(failureReason))
                {
                    payment.TransactionRef = string.IsNullOrWhiteSpace(payment.TransactionRef)
                        ? $"FAILED: {failureReason}"
                        : $"{payment.TransactionRef} | FAILED: {failureReason}";
                }

                db.SaveChanges();

                TempData["SuccessMessage"] = $"Payment #{payment.PaymentID} has been marked as Failed.";
                return RedirectToAction("Details", new { id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error marking payment as failed: {ex.Message}";
                return RedirectToAction("Details", new { id });
            }
        }
        private string[] GetAllowedStatusTransitions(string currentStatus)
        {
            switch (currentStatus)
            {
                case "Pending":
                    return new[] { "Pending", "Authorized", "Captured", "Failed" };
                case "Authorized":
                    return new[] { "Authorized", "Captured", "Failed" };
                case "Captured":
                    return new[] { "Captured", "Refunded" };
                case "Failed":
                    return new[] { "Failed" };
                case "Refunded":
                    return new[] { "Refunded" };
                default:
                    return new[] { currentStatus };
            }
        }

        private SelectListItem[] GetPaymentMethods()
        {
            return new[]
            {
                new SelectListItem { Value = "COD", Text = "Cash on Delivery (COD)" },
                new SelectListItem { Value = "Card", Text = "Credit/Debit Card" },
                new SelectListItem { Value = "PayPal", Text = "PayPal" },
                new SelectListItem { Value = "VNPay", Text = "VNPay" },
                new SelectListItem { Value = "MoMo", Text = "MoMo" },
                new SelectListItem { Value = "Bank", Text = "Bank Transfer" }
            };
        }
        public static string GetStatusBadgeClass(string status)
        {
            switch (status)
            {
                case "Pending":
                    return "warning";
                case "Authorized":
                    return "info";
                case "Captured":
                    return "success";
                case "Failed":
                    return "danger";
                case "Refunded":
                    return "secondary";
                default:
                    return "light";
            }
        }
        public static string GetMethodIcon(string method)
        {
            switch (method)
            {
                case "COD":
                    return "bi-cash-coin";
                case "Card":
                    return "bi-credit-card";
                case "PayPal":
                    return "bi-paypal";
                case "VNPay":
                case "Bank":
                    return "bi-bank";
                case "MoMo":
                    return "bi-phone";
                default:
                    return "bi-wallet2";
            }
        }

        public class PaymentFilterViewModel
        {
            [Display(Name = "Search")]
            public string SearchTerm { get; set; }

            [Display(Name = "Payment Method")]
            public string Method { get; set; }

            [Display(Name = "Payment Status")]
            public string Status { get; set; }

            [Display(Name = "From Date")]
            [DataType(DataType.Date)]
            public DateTime? FromDate { get; set; }

            [Display(Name = "To Date")]
            [DataType(DataType.Date)]
            public DateTime? ToDate { get; set; }
            public string SortBy { get; set; } = "date_desc";
            public int Page { get; set; } = 1;
            public int PageSize { get; set; } = 20;
            public int TotalPages { get; set; }
            public int TotalRecords { get; set; }
            public IEnumerable<Payment> Payments { get; set; }
            public bool HasPreviousPage => Page > 1;
            public bool HasNextPage => Page < TotalPages;
        }

        #endregion

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