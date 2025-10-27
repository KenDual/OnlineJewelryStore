using OnlineJewelryStore.Models;
using System;
using System.Linq;
using System.Net.Mail;
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
            if (Session["UserID"] != null)
            {
                if (Session["UserRole"]?.ToString() == "Administrator")
                    return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                else
                    return RedirectToAction("Home", "Feature");
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
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Email và Password không được để trống.";
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            var user = db.Users.FirstOrDefault(u => u.Email == email);

            if (user != null && user.PasswordHash == password)
            {
                user.LastLogin = DateTime.Now;
                db.SaveChanges();

                //FormsAuthentication.SetAuthCookie(user.Email, false);

                Session["UserID"] = user.UserID;
                Session["UserName"] = user.FirstName + " " + user.LastName;
                Session["UserEmail"] = user.Email;
                Session["UserRole"] = user.Role;

                if (user.Role == "Administrator")
                {
                    return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                }
                else
                {
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);

                    return RedirectToAction("Home", "Feature");
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
            if (Session["UserID"] != null)
            {
                return RedirectToAction("Home", "Feature");
            }

            return View();
        }

        // POST: Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Register(User user, string password, string confirmPassword)
        {
            if (password != confirmPassword)
            {
                ViewBag.Error = "Mật khẩu xác nhận không khớp.";
                return View(user);
            }

            if (string.IsNullOrEmpty(password) || password.Length < 6)
            {
                ViewBag.Error = "Mật khẩu phải có ít nhất 6 ký tự.";
                return View(user);
            }

            if (ModelState.IsValid)
            {
                if (db.Users.Any(u => u.Email == user.Email))
                {
                    ViewBag.Error = "Email này đã được đăng ký.";
                    return View(user);
                }

                if (string.IsNullOrEmpty(user.Phone))
                {
                    user.Phone = "0000000000";
                }

                user.PasswordHash = password;
                user.Role = "Customer";
                user.RegistrationDate = DateTime.Now;
                user.LastLogin = null;
                user.SocialLoginProvider = null;

                db.Users.Add(user);
                db.SaveChanges();

                TempData["SuccessMessage"] = "Đăng ký thành công! Vui lòng đăng nhập.";
                return RedirectToAction("Login");
            }

            return View(user);
        }

        // GET: Account/Logout
        public ActionResult Logout()
        {
            //FormsAuthentication.SignOut();

            Session.Clear();
            Session.Abandon();

            return RedirectToAction("Login", "Account");
        }

        // GET: Account/Profile
        [HttpGet]
        public ActionResult Profile()
        {
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
                if (user.PasswordHash == oldPassword)
                {
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

        // GET: Account/ForgotPassword
        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            if (Session["UserID"] != null)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        // POST: Account/ForgotPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Vui lòng nhập địa chỉ email.";
                return View();
            }

            var user = db.Users.FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                TempData["SuccessMessage"] = "Nếu email tồn tại, mã đã được gửi.";
                return RedirectToAction("ForgotPassword");
            }

            var code = GenerateSixDigitCode();
            Session[$"ResetCode_{email}"] = code;
            Session[$"ResetCodeExpiry_{email}"] = DateTime.Now.AddHours(1);
            Session.Remove($"ResetVerified_{email}");

            try
            { 
                SendResetEmail(email, code);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Không thể gửi email: " + ex.Message;
                return View();
            }

            return RedirectToAction("CheckDigit", new { email = email });
        }


        // GET: Account/ResetPassword
        [AllowAnonymous]
        public ActionResult ResetPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("ForgotPassword");
            }

            ViewBag.Email = email;

            if (TempData["ResetToken"] != null)
            {
                ViewBag.ShowToken = true;
                ViewBag.Token = TempData["ResetToken"];
                TempData.Keep("SuccessMessage");
            }

            return View();
        }

        // POST: Account/ResetPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(string email, string token, string newPassword, string confirmPassword)
        {
            ViewBag.Email = email;

            if (string.IsNullOrEmpty(token))
            {
                ViewBag.Error = "Vui lòng nhập mã xác nhận.";
                return View();
            }

            if (string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
            {
                ViewBag.Error = "Vui lòng nhập mật khẩu mới.";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "Mật khẩu xác nhận không khớp.";
                return View();
            }

            if (newPassword.Length < 6)
            {
                ViewBag.Error = "Mật khẩu phải có ít nhất 6 ký tự.";
                return View();
            }

            var sessionToken = Session[$"ResetToken_{email}"] as string;
            var tokenExpiry = Session[$"ResetTokenExpiry_{email}"] as DateTime?;
            var userId = Session[$"ResetTokenUserID_{email}"] as int?;

            if (sessionToken == null || sessionToken != token.ToUpper())
            {
                ViewBag.Error = "Mã xác nhận không đúng.";
                return View();
            }

            if (tokenExpiry == null || tokenExpiry < DateTime.Now)
            {
                ViewBag.Error = "Mã xác nhận đã hết hạn. Vui lòng yêu cầu mã mới.";

                Session.Remove($"ResetToken_{email}");
                Session.Remove($"ResetTokenExpiry_{email}");
                Session.Remove($"ResetTokenUserID_{email}");
                return View();
            }

            var user = db.Users.Find(userId);
            if (user != null)
            {
                user.PasswordHash = newPassword;
                db.SaveChanges();

                Session.Remove($"ResetToken_{email}");
                Session.Remove($"ResetTokenExpiry_{email}");
                Session.Remove($"ResetTokenUserID_{email}");

                TempData["SuccessMessage"] = "Mật khẩu đã được đặt lại thành công. Vui lòng đăng nhập với mật khẩu mới.";
                return RedirectToAction("Login");
            }

            ViewBag.Error = "Có lỗi xảy ra. Vui lòng thử lại.";
            return View();
        }

        // --- Helpers (thêm vào trong AccountController) ---
        private string GenerateSixDigitCode()
        {
            return new Random().Next(0, 1000000).ToString("D6"); // 000000..999999
        }

        private void SendResetEmail(string toEmail, string code)
        {
            var from = System.Configuration.ConfigurationManager.AppSettings["SmtpFrom"] ?? "no-reply@localhost";
            var subject = "[Online Jewelry Store] Your password reset code";
            var body = $@"
                        <p>Hi,</p>
                        <p>Your verification code is: <b style='font-size:18px;letter-spacing:3px;'>{code}</b></p>
                        <p>The code expires in <b>60 minutes</b>.</p>";

            using (var msg = new System.Net.Mail.MailMessage(from, toEmail))
            {
                msg.Subject = subject;
                msg.Body = body;
                msg.IsBodyHtml = true;
                var client = new System.Net.Mail.SmtpClient();
                client.Send(msg);
            }
        }

        // GET: Account/CheckDigit
        [AllowAnonymous]
        public ActionResult CheckDigit(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return RedirectToAction("ForgotPassword");
            ViewBag.Email = email;
            return View();
        }

        // POST: Account/CheckDigit
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult CheckDigit(string email, string code)
        {
            ViewBag.Email = email;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            {
                ViewBag.Error = "Vui lòng nhập mã 6 số.";
                return View();
            }

            var saved = Session[$"ResetCode_{email}"] as string;
            var exp = Session[$"ResetCodeExpiry_{email}"] as DateTime?;

            if (saved == null || exp == null || exp < DateTime.Now)
            {
                ViewBag.Error = "Mã đã hết hạn hoặc không tồn tại. Hãy yêu cầu mã mới.";
                return View();
            }

            if (saved != code)
            {
                ViewBag.Error = "Mã không đúng. Vui lòng thử lại.";
                return View();
            }

            // Đúng mã → đánh dấu verified và chuyển sang NewPassword
            Session[$"ResetVerified_{email}"] = true;
            return RedirectToAction("NewPassword", new { email = email });
        }

        // GET: Account/NewPassword
        [AllowAnonymous]
        public ActionResult NewPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return RedirectToAction("ForgotPassword");
            var verified = Session[$"ResetVerified_{email}"] as bool?;
            if (verified != true) return RedirectToAction("CheckDigit", new { email = email });

            ViewBag.Email = email;
            return View();
        }

        // POST: Account/NewPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult NewPassword(string email, string newPassword, string confirmPassword)
        {
            ViewBag.Email = email;

            var verified = Session[$"ResetVerified_{email}"] as bool?;
            if (verified != true)
            {
                return RedirectToAction("CheckDigit", new { email = email });
            }

            if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                ViewBag.Error = "Vui lòng nhập mật khẩu mới.";
                return View();
            }
            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "Mật khẩu xác nhận không khớp.";
                return View();
            }
            if (newPassword.Length < 6)
            {
                ViewBag.Error = "Mật khẩu phải có ít nhất 6 ký tự.";
                return View();
            }

            var user = db.Users.FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                ViewBag.Error = "Email không hợp lệ.";
                return View();
            }

            user.PasswordHash = newPassword;
            db.SaveChanges();

            // Clear session keys
            Session.Remove($"ResetCode_{email}");
            Session.Remove($"ResetCodeExpiry_{email}");
            Session.Remove($"ResetVerified_{email}");

            TempData["SuccessMessage"] = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập.";
            return RedirectToAction("Login");
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