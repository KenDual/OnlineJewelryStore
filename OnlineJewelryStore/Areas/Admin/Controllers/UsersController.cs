using OnlineJewelryStore.Filters;
using OnlineJewelryStore.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace OnlineJewelryStore.Areas.Admin.Controllers
{
    [AdminAuthorize]
    public class UsersController : Controller
    {
        private OnlineJewelryStoreEntities db = new OnlineJewelryStoreEntities();

        // GET: Admin/Users
        public ActionResult Index(string searchTerm, string roleFilter, string sortOrder, int page = 1)
        {
            ViewBag.ActiveMenu = "Users";

            var users = db.Users.AsQueryable();

            // tìm kiếm
            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.Trim();
                users = users.Where(u =>
                    u.FirstName.Contains(searchTerm) ||
                    u.LastName.Contains(searchTerm) ||
                    u.Email.Contains(searchTerm) ||
                    u.Phone.Contains(searchTerm)
                );
            }
            if (!string.IsNullOrEmpty(roleFilter) && roleFilter != "All")
            {
                users = users.Where(u => u.Role == roleFilter);
            }
            ViewBag.CurrentSort = sortOrder;
            ViewBag.NameSortParam = String.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewBag.EmailSortParam = sortOrder == "email" ? "email_desc" : "email";
            ViewBag.DateSortParam = sortOrder == "date" ? "date_desc" : "date";

            switch (sortOrder)
            {
                case "name_desc":
                    users = users.OrderByDescending(u => u.FirstName).ThenByDescending(u => u.LastName);
                    break;
                case "email":
                    users = users.OrderBy(u => u.Email);
                    break;
                case "email_desc":
                    users = users.OrderByDescending(u => u.Email);
                    break;
                case "date":
                    users = users.OrderBy(u => u.RegistrationDate);
                    break;
                case "date_desc":
                    users = users.OrderByDescending(u => u.RegistrationDate);
                    break;
                default:
                    users = users.OrderBy(u => u.FirstName).ThenBy(u => u.LastName);
                    break;
            }

            // phân trang
            int pageSize = 20;
            int totalUsers = users.Count();
            int totalPages = (int)Math.Ceiling((double)totalUsers / pageSize);

            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var pagedUsers = users
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var userStatistics = new Dictionary<int, dynamic>();
            foreach (var user in pagedUsers)
            {
                var orderCount = db.Orders.Count(o => o.UserID == user.UserID);
                var totalSpent = db.Orders
                    .Where(o => o.UserID == user.UserID)
                    .Sum(o => (decimal?)o.GrandTotal) ?? 0;

                userStatistics[user.UserID] = Tuple.Create(orderCount, totalSpent);
            }

            ViewBag.SearchTerm = searchTerm;
            ViewBag.RoleFilter = roleFilter;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalUsers = totalUsers;
            ViewBag.PageSize = pageSize;
            ViewBag.UserStatistics = userStatistics;

            return View(pagedUsers);
        }

        // GET: Admin/Users/Create
        public ActionResult Create()
        {
            ViewBag.ActiveMenu = "Users";
            return View();
        }

        // POST: Admin/Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "UserID,FirstName,LastName,Email,Phone,PasswordHash,Role")] User user)
        {
            if (ModelState.IsValid)
            {
                // Check email duplicate
                if (db.Users.Any(u => u.Email == user.Email))
                {
                    ModelState.AddModelError("Email", "Email này đã tồn tại.");
                    return View(user);
                }

                // Validate password length (simple validation)
                if (string.IsNullOrWhiteSpace(user.PasswordHash) || user.PasswordHash.Length < 3)
                {
                    ModelState.AddModelError("PasswordHash", "Password must be at least 3 characters.");
                    return View(user);
                }

                // Auto-set fields
                user.RegistrationDate = DateTime.Now;
                user.LastLogin = null;
                user.SocialLoginProvider = null;

                db.Users.Add(user);
                db.SaveChanges();

                TempData["Success"] = $"User '{user.FirstName} {user.LastName}' created successfully!";
                return RedirectToAction("Index");
            }

            return View(user);
        }

        // GET: Admin/Users/Details/5
        public ActionResult Details(int? id)
        {
            ViewBag.ActiveMenu = "Users";

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            User user = db.Users.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            // === TAB 2: ADDRESSES ===
            ViewBag.Addresses = db.Addresses
                .Where(a => a.UserID == id)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedAt)
                .ToList();

            // === TAB 3: ORDERS ===
            ViewBag.Orders = db.Orders
                .Where(o => o.UserID == id)
                .OrderByDescending(o => o.OrderDate)
                .Take(20) // Latest 20 orders
                .ToList();

            ViewBag.AllOrdersCount = db.Orders.Count(o => o.UserID == id);

            // === TAB 4: WISHLIST ===
            ViewBag.Wishlist = db.Wishlists
                .Where(w => w.UserID == id)
                .Include(w => w.ProductVariant)
                .Include(w => w.ProductVariant.Product)
                .OrderByDescending(w => w.AddedAt)
                .ToList();

            // === TAB 5: REVIEWS ===
            ViewBag.Reviews = db.Reviews
                .Where(r => r.UserID == id)
                .Include(r => r.Product)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            // === TAB 6: CART ===
            var cart = db.Carts
                .Include(c => c.CartItems.Select(ci => ci.ProductVariant))
                .Include(c => c.CartItems.Select(ci => ci.ProductVariant.Product))
                .FirstOrDefault(c => c.UserID == id);
            ViewBag.Cart = cart;

            // === CALCULATE STATISTICS ===
            var orders = db.Orders.Where(o => o.UserID == id).ToList();
            ViewBag.TotalOrders = orders.Count;
            ViewBag.TotalSpent = orders.Sum(o => (decimal?)o.GrandTotal) ?? 0;
            ViewBag.AverageOrderValue = ViewBag.TotalOrders > 0 ? ViewBag.TotalSpent / ViewBag.TotalOrders : 0;

            // Order status breakdown
            ViewBag.PendingOrders = orders.Count(o => o.Status == "Pending");
            ViewBag.ProcessingOrders = orders.Count(o => o.Status == "Processing");
            ViewBag.ShippedOrders = orders.Count(o => o.Status == "Shipped");
            ViewBag.DeliveredOrders = orders.Count(o => o.Status == "Delivered");
            ViewBag.CancelledOrders = orders.Count(o => o.Status == "Cancelled");

            // Cart statistics
            if (cart != null)
            {
                var cartItems = cart.CartItems.ToList();
                ViewBag.CartTotalItems = cartItems.Sum(ci => ci.Quantity);
                ViewBag.CartTotalValue = cartItems.Sum(ci =>
                    (ci.ProductVariant.Product.BasePrice + ci.ProductVariant.AdditionalPrice) * ci.Quantity);
            }
            else
            {
                ViewBag.CartTotalItems = 0;
                ViewBag.CartTotalValue = 0;
            }

            // Review statistics
            var reviews = db.Reviews.Where(r => r.UserID == id).ToList();
            ViewBag.TotalReviews = reviews.Count;
            ViewBag.AverageRatingGiven = reviews.Count > 0 ? reviews.Average(r => (double)r.Rating) : 0;

            return View(user);
        }

        // GET: Admin/Users/Edit/5
        public ActionResult Edit(int? id)
        {
            ViewBag.ActiveMenu = "Users";

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            User user = db.Users.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            return View(user);
        }

        // POST: Admin/Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "UserID,FirstName,LastName,Email,Phone,PasswordHash,Role")] User user)
        {
            if (ModelState.IsValid)
            {
                if (db.Users.Any(u => u.Email == user.Email && u.UserID != user.UserID))
                {
                    ModelState.AddModelError("Email", "Email này đã được sử dụng.");
                    return View(user);
                }

                var existingUser = db.Users.Find(user.UserID);
                if (existingUser == null)
                {
                    return HttpNotFound();
                }
                existingUser.FirstName = user.FirstName;
                existingUser.LastName = user.LastName;
                existingUser.Email = user.Email;
                existingUser.Phone = user.Phone;
                existingUser.PasswordHash = user.PasswordHash;
                existingUser.Role = user.Role;

                db.SaveChanges();

                TempData["Success"] = "User updated successfully!";
                return RedirectToAction("Index");
            }

            return View(user);
        }

        // GET: Admin/Users/Delete/5
        public ActionResult Delete(int? id)
        {
            ViewBag.ActiveMenu = "Users";

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            User user = db.Users.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            // Calculate what will be deleted
            ViewBag.OrderCount = db.Orders.Count(o => o.UserID == id);
            ViewBag.AddressCount = db.Addresses.Count(a => a.UserID == id);
            ViewBag.WishlistCount = db.Wishlists.Count(w => w.UserID == id);
            ViewBag.ReviewCount = db.Reviews.Count(r => r.UserID == id);

            var cart = db.Carts.FirstOrDefault(c => c.UserID == id);
            ViewBag.CartItemCount = cart != null ? db.CartItems.Count(ci => ci.CartID == cart.CartID) : 0;

            return View(user);
        }

        // POST: Admin/Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            User user = db.Users.Find(id);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("Index");
            }

            var orderCount = db.Orders.Count(o => o.UserID == id);
            if (orderCount > 0)
            {
                TempData["Error"] = $"Cannot delete user '{user.FirstName} {user.LastName}' because they have {orderCount} order(s). Order history must be preserved.";
                return RedirectToAction("Index");
            }

            string userName = $"{user.FirstName} {user.LastName}";


            try
            {
                db.Users.Remove(user);
                db.SaveChanges();

                TempData["Success"] = $"User '{userName}' deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting user: {ex.Message}";
            }

            return RedirectToAction("Index");
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