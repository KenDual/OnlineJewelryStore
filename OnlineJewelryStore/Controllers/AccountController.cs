using OnlineJewelryStore.Models;
using System;
using System.Linq;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using System.Web.Security;

namespace OnlineJewelryStore.Controllers
{
    public class AccountController : Controller
    {
        private OnlineJewelryStoreEntities db = new OnlineJewelryStoreEntities();

        // GET: Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            // Nếu đã đăng nhập rồi thì redirect về trang chủ
            if (Session["UserID"] != null)
            {
                if (Session["UserRole"]?.ToString() == "Administrator")
                    return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                else
                    return RedirectToAction("Index", "Home");
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string email, string password, string returnUrl)
        {
            // Validation
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Email và Password không được để trống.";
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            // Tìm user theo email
            var user = db.Users.FirstOrDefault(u => u.Email == email);

            // Kiểm tra user tồn tại và verify password
            if (user != null && user.PasswordHash == password)
            {
                // Cập nhật LastLogin
                user.LastLogin = DateTime.Now;
                db.SaveChanges();

                // Tạo authentication cookie
                FormsAuthentication.SetAuthCookie(user.Email, false);

                // Lưu thông tin user vào Session
                Session["UserID"] = user.UserID;
                Session["UserName"] = user.FirstName + " " + user.LastName;
                Session["UserEmail"] = user.Email;
                Session["UserRole"] = user.Role;

                // Redirect theo role
                if (user.Role == "Administrator")
                {
                    return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                }
                else
                {
                    // Nếu có returnUrl thì redirect về đó
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);

                    return RedirectToAction("Index", "Home");
                }
            }

            ViewBag.Error = "Email hoặc mật khẩu không đúng.";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // GET: Account/Register
        [AllowAnonymous]
        public ActionResult Register()
        {
            // Nếu đã đăng nhập rồi thì redirect về trang chủ
            if (Session["UserID"] != null)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        // POST: Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Register(User user, string password, string confirmPassword)
        {
            // Kiểm tra password match
            if (password != confirmPassword)
            {
                ViewBag.Error = "Mật khẩu xác nhận không khớp.";
                return View(user);
            }

            // Kiểm tra độ dài password
            if (string.IsNullOrEmpty(password) || password.Length < 6)
            {
                ViewBag.Error = "Mật khẩu phải có ít nhất 6 ký tự.";
                return View(user);
            }

            if (ModelState.IsValid)
            {
                // Kiểm tra email đã tồn tại chưa
                if (db.Users.Any(u => u.Email == user.Email))
                {
                    ViewBag.Error = "Email này đã được đăng ký.";
                    return View(user);
                }

                // Hash password
                user.PasswordHash = password;
                user.Role = "Customer"; // Mặc định là Customer
                user.RegistrationDate = DateTime.Now;
                user.LastLogin = null;
                user.SocialLoginProvider = null;

                db.Users.Add(user);
                db.SaveChanges();

                // Hiển thị thông báo thành công
                TempData["SuccessMessage"] = "Đăng ký thành công! Vui lòng đăng nhập.";
                return RedirectToAction("Login");
            }

            return View(user);
        }

        // GET: Account/Logout
        public ActionResult Logout()
        {
            // Xóa authentication cookie
            FormsAuthentication.SignOut();

            // Xóa session
            Session.Clear();
            Session.Abandon();

            return RedirectToAction("Index", "Home");
        }

        // GET: Account/Profile
        [HttpGet]
        public ActionResult Profile()
        {
            // Kiểm tra đã đăng nhập chưa
            if (Session["UserID"] == null)
            {
                return RedirectToAction("Login");
            }

            int userId = (int)Session["UserID"];
            var user = db.Users.Find(userId);

            if (user == null)
            {
                return HttpNotFound();
            }

            return View(user);
        }

        // POST: Account/Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Profile(User user)
        {
            if (Session["UserID"] == null)
            {
                return RedirectToAction("Login");
            }

            if (ModelState.IsValid)
            {
                var existingUser = db.Users.Find(user.UserID);
                if (existingUser != null)
                {
                    existingUser.FirstName = user.FirstName;
                    existingUser.LastName = user.LastName;
                    existingUser.Phone = user.Phone;

                    db.SaveChanges();

                    // Update session
                    Session["UserName"] = existingUser.FirstName + " " + existingUser.LastName;

                    TempData["SuccessMessage"] = "Cập nhật thông tin thành công!";
                }
            }

            return View(user);
        }

        // GET: Account/ChangePassword
        [HttpGet]
        public ActionResult ChangePassword()
        {
            if (Session["UserID"] == null)
            {
                return RedirectToAction("Login");
            }

            return View();
        }

        // POST: Account/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(string oldPassword, string newPassword, string confirmPassword)
        {
            if (Session["UserID"] == null)
            {
                return RedirectToAction("Login");
            }

            if (string.IsNullOrEmpty(oldPassword) || string.IsNullOrEmpty(newPassword))
            {
                ViewBag.Error = "Vui lòng điền đầy đủ thông tin.";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "Mật khẩu mới không khớp.";
                return View();
            }

            if (newPassword.Length < 6)
            {
                ViewBag.Error = "Mật khẩu phải có ít nhất 6 ký tự.";
                return View();
            }

            int userId = (int)Session["UserID"];
            var user = db.Users.Find(userId);

            if (user != null)
            {
                // Verify old password
                if (user.PasswordHash == oldPassword)
                {
                    // Update new password
                    user.PasswordHash = newPassword;
                    db.SaveChanges();

                    TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
                    return RedirectToAction("Profile");
                }
                else
                {
                    ViewBag.Error = "Mật khẩu cũ không đúng.";
                }
            }

            return View();
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