using OnlineJewelryStore.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace OnlineJewelryStore.Controllers
{
    [Authorize]
    public class ReviewsController : Controller
    {
        private OnlineJewelryStoreEntities db = new OnlineJewelryStoreEntities();

        // ===================================================================
        // 1. CREATE REVIEW (AJAX POST)
        // ===================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Create(int productId, int rating, string title, string body)
        {
            try
            {
                // Validate rating
                if (rating < 1 || rating > 5)
                {
                    return Json(new { success = false, message = "Đánh giá phải từ 1 đến 5 sao." });
                }

                // Lấy UserID từ session/claims (tùy cách bạn implement authentication)
                // Ví dụ nếu dùng ASP.NET Identity:
                var username = User.Identity.Name;
                var userId = db.Users.Where(u => u.Email == username).Select(u => u.UserID).FirstOrDefault();

                if (userId == 0)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                // Kiểm tra product có tồn tại không
                var product = db.Products.Find(productId);
                if (product == null)
                {
                    return Json(new { success = false, message = "Sản phẩm không tồn tại." });
                }

                // Tạo review mới
                var review = new Review
                {
                    UserID = userId,
                    ProductID = productId,
                    Rating = (short)rating,
                    Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
                    Body = string.IsNullOrWhiteSpace(body) ? null : body.Trim(),
                    CreatedAt = DateTime.Now
                };

                db.Reviews.Add(review);
                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = "Đánh giá của bạn đã được gửi thành công!",
                    reviewId = review.ReviewID
                });
            }
            catch (SqlException ex)
            {
                // Bắt lỗi từ trigger TR_Reviews_ValidatePurchase
                if (ex.Message.Contains("You can only review products that you have purchased"))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Bạn chỉ có thể đánh giá sản phẩm đã mua và nhận hàng."
                    });
                }

                // Bắt lỗi duplicate review (UQ_Reviews_User_Product)
                if (ex.Number == 2627 || ex.Message.Contains("UQ_Reviews_User_Product"))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Bạn đã đánh giá sản phẩm này rồi."
                    });
                }

                return Json(new
                {
                    success = false,
                    message = "Có lỗi xảy ra: " + ex.Message
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Có lỗi xảy ra: " + ex.Message
                });
            }
        }

        // ===================================================================
        // 2. EDIT REVIEW - GET (Hiển thị form edit)
        // ===================================================================
        [HttpGet]
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return HttpNotFound();
            }

            var username = User.Identity.Name;
            var userId = db.Users.Where(u => u.Email == username).Select(u => u.UserID).FirstOrDefault();

            if (userId == 0)
            {
                return Json(new { success = false, message = "User not found." });
            }

            // Lấy review kèm Product info
            var review = db.Reviews
                .Include(r => r.Product)
                .FirstOrDefault(r => r.ReviewID == id);

            if (review == null)
            {
                return HttpNotFound();
            }

            // Kiểm tra ownership: chỉ edit review của mình
            if (review.UserID != userId)
            {
                TempData["ErrorMessage"] = "Bạn chỉ có thể chỉnh sửa đánh giá của mình.";
                return RedirectToAction("Details", "Products", new { id = review.ProductID });
            }

            return View(review);
        }

        // ===================================================================
        // 3. EDIT REVIEW - POST (Xử lý edit)
        // ===================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, int rating, string title, string body)
        {
            try
            {
                // Validate rating
                if (rating < 1 || rating > 5)
                {
                    ModelState.AddModelError("", "Đánh giá phải từ 1 đến 5 sao.");
                    return View();
                }

                var username = User.Identity.Name;
                var userId = db.Users.Where(u => u.Email == username).Select(u => u.UserID).FirstOrDefault();

                if (userId == 0)
                {
                    return Json(new { success = false, message = "User not found." });
                }
                var review = db.Reviews.Find(id);

                if (review == null)
                {
                    return HttpNotFound();
                }

                // Kiểm tra ownership
                if (review.UserID != userId)
                {
                    TempData["ErrorMessage"] = "Bạn chỉ có thể chỉnh sửa đánh giá của mình.";
                    return RedirectToAction("Details", "Products", new { id = review.ProductID });
                }

                // Update review (không update CreatedAt)
                review.Rating = (short)rating;
                review.Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
                review.Body = string.IsNullOrWhiteSpace(body) ? null : body.Trim();

                db.Entry(review).State = EntityState.Modified;
                db.SaveChanges();

                TempData["SuccessMessage"] = "Đánh giá của bạn đã được cập nhật!";
                return RedirectToAction("Details", "Products", new { id = review.ProductID });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Có lỗi xảy ra: " + ex.Message);
                return View();
            }
        }

        // ===================================================================
        // 4. DELETE REVIEW (AJAX POST)
        // ===================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Delete(int id)
        {
            try
            {
                var username = User.Identity.Name;
                var userId = db.Users.Where(u => u.Email == username).Select(u => u.UserID).FirstOrDefault();

                if (userId == 0)
                {
                    return Json(new { success = false, message = "User not found." });
                }
                var review = db.Reviews.Find(id);

                if (review == null)
                {
                    return Json(new { success = false, message = "Đánh giá không tồn tại." });
                }

                // Kiểm tra ownership
                if (review.UserID != userId)
                {
                    return Json(new { success = false, message = "Bạn chỉ có thể xóa đánh giá của mình." });
                }

                // Hard delete
                db.Reviews.Remove(review);
                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = "Đánh giá đã được xóa thành công!"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Có lỗi xảy ra: " + ex.Message
                });
            }
        }

        // ===================================================================
        // 5. GET PRODUCT REVIEWS (AJAX GET) - Trả về Partial View
        // ===================================================================
        [HttpGet]
        [AllowAnonymous] // Cho phép guest xem reviews
        public ActionResult GetProductReviews(int productId)
        {
            try
            {
                // Lấy tất cả reviews của product, sắp xếp mới nhất trước
                var reviews = db.Reviews
                    .Include(r => r.User)
                    .Where(r => r.ProductID == productId)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();

                // Tính average rating và total count
                var totalReviews = reviews.Count;
                var averageRating = totalReviews > 0
                    ? Math.Round(reviews.Average(r => r.Rating), 1)
                    : 0;

                // Kiểm tra user hiện tại đã review chưa (nếu đã login)
                int? currentUserId = null;
                if (User.Identity.IsAuthenticated)
                {
                    var username = User.Identity.Name;
                    currentUserId = db.Users.Where(u => u.Email == username).Select(u => u.UserID).FirstOrDefault();
                }

                // Pass data qua ViewBag
                ViewBag.AverageRating = averageRating;
                ViewBag.TotalReviews = totalReviews;
                ViewBag.CurrentUserId = currentUserId;

                return PartialView("_ProductReviews", reviews);
            }
            catch (Exception ex)
            {
                return Content("Có lỗi xảy ra khi tải đánh giá: " + ex.Message);
            }
        }

        // ===================================================================
        // 6. CHECK CAN USER REVIEW (Helper method - dùng cho View)
        // ===================================================================
        [HttpGet]
        public JsonResult CanUserReview(int productId)
        {
            try
            {
                if (!User.Identity.IsAuthenticated)
                {
                    return Json(new
                    {
                        canReview = false,
                        message = "Vui lòng đăng nhập để đánh giá.",
                        reason = "not_authenticated"
                    }, JsonRequestBehavior.AllowGet);
                }

                var username = User.Identity.Name;
                var userId = db.Users.Where(u => u.Email == username).Select(u => u.UserID).FirstOrDefault();

                if (userId == 0)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                // Kiểm tra user đã review sản phẩm này chưa
                var hasReviewed = db.Reviews
                    .Any(r => r.UserID == userId && r.ProductID == productId);

                if (hasReviewed)
                {
                    return Json(new
                    {
                        canReview = false,
                        message = "Bạn đã đánh giá sản phẩm này rồi.",
                        reason = "already_reviewed"
                    }, JsonRequestBehavior.AllowGet);
                }

                // Kiểm tra user có order delivered với product này không
                var hasPurchased = db.Orders
                    .Where(o => o.UserID == userId && o.Status == "Delivered")
                    .Join(db.OrderItems, o => o.OrderID, oi => oi.OrderID, (o, oi) => oi)
                    .Join(db.ProductVariants, oi => oi.VariantID, pv => pv.VariantID, (oi, pv) => pv)
                    .Any(pv => pv.ProductID == productId);

                if (!hasPurchased)
                {
                    return Json(new
                    {
                        canReview = false,
                        message = "Bạn cần mua và nhận sản phẩm này để có thể đánh giá.",
                        reason = "not_purchased"
                    }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    canReview = true,
                    message = "Bạn có thể đánh giá sản phẩm này."
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    canReview = false,
                    message = "Có lỗi xảy ra: " + ex.Message,
                    reason = "error"
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // ===================================================================
        // 7. GET USER'S REVIEW FOR PRODUCT (Helper - dùng để hiển thị existing review)
        // ===================================================================
        [HttpGet]
        public JsonResult GetUserReview(int productId)
        {
            try
            {
                if (!User.Identity.IsAuthenticated)
                {
                    return Json(new { hasReview = false }, JsonRequestBehavior.AllowGet);
                }

                var username = User.Identity.Name;
                var userId = db.Users.Where(u => u.Email == username).Select(u => u.UserID).FirstOrDefault();

                if (userId == 0)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                var review = db.Reviews
                    .Where(r => r.UserID == userId && r.ProductID == productId)
                    .Select(r => new
                    {
                        reviewId = r.ReviewID,
                        rating = r.Rating,
                        title = r.Title,
                        body = r.Body,
                        createdAt = r.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss")
                    })
                    .FirstOrDefault();

                if (review == null)
                {
                    return Json(new { hasReview = false }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    hasReview = true,
                    review = review
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    hasReview = false,
                    error = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // ===================================================================
        // Dispose
        // ===================================================================
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