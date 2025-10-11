using OnlineJewelryStore.Filters;
using OnlineJewelryStore.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;

namespace OnlineJewelryStore.Areas.Admin.Controllers
{
    [AdminAuthorize]
    public class CouponsController : Controller
    {
        private readonly OnlineJewelryStoreEntities db = new OnlineJewelryStoreEntities();

        // GET: Admin/Coupons
        public ActionResult Index(string searchCode, bool? isActive, string sortOrder)
        {
            // Base query
            var coupons = db.Coupons.AsQueryable();

            // Search by Code
            if (!string.IsNullOrWhiteSpace(searchCode))
            {
                coupons = coupons.Where(c => c.Code.Contains(searchCode));
                ViewBag.SearchCode = searchCode;
            }

            // Filter by IsActive
            if (isActive.HasValue)
            {
                coupons = coupons.Where(c => c.IsActive == isActive.Value);
                ViewBag.IsActive = isActive.Value;
            }

            // Sorting
            ViewBag.CodeSortParam = string.IsNullOrEmpty(sortOrder) ? "code_desc" : "";
            ViewBag.PercentSortParam = sortOrder == "percent" ? "percent_desc" : "percent";
            ViewBag.UsageSortParam = sortOrder == "usage" ? "usage_desc" : "usage";
            ViewBag.CurrentSort = sortOrder;

            switch (sortOrder)
            {
                case "code_desc":
                    coupons = coupons.OrderByDescending(c => c.Code);
                    break;
                case "percent":
                    coupons = coupons.OrderBy(c => c.PercentOff);
                    break;
                case "percent_desc":
                    coupons = coupons.OrderByDescending(c => c.PercentOff);
                    break;
                case "usage":
                    coupons = coupons.OrderBy(c => c.Orders.Count);
                    break;
                case "usage_desc":
                    coupons = coupons.OrderByDescending(c => c.Orders.Count);
                    break;
                default:
                    coupons = coupons.OrderBy(c => c.Code);
                    break;
            }

            // Statistics
            ViewBag.TotalCoupons = db.Coupons.Count();
            ViewBag.ActiveCoupons = db.Coupons.Count(c => c.IsActive);
            ViewBag.InactiveCoupons = db.Coupons.Count(c => !c.IsActive);
            ViewBag.TotalUsage = db.Coupons.Sum(c => (int?)c.Orders.Count) ?? 0;

            return View(coupons.ToList());
        }

        // GET: Admin/Coupons/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var coupon = db.Coupons
                .Include(c => c.Orders.Select(o => o.User))
                .FirstOrDefault(c => c.CouponID == id);

            if (coupon == null)
            {
                return HttpNotFound();
            }

            // Statistics
            ViewBag.TotalUsed = coupon.Orders.Count;
            ViewBag.TotalRevenue = coupon.Orders.Sum(o => (decimal?)o.GrandTotal) ?? 0;
            ViewBag.TotalDiscount = coupon.Orders.Sum(o => (decimal?)o.DiscountTotal) ?? 0;
            ViewBag.AverageDiscount = coupon.Orders.Any()
                ? coupon.Orders.Average(o => o.DiscountTotal)
                : 0;

            return View(coupon);
        }

        // GET: Admin/Coupons/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Admin/Coupons/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Code,PercentOff,IsActive")] Coupon coupon)
        {
            if (ModelState.IsValid)
            {
                // Check if Code already exists
                if (db.Coupons.Any(c => c.Code.ToUpper() == coupon.Code.ToUpper()))
                {
                    ModelState.AddModelError("Code", "Mã coupon này đã tồn tại.");
                    return View(coupon);
                }

                // Validate PercentOff range
                if (coupon.PercentOff <= 0 || coupon.PercentOff > 100)
                {
                    ModelState.AddModelError("PercentOff", "Phần trăm giảm giá phải từ 0.01% đến 100%.");
                    return View(coupon);
                }

                // Uppercase the code for consistency
                coupon.Code = coupon.Code.ToUpper().Trim();

                db.Coupons.Add(coupon);
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Tạo coupon '{coupon.Code}' thành công!";
                return RedirectToAction("Index");
            }

            return View(coupon);
        }

        // GET: Admin/Coupons/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var coupon = db.Coupons.Find(id);
            if (coupon == null)
            {
                return HttpNotFound();
            }

            // Check if coupon has been used
            ViewBag.HasBeenUsed = coupon.Orders.Any();
            ViewBag.UsageCount = coupon.Orders.Count;

            return View(coupon);
        }

        // POST: Admin/Coupons/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "CouponID,Code,PercentOff,IsActive")] Coupon coupon)
        {
            if (ModelState.IsValid)
            {
                // Check if Code already exists (excluding current coupon)
                if (db.Coupons.Any(c => c.Code.ToUpper() == coupon.Code.ToUpper()
                    && c.CouponID != coupon.CouponID))
                {
                    ModelState.AddModelError("Code", "Mã coupon này đã tồn tại.");
                    return View(coupon);
                }

                // Validate PercentOff range
                if (coupon.PercentOff <= 0 || coupon.PercentOff > 100)
                {
                    ModelState.AddModelError("PercentOff", "Phần trăm giảm giá phải từ 0.01% đến 100%.");
                    return View(coupon);
                }

                // Uppercase the code
                coupon.Code = coupon.Code.ToUpper().Trim();

                db.Entry(coupon).State = EntityState.Modified;
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Cập nhật coupon '{coupon.Code}' thành công!";
                return RedirectToAction("Index");
            }

            return View(coupon);
        }

        // GET: Admin/Coupons/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var coupon = db.Coupons.Find(id);
            if (coupon == null)
            {
                return HttpNotFound();
            }

            // Check if coupon has been used
            ViewBag.HasBeenUsed = coupon.Orders.Any();
            ViewBag.UsageCount = coupon.Orders.Count;

            return View(coupon);
        }

        // POST: Admin/Coupons/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var coupon = db.Coupons.Find(id);
            if (coupon == null)
            {
                return HttpNotFound();
            }

            // Check if coupon has been used in any orders
            if (coupon.Orders.Any())
            {
                TempData["ErrorMessage"] = $"Không thể xóa coupon '{coupon.Code}' vì đã được sử dụng trong {coupon.Orders.Count} đơn hàng. Bạn có thể vô hiệu hóa coupon thay vì xóa.";
                return RedirectToAction("Index");
            }

            try
            {
                db.Coupons.Remove(coupon);
                db.SaveChanges();
                TempData["SuccessMessage"] = $"Xóa coupon '{coupon.Code}' thành công!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xóa coupon: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        // POST: Admin/Coupons/ToggleActive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleActive(int id)
        {
            var coupon = db.Coupons.Find(id);
            if (coupon == null)
            {
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = "Không tìm thấy coupon." });
                }
                TempData["ErrorMessage"] = "Không tìm thấy coupon.";
                return RedirectToAction("Index");
            }

            coupon.IsActive = !coupon.IsActive;
            db.Entry(coupon).State = EntityState.Modified;
            db.SaveChanges();

            var statusText = coupon.IsActive ? "kích hoạt" : "vô hiệu hóa";
            TempData["SuccessMessage"] = $"Đã {statusText} coupon '{coupon.Code}' thành công!";

            if (Request.IsAjaxRequest())
            {
                return Json(new { success = true, isActive = coupon.IsActive });
            }

            return RedirectToAction("Index");
        }

        // POST: Admin/Coupons/BulkToggleActive
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BulkToggleActive(int[] ids, bool isActive)
        {
            if (ids == null || ids.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn ít nhất một coupon.";
                return RedirectToAction("Index");
            }

            var coupons = db.Coupons.Where(c => ids.Contains(c.CouponID)).ToList();
            foreach (var coupon in coupons)
            {
                coupon.IsActive = isActive;
                db.Entry(coupon).State = EntityState.Modified;
            }

            db.SaveChanges();

            var statusText = isActive ? "kích hoạt" : "vô hiệu hóa";
            TempData["SuccessMessage"] = $"Đã {statusText} {coupons.Count} coupon thành công!";

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