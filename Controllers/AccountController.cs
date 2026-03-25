using MTKPM_FE.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace MTKPM_FE.Controllers
{
    public class AccountController : Controller
    {
        private readonly myContext _context;
        private readonly LogApiClient _logApiClient;

        public AccountController(myContext context, LogApiClient logApiClient)
        {
            _context = context;
            _logApiClient = logApiClient;
        }

        // ================== TRANG ĐĂNG NHẬP CHUNG ==================
        [HttpGet]
        public IActionResult Login(string tab = "login", string returnUrl = null)
        {
            ViewBag.ActiveTab = tab;
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // ================== XỬ LÝ ĐĂNG NHẬP (GỘP ADMIN & CUSTOMER) ==================
        [HttpPost]
        public async Task<IActionResult> Login(string loginUsername, string loginPassword, string returnUrl = null)
        {
            ViewBag.ActiveTab = "login";

            // Xử lý Lockout nếu nhập sai 5 lần
            int attempts = HttpContext.Session.GetInt32("LoginAttempts") ?? 0;
            if (attempts >= 5)
            {
                ViewBag.LoginError = "Tài khoản bị khóa tạm thời do nhập sai quá nhiều lần.";
                return View("Login");
            }

            // Kiểm tra bỏ trống (TC_AUTH_022)
            if (string.IsNullOrEmpty(loginUsername))
            {
                ViewBag.LoginError = "yêu cầu nhập username";
                return View("Login");
            }
            if (string.IsNullOrEmpty(loginPassword))
            {
                ViewBag.LoginError = "Vui lòng nhập đầy đủ thông tin.";
                return View("Login");
            }

            // 1. ƯU TIÊN KIỂM TRA TÀI KHOẢN ADMIN TRƯỚC (TC_AUTH_023)
            var admin = _context.tbl_admin.FirstOrDefault(a => a.admin_name == loginUsername || a.admin_email == loginUsername);
            if (admin != null && admin.admin_password == loginPassword)
            {
                HttpContext.Session.SetString("Role", "Admin");
                HttpContext.Session.SetString("AdminEmail", admin.admin_email);
                HttpContext.Session.SetString("admin_session", admin.admin_id.ToString());

                try
                {
                    // Bọc try-catch để web không bị crash nếu API Log (localhost:5226) đang tắt
                    await _logApiClient.LogAdminLoginAsync(admin.admin_id.ToString(), "Login", "Admin logged in successfully");
                }
                catch { /* Bỏ qua lỗi kết nối API Log */ }

                return RedirectToAction("Index", "Admin");
            }

            // 2. NẾU KHÔNG PHẢI ADMIN, KIỂM TRA TÀI KHOẢN CUSTOMER (TC_AUTH_019)
            var user = _context.tbl_customer.FirstOrDefault(c => c.customer_name == loginUsername && c.customer_password == loginPassword);
            if (user != null)
            {
                if (user.IsLocked)
                {
                    ViewBag.Error = "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên.";
                    return View();
                }
                // Reset số lần đăng nhập sai
                HttpContext.Session.SetInt32("LoginAttempts", 0);

                HttpContext.Session.SetString("Role", "Customer");
                HttpContext.Session.SetString("CustomerId", user.customer_id.ToString());
                HttpContext.Session.SetString("CustomerName", user.customer_name);

                try
                {
                    await _logApiClient.LogUserLoginAsync(user.customer_id.ToString(), "Login", "Customer logged in");
                }
                catch { /* Bỏ qua lỗi kết nối API Log */ }

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index", "Home");
            }

            // 3. TRƯỜNG HỢP SAI TÀI KHOẢN HOẶC MẬT KHẨU (TC_AUTH_020, TC_AUTH_021)
            HttpContext.Session.SetInt32("LoginAttempts", attempts + 1);
            ViewBag.LoginError = "Tài khoản hoặc mật khẩu không đúng";
            return View("Login");
        }

        // ================== ĐĂNG KÝ CUSTOMER ==================
        [HttpPost]
        public async Task<IActionResult> Register(string username, string email, string password, string confirmPassword)
        {
            ViewBag.ActiveTab = "signup";

            // TC_AUTH_006: Để trống tất cả các trường
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
            {
                ViewBag.SignupError = "Vui lòng nhập đầy đủ thông tin";
                return View("Login");
            }

            // TC_AUTH_007 -> 012: Validation Username
            if (username.Length < 6 || username.Length > 20)
            {
                ViewBag.SignupError = "Tên đăng nhập từ 6 20 ký tự";
                return View("Login");
            }
            if (!char.IsLetter(username[0]))
            {
                ViewBag.SignupError = "Tên đăng nhập phải bắt đầu bằng chữ cái";
                return View("Login");
            }
            if (!Regex.IsMatch(username, @"^[a-zA-Z0-9]+$"))
            {
                ViewBag.SignupError = "Tên đăng nhập chỉ chứa chữ cái và số";
                return View("Login");
            }

            // Định dạng Email
            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                ViewBag.SignupError = "Email không đúng định dạng.";
                return View("Login");
            }

            // TC_AUTH_013 -> 018, TC_AUTH_004: Validation Password
            if (password.Length < 8) { ViewBag.SignupError = "Mật khẩu phải có ít nhất 8 ký tự"; return View("Login"); }
            if (!Regex.IsMatch(password, @"[A-Z]+")) { ViewBag.SignupError = "Mật khẩu phải có ít nhất 1 chữ hoa"; return View("Login"); }
            if (!Regex.IsMatch(password, @"[a-z]+")) { ViewBag.SignupError = "Mật khẩu phải có ít nhất 1 chữ thường"; return View("Login"); }
            if (!Regex.IsMatch(password, @"[0-9]+")) { ViewBag.SignupError = "Mật khẩu phải có ít nhất 1 số"; return View("Login"); }
            if (!Regex.IsMatch(password, @"[!@#$%^&*()_+=\[{\]};:<>|./?,-]+")) { ViewBag.SignupError = "Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt"; return View("Login"); }

            // TC_AUTH_005: Confirm Password
            if (password != confirmPassword)
            {
                ViewBag.SignupError = "Confirm password không khớp";
                return View("Login");
            }

            // TC_AUTH_002, 003: Trùng lặp
            if (_context.tbl_customer.Any(c => c.customer_name == username)) { ViewBag.SignupError = "Tên đăng nhập đã được sử dụng"; return View("Login"); }
            if (_context.tbl_customer.Any(c => c.customer_email == email)) { ViewBag.SignupError = "Email đã được đăng ký"; return View("Login"); }

            // Lưu tài khoản
            var customer = new Customer
            {
                customer_name = username,
                customer_email = email,
                customer_password = password,
                customer_image = "default.png",
                customer_phone = "",
                customer_address = "",
                customer_city = "",
                customer_country = "",
                customer_gender = "",
                CreatedAt = DateTime.Now
            };

            _context.tbl_customer.Add(customer);
            await _context.SaveChangesAsync();

            ViewBag.SignupSuccess = "Đã đăng kí thành công";
            return View("Login");
        }


        // ================== ĐĂNG XUẤT CHUNG ==================
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // ================== QUẢN LÝ HỒ SƠ  ==================
        [HttpGet]
        public IActionResult Profile()
        {
            // 1. Kiểm tra nếu là Khách hàng (Customer) đang đăng nhập
            var customerIdStr = HttpContext.Session.GetString("CustomerId");
            if (!string.IsNullOrEmpty(customerIdStr))
            {
                var user = _context.tbl_customer.Find(int.Parse(customerIdStr));
                if (user != null) return View(user); // Trả về file Profile.cshtml của Khách hàng
            }

            // 2. Kiểm tra nếu là Admin đang đăng nhập
            var adminIdStr = HttpContext.Session.GetString("admin_session");
            if (!string.IsNullOrEmpty(adminIdStr))
            {
                var admin = _context.tbl_admin.Find(int.Parse(adminIdStr));

                if (admin != null) return View("AdminProfile", admin);
            }

            // 3. Nếu chưa ai đăng nhập thì mới đá về trang Login
            return RedirectToAction("Login");
        }

        [HttpPost]
        public IActionResult Profile(Admin model)
        {
            var admin = _context.tbl_admin.Find(model.admin_id);
            if (admin != null)
            {
                admin.admin_name = model.admin_name;
                admin.admin_email = model.admin_email;
                if (!string.IsNullOrEmpty(model.admin_password))
                {
                    admin.admin_password = model.admin_password;
                }
                _context.SaveChanges();
                ViewBag.Success = "Cập nhật thông tin Admin thành công!";
            }
            return View(admin);
        }

        [HttpPost]
        public IActionResult ChangeProfileImage(int admin_id, IFormFile admin_image)
        {
            var admin = _context.tbl_admin.Find(admin_id);
            if (admin != null && admin_image != null && admin_image.Length > 0)
            {
                var fileName = Path.GetFileName(admin_image.FileName);
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "admin_image");

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                using (var stream = new FileStream(Path.Combine(uploadPath, fileName), FileMode.Create))
                {
                    admin_image.CopyTo(stream);
                }

                admin.admin_image = fileName;
                _context.SaveChanges();
                ViewBag.Success = "Đổi ảnh đại diện thành công!";
            }
            return RedirectToAction("Profile");
        }

        // ================== QUẢN LÝ HỒ SƠ CUSTOMER ==================
        [HttpGet]
        public IActionResult EditProfile()
        {
            var idStr = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(idStr)) return RedirectToAction("Login");

            var user = _context.tbl_customer.Find(int.Parse(idStr));
            if (user == null) return RedirectToAction("Login");

            return View(user);
        }

        [HttpPost]
        public IActionResult EditProfile(Customer model, IFormFile customer_image_file)
        {
            var custIdStr = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(custIdStr)) return RedirectToAction("Login");
            int custId = int.Parse(custIdStr);

            var user = _context.tbl_customer.Find(custId);
            if (user == null) return RedirectToAction("Login");

            if (_context.tbl_customer.Any(c => c.customer_email == model.customer_email && c.customer_id != custId))
            {
                ViewBag.Error = "Email đã được sử dụng";
                return View(user);
            }

            try
            {
                user.customer_name = model.customer_name;
                user.customer_email = model.customer_email;
                user.customer_phone = model.customer_phone ?? "";
                user.customer_gender = model.customer_gender ?? "";
                user.customer_country = model.customer_country ?? "";
                user.customer_city = model.customer_city ?? "";
                user.customer_address = model.customer_address ?? "";

                if (customer_image_file != null && customer_image_file.Length > 0)
                {
                    var fileName = Path.GetFileName(customer_image_file.FileName);
                    var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "users");

                    if (!Directory.Exists(uploadPath))
                        Directory.CreateDirectory(uploadPath);

                    var filePath = Path.Combine(uploadPath, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        customer_image_file.CopyTo(stream);
                    }

                    user.customer_image = "/images/users/" + fileName;
                }

                _context.Entry(user).State = EntityState.Modified;
                _context.SaveChanges();

                ViewBag.Success = "Cập nhật thành công";
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Có lỗi xảy ra: " + ex.Message;
            }

            return View(user);
        }

        // ================== ĐỔI MẬT KHẨU KHÁCH HÀNG ==================
        [HttpGet]
        public IActionResult ChangePassword()
        {
            var idStr = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(idStr)) return RedirectToAction("Login");
            return View();
        }

        [HttpPost]
        public IActionResult ChangePassword(string oldPassword, string newPassword, string confirmPassword)
        {
            var idStr = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(idStr)) return RedirectToAction("Login");

            var user = _context.tbl_customer.Find(int.Parse(idStr));
            if (user == null) return RedirectToAction("Login");

            if (user.customer_password != oldPassword) { ViewBag.Error = "Mật khẩu cũ không đúng"; return View(); }
            if (oldPassword == newPassword) { ViewBag.Error = "Mật khẩu mới phải khác mật khẩu cũ."; return View(); }

            // Check đầy đủ như Register
            if (newPassword.Length < 8) { ViewBag.Error = "Mật khẩu phải có ít nhất 8 ký tự"; return View(); }
            if (!Regex.IsMatch(newPassword, @"[A-Z]+") || !Regex.IsMatch(newPassword, @"[a-z]+") || !Regex.IsMatch(newPassword, @"[0-9]+") || !Regex.IsMatch(newPassword, @"[!@#$%^&*()_+=\[{\]};:<>|./?,-]+"))
            {
                ViewBag.Error = "Mật khẩu phải có ít nhất 8 ký tự, bao gồm chữ hoa, thường, số, ký tự đặc biệt";
                return View();
            }

            if (newPassword != confirmPassword) { ViewBag.Error = "Mật khẩu xác nhận không khớp"; return View(); }

            user.customer_password = newPassword;
            _context.SaveChanges();
            ViewBag.Success = "Đổi mật khẩu thành công";
            return View();
        }

        // ================== QUÊN MẬT KHẨU ==================
        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        public IActionResult ForgotPassword(string email)
        {
            if (!_context.tbl_customer.Any(c => c.customer_email == email))
            {
                ViewBag.Error = "Email không tồn tại trong hệ thống";
                return View();
            }
            ViewBag.Success = "Link đặt lại mật khẩu đã được gửi đến email của bạn";
            return View();
        }
    }
}