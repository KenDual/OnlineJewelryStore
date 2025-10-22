using OnlineJewelryStore.Models;
using OnlineJewelryStore.ViewModels.Address;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace OnlineJewelryStore.Controllers
{
    [Authorize]
    public class AddressesController : Controller
    {
        private readonly OnlineJewelryStoreEntities db;

        public AddressesController()
        {
            db = new OnlineJewelryStoreEntities();
        }

        /// <summary>
        /// GET: /Addresses
        /// Hiển thị danh sách addresses của user
        /// </summary>
        [HttpGet]
        public ActionResult Index()
        {
            try
            {
                int userId = GetCurrentUserId();

                var addresses = db.Addresses
                    .Where(a => a.UserID == userId)
                    .OrderByDescending(a => a.IsDefault)
                    .ThenByDescending(a => a.CreatedAt)
                    .Select(a => new AddressListViewModel
                    {
                        AddressID = a.AddressID,
                        StreetAddress = a.StreetAddress,
                        City = a.City,
                        State = a.State,
                        PostalCode = a.PostalCode,
                        Country = a.Country,
                        Phone = a.Phone,
                        IsDefault = a.IsDefault,
                        CreatedAt = a.CreatedAt
                    })
                    .ToList();

                return View(addresses);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Addresses Index Error: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading addresses. Please try again.";
                return View();
            }
        }

        /// <summary>
        /// GET: /Addresses/Create
        /// Hiển thị form thêm địa chỉ mới
        /// </summary>
        [HttpGet]
        public ActionResult Create(string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            var model = new AddressViewModel
            {
                Country = "Vietnam", // Default country
                IsDefault = false
            };

            return View(model);
        }

        /// <summary>
        /// POST: /Addresses/Create
        /// Xử lý thêm địa chỉ mới
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(AddressViewModel model, string returnUrl = null)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    ViewBag.ReturnUrl = returnUrl;
                    return View(model);
                }

                int userId = GetCurrentUserId();

                // Nếu set làm default, unset các address khác
                if (model.IsDefault)
                {
                    var existingAddresses = db.Addresses.Where(a => a.UserID == userId).ToList();
                    foreach (var addr in existingAddresses)
                    {
                        addr.IsDefault = false;
                    }
                }
                else
                {
                    // Nếu đây là địa chỉ đầu tiên, tự động set default
                    bool hasAddresses = db.Addresses.Any(a => a.UserID == userId);
                    if (!hasAddresses)
                    {
                        model.IsDefault = true;
                    }
                }

                // Tạo Address mới
                var address = new Address
                {
                    UserID = userId,
                    StreetAddress = model.StreetAddress.Trim(),
                    City = model.City.Trim(),
                    State = model.State?.Trim(),
                    PostalCode = model.PostalCode.Trim(),
                    Country = model.Country.Trim(),
                    Phone = model.Phone.Trim(),
                    IsDefault = model.IsDefault,
                    CreatedAt = DateTime.Now
                };

                db.Addresses.Add(address);
                db.SaveChanges();

                TempData["SuccessMessage"] = "Address added successfully!";

                // Redirect về returnUrl nếu có (từ Checkout)
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Create Address Error: {ex.Message}");
                ModelState.AddModelError("", "Error creating address. Please try again.");
                ViewBag.ReturnUrl = returnUrl;
                return View(model);
            }
        }

        /// <summary>
        /// GET: /Addresses/Edit/{id}
        /// Hiển thị form chỉnh sửa địa chỉ
        /// </summary>
        [HttpGet]
        public ActionResult Edit(int id)
        {
            try
            {
                int userId = GetCurrentUserId();

                var address = db.Addresses.FirstOrDefault(a => a.AddressID == id && a.UserID == userId);

                if (address == null)
                {
                    TempData["ErrorMessage"] = "Address not found.";
                    return RedirectToAction("Index");
                }

                var model = new AddressViewModel
                {
                    AddressID = address.AddressID,
                    StreetAddress = address.StreetAddress,
                    City = address.City,
                    State = address.State,
                    PostalCode = address.PostalCode,
                    Country = address.Country,
                    Phone = address.Phone,
                    IsDefault = address.IsDefault,
                    CreatedAt = address.CreatedAt
                };

                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Edit Address Error: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading address. Please try again.";
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// POST: /Addresses/Edit/{id}
        /// Xử lý cập nhật địa chỉ
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(AddressViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                int userId = GetCurrentUserId();

                var address = db.Addresses.FirstOrDefault(a => a.AddressID == model.AddressID && a.UserID == userId);

                if (address == null)
                {
                    TempData["ErrorMessage"] = "Address not found.";
                    return RedirectToAction("Index");
                }

                // Nếu set làm default, unset các address khác
                if (model.IsDefault && !address.IsDefault)
                {
                    var existingAddresses = db.Addresses.Where(a => a.UserID == userId && a.AddressID != model.AddressID).ToList();
                    foreach (var addr in existingAddresses)
                    {
                        addr.IsDefault = false;
                    }
                }

                // Update address
                address.StreetAddress = model.StreetAddress.Trim();
                address.City = model.City.Trim();
                address.State = model.State?.Trim();
                address.PostalCode = model.PostalCode.Trim();
                address.Country = model.Country.Trim();
                address.Phone = model.Phone.Trim();
                address.IsDefault = model.IsDefault;

                db.Entry(address).State = EntityState.Modified;
                db.SaveChanges();

                TempData["SuccessMessage"] = "Address updated successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update Address Error: {ex.Message}");
                ModelState.AddModelError("", "Error updating address. Please try again.");
                return View(model);
            }
        }

        /// <summary>
        /// POST: /Addresses/Delete/{id}
        /// Xóa địa chỉ
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            try
            {
                int userId = GetCurrentUserId();

                var address = db.Addresses.FirstOrDefault(a => a.AddressID == id && a.UserID == userId);

                if (address == null)
                {
                    TempData["ErrorMessage"] = "Address not found.";
                    return RedirectToAction("Index");
                }

                // Kiểm tra address có đang được sử dụng trong Orders không
                bool isUsedInOrders = db.Orders.Any(o => o.ShippingAddressID == id);
                if (isUsedInOrders)
                {
                    TempData["ErrorMessage"] = "Cannot delete this address. It is being used in previous orders.";
                    return RedirectToAction("Index");
                }

                // Nếu xóa address default, set address khác làm default
                if (address.IsDefault)
                {
                    var nextAddress = db.Addresses
                        .Where(a => a.UserID == userId && a.AddressID != id)
                        .OrderByDescending(a => a.CreatedAt)
                        .FirstOrDefault();

                    if (nextAddress != null)
                    {
                        nextAddress.IsDefault = true;
                    }
                }

                db.Addresses.Remove(address);
                db.SaveChanges();

                TempData["SuccessMessage"] = "Address deleted successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Delete Address Error: {ex.Message}");
                TempData["ErrorMessage"] = "Error deleting address. Please try again.";
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// POST: /Addresses/SetDefault/{id} (AJAX)
        /// Set địa chỉ làm default
        /// </summary>
        [HttpPost]
        public JsonResult SetDefault(int id)
        {
            try
            {
                int userId = GetCurrentUserId();

                var address = db.Addresses.FirstOrDefault(a => a.AddressID == id && a.UserID == userId);

                if (address == null)
                {
                    return Json(new { success = false, message = "Address not found." });
                }

                // Unset all default addresses
                var existingAddresses = db.Addresses.Where(a => a.UserID == userId).ToList();
                foreach (var addr in existingAddresses)
                {
                    addr.IsDefault = false;
                }

                // Set new default
                address.IsDefault = true;

                db.SaveChanges();

                return Json(new { success = true, message = "Default address updated successfully!" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetDefault Error: {ex.Message}");
                return Json(new { success = false, message = "Error setting default address." });
            }
        }

        /// <summary>
        /// Helper method để lấy UserID từ authentication
        /// </summary>
        private int GetCurrentUserId()
        {
            if (User.Identity.IsAuthenticated)
            {
                // Option 1: Từ Session
                if (Session["UserID"] != null)
                {
                    return (int)Session["UserID"];
                }

                // Option 2: Từ User.Identity.Name (nếu lưu UserID)
                if (int.TryParse(User.Identity.Name, out int userId))
                {
                    return userId;
                }

                // Option 3: Query từ database bằng email
                var email = User.Identity.Name;
                var user = db.Users.FirstOrDefault(u => u.Email == email);
                if (user != null)
                {
                    return user.UserID;
                }
            }

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