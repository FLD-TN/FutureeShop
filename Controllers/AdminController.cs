using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MTKPM_FE.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;


namespace MTKPM_FE.Controllers
{
    public class AdminController : Controller
    {
        private readonly myContext _context;
        private readonly IWebHostEnvironment _env;
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var adminSession = context.HttpContext.Session.GetString("admin_session");

            if (string.IsNullOrEmpty(adminSession))
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            base.OnActionExecuting(context);
        }
        // =======================================================================
        public AdminController(myContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: /Admin
        // AdminController.cs
        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("admin_session") is null)
                return RedirectToAction("Login", "Account");

            // Gán tiêu đề cho trang
            ViewData["Title"] = "Dashboard";

            var now = DateTime.Now;
            var startOfYear = new DateTime(now.Year, 1, 1);
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            // Thống kê cơ bản
            ViewBag.ProductCount = _context.tbl_product.Count();
            ViewBag.CategoryCount = _context.tbl_category.Count();
            ViewBag.CustomerCount = _context.tbl_customer.Count();
            ViewBag.OrderCount = _context.tbl_order.Count();

            // Doanh thu
            ViewBag.YearlyRevenue = _context.tbl_order
                .Where(o => o.CreatedAt >= startOfYear && o.PaymentStatus == "Paid")
                .Sum(o => (decimal?)o.TotalAmount) ?? 0;

            ViewBag.MonthlyRevenue = _context.tbl_order
                .Where(o => o.CreatedAt >= startOfMonth && o.PaymentStatus == "Paid")
                .Sum(o => (decimal?)o.TotalAmount) ?? 0;

            ViewBag.TotalProductsSold = _context.tbl_product.Sum(p => p.product_stock);

            // Dữ liệu biểu đồ doanh thu theo tháng
            var revenueByMonth = _context.tbl_order
                .Where(o => o.CreatedAt.Year == now.Year) // Lọc theo năm hiện tại
                .GroupBy(o => o.CreatedAt.Month)       // Nhóm theo tháng ngay trên DB
                .Select(g => new { Month = g.Key, Total = g.Sum(o => o.TotalAmount) })
                .OrderBy(r => r.Month)
                .ToDictionary(r => r.Month, r => r.Total);

            var monthlyLabels = new List<string>();
            var monthlyData = new List<decimal>();

            for (int i = 1; i <= 12; i++)
            {
                monthlyLabels.Add($"Tháng {i}");
                monthlyData.Add(revenueByMonth.ContainsKey(i) ? revenueByMonth[i] : 0);
            }

            ViewBag.MonthlyLabels = monthlyLabels;
            ViewBag.MonthlyData = monthlyData;

            return View();
        }

        public IActionResult ViewFeedback(string searchTerm, int? rating,
            DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.tbl_feedback
                .Include(f => f.Customer)
                .AsQueryable();

            // Tìm kiếm
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(f =>
                    f.Content.Contains(searchTerm) ||
                    (f.Customer != null && f.Customer.customer_name.Contains(searchTerm)) || // Tìm theo tên khách hàng
                    (f.Customer == null && f.user_name.Contains(searchTerm)) // Tìm theo tên người gửi liên hệ
                );
            }

            // Lọc theo đánh giá
            if (rating.HasValue)
            {
                query = query.Where(f => f.Rating == rating);
            }

            // Lọc theo ngày
            if (fromDate.HasValue)
            {
                query = query.Where(f => f.CreatedAt >= fromDate);
            }
            if (toDate.HasValue)
            {
                query = query.Where(f => f.CreatedAt <= toDate);
            }

            var feedbacks = query.OrderByDescending(f => f.CreatedAt).ToList();
            return View(feedbacks);
        }

        // GET: /Admin/ViewContactMessages
        public async Task<IActionResult> ViewContactMessages()
        {
            if (HttpContext.Session.GetString("admin_session") is null)
                return RedirectToAction("Login", "Account");

            ViewData["Title"] = "Tin nhắn liên hệ";
            var messages = await _context.tbl_contact_message.OrderByDescending(m => m.CreatedAt).ToListAsync();
            return View(messages);
        }

        // GET: /Admin/Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Remove("admin_session");
            return RedirectToAction("Login", "Account");
        }

        // GET: /Admin/Profile
        [HttpGet]
        public IActionResult Profile()
        {
            if (!int.TryParse(HttpContext.Session.GetString("admin_session"), out var id))
                return RedirectToAction("Login", "Account");

            var admin = _context.tbl_admin.Find(id);
            if (admin == null) return NotFound();

            return View(admin);
        }

        // POST: /Admin/Profile
        [HttpPost]
        public IActionResult Profile(Admin admin)
        {
            var existing = _context.tbl_admin.Find(admin.admin_id);
            if (existing == null) return NotFound();

            existing.admin_name = admin.admin_name;
            existing.admin_email = admin.admin_email;
            // ... cập nhật các trường khác nếu cần ...
            _context.tbl_admin.Update(existing);
            _context.SaveChanges();

            return View("Profile");
        }

        // POST: /Admin/ChangeProfileImage
        [HttpPost]
        public IActionResult ChangeProfileImage(IFormFile admin_image)
        {
            if (!int.TryParse(HttpContext.Session.GetString("admin_session"), out var id))
                return RedirectToAction("Profile");

            var existing = _context.tbl_admin.Find(id);
            if (existing == null) return NotFound();

            if (admin_image?.Length > 0)
            {
                var fn = Path.GetFileName(admin_image.FileName);
                var path = Path.Combine(_env.WebRootPath, "admin_image", fn);
                using var fs = new FileStream(path, FileMode.Create);
                admin_image.CopyTo(fs);
                existing.admin_image = fn;
                _context.tbl_admin.Update(existing);
                _context.SaveChanges();
            }

            return RedirectToAction("Profile");
        }

        // --- CUSTOMER MANAGEMENT ---

        // GET: /Admin/FetchCustomer
        public IActionResult FetchCustomer(string searchTerm, string sortBy,
    DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.tbl_customer
                .Include(c => c.Orders)
                .AsQueryable();

            // Tìm kiếm
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(c =>
                    c.customer_name.Contains(searchTerm) ||
                    c.customer_email.Contains(searchTerm) ||
                    c.customer_phone.Contains(searchTerm)
                );
            }

            // Lọc theo ngày đăng ký
            if (fromDate.HasValue)
            {
                query = query.Where(c => c.CreatedAt >= fromDate);
            }
            if (toDate.HasValue)
            {
                query = query.Where(c => c.CreatedAt <= toDate);
            }

            // Sắp xếp
            switch (sortBy)
            {
                case "name":
                    query = query.OrderBy(c => c.customer_name);
                    break;
                case "orders":
                    query = query.OrderByDescending(c => c.Orders.Count);
                    break;
                case "date":
                    query = query.OrderByDescending(c => c.CreatedAt);
                    break;
                default:
                    query = query.OrderBy(c => c.customer_id);
                    break;
            }

            var customers = query.ToList();
            return View(customers);
        }

        // GET: /Admin/CustomerDetails/{id}
        public IActionResult CustomerDetails(int id)
        {
            var customer = _context.tbl_customer.Find(id);
            if (customer == null) return NotFound();
            return View(customer);
        }
        // ======================= QUẢN LÝ NGƯỜI DÙNG (CUSTOMER) =======================

        [HttpGet]
        public IActionResult addCustomer()
        {
            return View(new Customer());
        }

        [HttpPost]
        public async Task<IActionResult> addCustomer(Customer model)
        {
            // BỎ QUA KIỂM TRA CHO CÁC TRƯỜNG TÙY CHỌN HOẶC TỰ ĐỘNG GÁN
            ModelState.Remove("customer_gender");
            ModelState.Remove("customer_country");
            ModelState.Remove("customer_city");
            ModelState.Remove("customer_address");
            ModelState.Remove("customer_image");
            ModelState.Remove("Orders");
            ModelState.Remove("IsLocked");

            // Kiểm tra trùng Email (TC_USER_004)
            if (_context.tbl_customer.Any(c => c.customer_email == model.customer_email))
            {
                ModelState.AddModelError("customer_email", "Email đã tồn tại");
            }

            // Nếu Form có lỗi -> Tải lại trang
            if (!ModelState.IsValid) return View(model);

            model.customer_image = "default.png";
            model.customer_gender = model.customer_gender ?? "";
            model.customer_country = model.customer_country ?? "";
            model.customer_city = model.customer_city ?? "";
            model.customer_address = model.customer_address ?? "";
            model.IsLocked = false;
            model.CreatedAt = DateTime.Now;

            _context.tbl_customer.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Thêm người dùng thành công";
            return RedirectToAction("fetchCustomer");
        }

        [HttpGet]
        public IActionResult updateCustomer(int id)
        {
            var cust = _context.tbl_customer.Find(id);
            if (cust == null) return NotFound();
            return View(cust);
        }

        [HttpPost]
        public async Task<IActionResult> updateCustomer(Customer model)
        {
            // BỎ QUA KIỂM TRA CHO CÁC TRƯỜNG TÙY CHỌN
            ModelState.Remove("customer_gender");
            ModelState.Remove("customer_country");
            ModelState.Remove("customer_city");
            ModelState.Remove("customer_address");
            ModelState.Remove("customer_image");
            ModelState.Remove("Orders");
            ModelState.Remove("IsLocked");

            // Kiểm tra trùng Email với người khác (TC_USER_007)
            if (_context.tbl_customer.Any(c => c.customer_email == model.customer_email && c.customer_id != model.customer_id))
            {
                ModelState.AddModelError("customer_email", "Email đã tồn tại");
            }

            if (!ModelState.IsValid) return View(model);

            var existingCust = _context.tbl_customer.Find(model.customer_id);
            if (existingCust != null)
            {
                existingCust.customer_name = model.customer_name;
                existingCust.customer_phone = model.customer_phone;
                existingCust.customer_email = model.customer_email;
                existingCust.customer_password = model.customer_password;

                // Cập nhật các trường tùy chọn (Nếu rỗng thì gán chuỗi rỗng)
                existingCust.customer_gender = model.customer_gender ?? "";
                existingCust.customer_country = model.customer_country ?? "";
                existingCust.customer_city = model.customer_city ?? "";
                existingCust.customer_address = model.customer_address ?? "";

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật thành công";
                return RedirectToAction("fetchCustomer");
            }
            return NotFound();
        }

        [HttpGet]
        public async Task<IActionResult> deletePermission(int id)
        {
            var cust = await _context.tbl_customer.FindAsync(id);
            if (cust == null) return NotFound();

            // Ràng buộc xóa: KHÔNG cho xóa người dùng đã có đơn hàng (TC_USER_009)
            bool hasOrders = _context.tbl_order.Any(o => o.CustomerId == id);
            if (hasOrders)
            {
                TempData["ErrorMessage"] = "Không thể xóa người dùng đã có đơn hàng";
                return RedirectToAction("fetchCustomer");
            }

            _context.tbl_customer.Remove(cust);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Xóa thành công";
            return RedirectToAction("fetchCustomer");
        }

        // POST: /Admin/DeleteCustomer
        [HttpPost, ActionName("DeleteCustomer")]
        public IActionResult DeleteCustomerConfirmed(int id)
        {
            var existing = _context.tbl_customer.Find(id);
            if (existing == null) return NotFound();

            _context.tbl_customer.Remove(existing);
            _context.SaveChanges();
            return RedirectToAction("FetchCustomer");
        }

        // ======================= KHÓA / MỞ KHÓA TÀI KHOẢN =======================
        [HttpGet]
        [Route("Admin/ToggleLock/{id}")]
        public async Task<IActionResult> ToggleLock(int id)
        {
            var cust = await _context.tbl_customer.FindAsync(id);
            if (cust == null) return NotFound();

            // Đảo ngược trạng thái (Đang khóa thì mở, đang mở thì khóa)
            cust.IsLocked = !cust.IsLocked;
            await _context.SaveChangesAsync();

            string status = cust.IsLocked ? "khóa" : "mở khóa";
            TempData["SuccessMessage"] = $"Đã {status} tài khoản của [{cust.customer_name}] thành công!";

            return RedirectToAction("fetchCustomer");
        }

        // --- CATEGORY MANAGEMENT ---

        // GET: /Admin/FetchCategory
        public IActionResult FetchCategory(string searchTerm, string sortBy)
        {
            var query = _context.tbl_category
                .Include(c => c.Product)
                .AsQueryable();

            // Tìm kiếm
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(c =>
                    c.category_name.Contains(searchTerm)
                );
            }

            // Sắp xếp
            switch (sortBy)
            {
                case "name":
                    query = query.OrderBy(c => c.category_name);
                    break;
                case "products":
                    query = query.OrderByDescending(c => c.Product.Count);
                    break;
                default:
                    query = query.OrderBy(c => c.category_id);
                    break;
            }

            var categories = query.ToList();
            return View(categories);
        }


        [HttpGet]
        public IActionResult addCategory()
        {
            return View();

        }

        [HttpPost]
        public async Task<IActionResult> addCategory(Category model)
        {
            // 1. Lỗi tên trống
            if (string.IsNullOrWhiteSpace(model.category_name))
            {
                ModelState.AddModelError("category_name", "Tên danh mục không được để trống");
                return View(model);
            }

            // 2. Lỗi trùng tên (TC_CAT_003)
            if (_context.tbl_category.Any(c => c.category_name.ToLower() == model.category_name.ToLower()))
            {
                ModelState.AddModelError("category_name", "Tên danh mục đã tồn tại");
                return View(model);
            }

            _context.tbl_category.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Thêm danh mục thành công";
            return RedirectToAction("fetchCategory");
        }

        [HttpGet]
        public IActionResult updateCategory(int id)
        {
            var category = _context.tbl_category.Find(id);
            if (category == null) return NotFound();
            return View(category);
        }

        [HttpPost]
        public async Task<IActionResult> updateCategory(Category model)
        {
            if (string.IsNullOrWhiteSpace(model.category_name))
            {
                ModelState.AddModelError("category_name", "Tên danh mục không được để trống");
                return View(model);
            }

            // Kiểm tra trùng tên với danh mục KHÁC (TC_CAT_005)
            if (_context.tbl_category.Any(c => c.category_name.ToLower() == model.category_name.ToLower() && c.category_id != model.category_id))
            {
                ModelState.AddModelError("category_name", "Tên danh mục đã tồn tại");
                return View(model);
            }

            var existingCat = _context.tbl_category.Find(model.category_id);
            if (existingCat != null)
            {
                existingCat.category_name = model.category_name;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Cập nhật thành công";
                return RedirectToAction("fetchCategory");
            }
            return NotFound();
        }

        [HttpGet]
        public async Task<IActionResult> deletePermissionCategory(int id)
        {
            var category = await _context.tbl_category.FindAsync(id);
            if (category == null) return NotFound();

            // RÀNG BUỘC: Không cho phép xóa danh mục đã có sản phẩm (TC_CAT_007)
            bool hasProducts = _context.tbl_product.Any(p => p.cat_id == id);
            if (hasProducts)
            {
                TempData["ErrorMessage"] = "Không thể xóa danh mục đã có sản phẩm";
                return RedirectToAction("fetchCategory");
            }

            _context.tbl_category.Remove(category);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Xóa thành công";
            return RedirectToAction("fetchCategory");
        }

        // POST: /Admin/DeleteCategory
        [HttpPost, ActionName("DeleteCategory")]
        public IActionResult DeleteCategoryConfirmed(int id)
        {
            var existing = _context.tbl_category.Find(id);
            if (existing == null) return NotFound();

            _context.tbl_category.Remove(existing);
            _context.SaveChanges();
            return RedirectToAction("FetchCategory");
        }

        // --- PRODUCT MANAGEMENT ---

        // GET: /Admin/FetchProduct
        public IActionResult FetchProduct(string searchTerm, int? categoryId,
            decimal? minPrice, decimal? maxPrice, string sortBy)
        {
            var query = _context.tbl_product
                .Include(p => p.Category)
                .AsQueryable();

            // Tìm kiếm
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(p =>
                    p.product_name.Contains(searchTerm) ||
                    p.product_description.Contains(searchTerm)
                );
            }

            // Lọc theo danh mục
            if (categoryId.HasValue)
            {
                query = query.Where(p => p.cat_id == categoryId);
            }

            // Lọc theo giá
            if (minPrice.HasValue)
            {
                query = query.Where(p => p.product_price >= minPrice);
            }
            if (maxPrice.HasValue)
            {
                query = query.Where(p => p.product_price <= maxPrice);
            }

            // Sắp xếp
            switch (sortBy)
            {
                case "price_asc":
                    query = query.OrderBy(p => p.product_price);
                    break;
                case "price_desc":
                    query = query.OrderByDescending(p => p.product_price);
                    break;
                case "name":
                    query = query.OrderBy(p => p.product_name);
                    break;
                case "sold":
                    query = query.OrderByDescending(p => p.product_stock);
                    break;
                default:
                    query = query.OrderByDescending(p => p.CreatedAt);
                    break;
            }

            var products = query.ToList();
            ViewBag.Categories = _context.tbl_category.ToList();
            return View(products);
        }

        // ======================= QUẢN LÝ SẢN PHẨM =======================

        [HttpGet]
        public IActionResult addProduct()
        {
            ViewData["category"] = _context.tbl_category.ToList();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> addProduct(Product model, IFormFile image_file)
        {
            ViewData["category"] = _context.tbl_category.ToList();

            // 1. Kiểm tra bắt buộc: Tên, Giá, Danh mục, Tồn kho (Nếu thiếu -> Reload trang như F5)
            if (string.IsNullOrWhiteSpace(model.product_name) ||
                model.product_price <= 0 ||
                model.cat_id == null ||
                model.product_stock < 0)
            {
                return View(model);
            }

            // 2. Kiểm tra trùng tên sản phẩm
            if (_context.tbl_product.Any(p => p.product_name == model.product_name))
            {
                ModelState.AddModelError("product_name", "Tên sản phẩm đã tồn tại");
                return View(model);
            }

            // 3. XỬ LÝ ẢNH
            if (image_file != null && image_file.Length > 0)
            {
                // Có chọn ảnh -> Lưu file vào product_images
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(image_file.FileName);
                string uploadPath = Path.Combine(_env.WebRootPath, "product_images", fileName);
                using (var stream = new FileStream(uploadPath, FileMode.Create))
                {
                    await image_file.CopyToAsync(stream);
                }
                model.product_image = fileName;
            }
            else
            {
                model.product_image = "../images/users/banghe1.jpg";
            }

            // 4. Xử lý các trường bỏ trống (Tùy chọn)
            if (string.IsNullOrWhiteSpace(model.product_description))
            {
                model.product_description = ""; // Gán chuỗi rỗng nếu bỏ trống mô tả
            }

            model.CreatedAt = DateTime.Now;

            // Xóa các lỗi mặc định của Entity Framework để cho phép lưu DB
            ModelState.Clear();

            // Lưu vào Database
            _context.tbl_product.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Thêm sản phẩm thành công";
            return RedirectToAction("fetchProduct");
        }

        [HttpGet]
        public IActionResult UpdateProduct(int id)
        {
            var product = _context.tbl_product.Find(id);
            if (product == null) return NotFound();
            ViewData["category"] = _context.tbl_category.ToList();
            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProduct(Product model)
        {
            ViewData["category"] = _context.tbl_category.ToList();

            // 1. Kiểm tra bắt buộc: Giá <= 0, Tồn kho < 0, Tên rỗng
            if (string.IsNullOrWhiteSpace(model.product_name) ||
                model.product_price <= 0 ||
                model.cat_id == null ||
                model.product_stock < 0)
            {
                return View(model);
            }

            // 2. Validate: Trùng tên (bỏ qua chính nó)
            if (_context.tbl_product.Any(p => p.product_name == model.product_name && p.product_id != model.product_id))
            {
                ModelState.AddModelError("product_name", "Tên sản phẩm đã tồn tại");
                return View(model);
            }

            ModelState.Clear(); // Xóa lỗi EF tự sinh

            var existingProduct = _context.tbl_product.Find(model.product_id);
            if (existingProduct != null)
            {
                existingProduct.product_name = model.product_name;
                existingProduct.product_price = model.product_price;
                existingProduct.product_discount_price = model.product_discount_price;
                existingProduct.product_stock = model.product_stock;
                existingProduct.product_description = string.IsNullOrWhiteSpace(model.product_description) ? "" : model.product_description;
                existingProduct.cat_id = model.cat_id;

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật thành công";
                return RedirectToAction("fetchProduct");
            }
            return NotFound();
        }

        [HttpGet]
        public async Task<IActionResult> DeletePermissionProduct(int id)
        {
            var product = await _context.tbl_product.FindAsync(id);
            if (product == null) return NotFound();

            // Ràng buộc xóa: Sản phẩm đã có trong đơn hàng thì KHÔNG được xóa (TC_PROD_015)
            // (Giả sử bạn có bảng tbl_orderdetail liên kết qua khóa ProductID)
            bool hasOrders = _context.tbl_orderdetail.Any(od => od.ProductID == id);
            if (hasOrders)
            {
                TempData["ErrorMessage"] = "Không thể xóa sản phẩm đã có trong đơn hàng";
                return RedirectToAction("fetchProduct");
            }

            _context.tbl_product.Remove(product);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Xóa thành công";
            return RedirectToAction("fetchProduct");
        }


        // GET: /Admin/ProductDetails/{id}
        public IActionResult ProductDetails(int id)
        {
            var prod = _context.tbl_product
                            .Include(p => p.Category)
                            .Include(p => p.ProductImages)
                            .FirstOrDefault(p => p.product_id == id);

            if (prod == null) return NotFound();
            return View(prod);
        }

        // POST: /Admin/DeleteProductImage
        [HttpPost]
        public IActionResult DeleteProductImage(int imageId, int productId)
        {
            var img = _context.ProductImage.Find(imageId);
            if (img != null)
            {
                var filePath = Path.Combine(_env.WebRootPath, "product_images", img.ImagePath);
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

                _context.ProductImage.Remove(img);
                _context.SaveChanges();
            }
            return RedirectToAction("UpdateProduct", new { id = productId });
        }

        // POST: /Admin/ChangeProductImage
        [HttpPost]
        public IActionResult ChangeProductImage(IFormFile product_image, Product product)
        {
            var existing = _context.tbl_product.Find(product.product_id);
            if (existing == null) return NotFound();

            if (product_image?.Length > 0)
            {
                var fn = Path.GetFileName(product_image.FileName);
                var path = Path.Combine(_env.WebRootPath, "product_images", fn);
                using var fs = new FileStream(path, FileMode.Create);
                product_image.CopyTo(fs);

                existing.product_image = fn;
                _context.tbl_product.Update(existing);
                _context.SaveChanges();
            }
            return RedirectToAction("FetchProduct");
        }


        // POST: /Admin/DeleteProduct/5
        [HttpPost, ActionName("DeleteProduct")]
        public IActionResult DeleteProductConfirmed(int id)
        {
            var prod = _context.tbl_product.Find(id);
            if (prod != null)
            {
                _context.tbl_product.Remove(prod);
                _context.SaveChanges();
            }
            return RedirectToAction("FetchProduct");
        }

        // --- BLOG MANAGEMENT ---

        // GET: /Admin/ManageBlogs
        // Action để hiển thị danh sách các bài blog đã tạo
        public async Task<IActionResult> ManageBlogs()
        {
            if (HttpContext.Session.GetString("admin_session") is null)
                return RedirectToAction("Login", "Account");

            ViewData["Title"] = "Quản lý Blog";
            var blogs = await _context.tbl_blog.OrderByDescending(b => b.CreatedAt).ToListAsync();
            return View(blogs); // Cần tạo View Views/Admin/ManageBlogs.cshtml
        }

        // GET: /Admin/CreateBlog
        // Action để hiển thị form tạo bài viết mới
        public IActionResult CreateBlog()
        {
            if (HttpContext.Session.GetString("admin_session") is null)
                return RedirectToAction("Login", "Account");

            ViewData["Title"] = "Tạo bài viết mới";
            return View(); // Trả về View Views/Admin/CreateBlog.cshtml
        }

        // POST: /Admin/CreateBlog
        // Action để xử lý dữ liệu từ form và lưu bài viết
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBlog(Blog blog, IFormFile photo)
        {
            if (HttpContext.Session.GetString("admin_session") is null)
                return RedirectToAction("Login", "Account");

            ViewData["Title"] = "Tạo bài viết mới";
            if (ModelState.IsValid)
            {
                // Xử lý upload hình ảnh đại diện
                if (photo != null && photo.Length > 0)
                {
                    // Tạo tên file độc nhất để tránh trùng lặp
                    var fileName = Path.GetFileNameWithoutExtension(photo.FileName);
                    var fileExtension = Path.GetExtension(photo.FileName);
                    var uniqueFileName = $"{fileName}_{DateTime.Now:yyyyMMddHHmmssfff}{fileExtension}";

                    // Đường dẫn để lưu file (wwwroot/blog_images)
                    var path = Path.Combine(_env.WebRootPath, "blog_images", uniqueFileName);

                    // Đảm bảo thư mục tồn tại
                    Directory.CreateDirectory(Path.GetDirectoryName(path));

                    // Copy file vào thư mục
                    using (var stream = new FileStream(path, FileMode.Create))
                    {
                        await photo.CopyToAsync(stream);
                    }

                    // Lưu tên file vào model
                    blog.blog_image = uniqueFileName;
                }

                // Tự động tạo slug từ tiêu đề
                blog.slug = GenerateSlug(blog.blog_title);

                blog.CreatedAt = DateTime.Now;
                _context.tbl_blog.Add(blog);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Đã tạo bài viết thành công!";
                return RedirectToAction("ManageBlogs");
            }

            return View(blog);
        }

        // GET: /Admin/EditBlog/5
        // Action để hiển thị form chỉnh sửa bài viết
        public async Task<IActionResult> EditBlog(int? id)
        {
            if (HttpContext.Session.GetString("admin_session") is null)
                return RedirectToAction("Login", "Account");

            if (id == null)
            {
                return NotFound();
            }

            var blog = await _context.tbl_blog.FindAsync(id);
            if (blog == null)
            {
                return NotFound();
            }
            ViewData["Title"] = "Chỉnh sửa bài viết";
            return View(blog); // Cần tạo View Views/Admin/EditBlog.cshtml
        }

        // POST: /Admin/EditBlog/5
        // Action để xử lý việc cập nhật bài viết
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBlog(int id, Blog blog, IFormFile photo)
        {
            if (id != blog.blog_id)
            {
                return NotFound();
            }

            if (HttpContext.Session.GetString("admin_session") is null)
                return RedirectToAction("Login", "Account");

            ViewData["Title"] = "Chỉnh sửa bài viết";
            if (ModelState.IsValid)
            {
                try
                {
                    var existingBlog = await _context.tbl_blog.FindAsync(id);
                    if (existingBlog == null) return NotFound();

                    // Cập nhật thông tin
                    existingBlog.blog_title = blog.blog_title;
                    existingBlog.blog_description = blog.blog_description;

                    // Tạo lại slug nếu tiêu đề thay đổi hoặc slug đang rỗng
                    existingBlog.slug = GenerateSlug(blog.blog_title);

                    // Xử lý upload ảnh mới nếu có
                    if (photo != null && photo.Length > 0)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(photo.FileName);
                        var fileExtension = Path.GetExtension(photo.FileName);
                        var uniqueFileName = $"{fileName}_{DateTime.Now:yyyyMMddHHmmssfff}{fileExtension}";
                        var path = Path.Combine(_env.WebRootPath, "blog_images", uniqueFileName);

                        using (var stream = new FileStream(path, FileMode.Create))
                        {
                            await photo.CopyToAsync(stream);
                        }
                        // (Tùy chọn) Xóa ảnh cũ nếu cần
                        // ...
                        existingBlog.blog_image = uniqueFileName;
                    }

                    _context.Update(existingBlog);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.tbl_blog.Any(e => e.blog_id == blog.blog_id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                TempData["SuccessMessage"] = "Đã cập nhật bài viết thành công!";
                return RedirectToAction(nameof(ManageBlogs));
            }
            return View(blog);
        }


        // Hàm helper để tạo slug
        private string GenerateSlug(string title)
        {
            if (string.IsNullOrEmpty(title)) return "";

            // Chuyển hết sang chữ thường
            title = title.ToLowerInvariant();

            // Bỏ dấu
            var bytes = Encoding.GetEncoding("Cyrillic").GetBytes(title);
            title = Encoding.ASCII.GetString(bytes);

            // Xóa các ký tự đặc biệt
            title = Regex.Replace(title, @"[^a-z0-9\s-]", "");

            // Thay thế khoảng trắng bằng dấu gạch ngang
            title = Regex.Replace(title, @"\s+", "-").Trim();

            // Cắt bớt nếu quá dài và đảm bảo không kết thúc bằng dấu gạch ngang
            return title.Substring(0, title.Length <= 100 ? title.Length : 100).TrimEnd('-');
        }


        // ======================= QUẢN LÝ ĐƠN HÀNG (ADMIN) =======================

        // 1. Giao diện Danh sách đơn hàng (Có hỗ trợ tìm kiếm, lọc)
        // 1. Giao diện Danh sách đơn hàng (Có bộ lọc, phân trang, và sắp xếp ưu tiên)
        [HttpGet]
        public IActionResult Orders(string searchTerm, DateTime? fromDate, DateTime? toDate, string statusFilter, decimal? minTotal, decimal? maxTotal, int page = 1)
        {
            int pageSize = 15; // Số lượng đơn hàng trên 1 trang

            var query = _context.tbl_order
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .AsQueryable();

            // 1. Lọc theo tìm kiếm (Mã đơn hoặc Tên khách)
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(o => o.Customer.customer_name.Contains(searchTerm) || o.OrderID.ToString().Contains(searchTerm));
            }

            // 2. Lọc theo trạng thái đơn hàng
            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(o => o.OrderStatus == statusFilter);
            }

            // 3. Lọc theo ngày và tổng tiền
            if (fromDate.HasValue) query = query.Where(o => o.CreatedAt >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(o => o.CreatedAt <= toDate.Value);
            if (minTotal.HasValue) query = query.Where(o => o.TotalAmount >= minTotal.Value);
            if (maxTotal.HasValue) query = query.Where(o => o.TotalAmount <= maxTotal.Value);

            query = query.OrderBy(o => o.OrderStatus == "Chờ xác nhận" ? 0 : 1)
                         .ThenBy(o => o.CreatedAt);

            // 5. Tính toán phân trang
            int totalItems = query.Count();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // Lấy dữ liệu của trang hiện tại
            var orders = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // Truyền dữ liệu ra View để giữ lại trạng thái của form lọc và phân trang
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.SearchTerm = searchTerm;
            ViewBag.StatusFilter = statusFilter;

            return View("Orders", orders);
        }

        // 2. Giao diện Xem chi tiết đơn hàng
        [HttpGet]
        public IActionResult OrderDetail(int id)
        {
            var order = _context.tbl_order
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefault(o => o.OrderID == id);

            if (order == null) return NotFound();

            // Trả về file giao diện có tên "OrderDetails.cshtml"
            return View("OrderDetails", order);
        }


        // ================= CÁC API XỬ LÝ (TRẢ VỀ JSON CHO JAVASCRIPT) =================

        // 3. API Xác nhận đơn hàng (TC_ORD_001)
        [HttpPost]
        public IActionResult ConfirmOrder(int id)
        {
            var order = _context.tbl_order.Find(id);
            if (order != null && order.OrderStatus == "Chờ xác nhận")
            {
                order.OrderStatus = "Đã xác nhận";
                _context.SaveChanges();
                return Json(new { success = true, message = "Xác nhận đơn hàng thành công!" });
            }
            return Json(new { success = false, message = "Lỗi: Đơn hàng không ở trạng thái Chờ xác nhận." });
        }

        // 4. API Admin Hủy đơn hàng (TC_ORD_003)
        [HttpPost]
        public IActionResult CancelOrder(int id, string reason)
        {
            var order = _context.tbl_order.Find(id);
            if (order != null && order.OrderStatus == "Chờ xác nhận")
            {
                order.OrderStatus = "Đã hủy";
                order.CancelReason = reason;
                _context.SaveChanges();
                return Json(new { success = true, message = "Đã hủy đơn hàng thành công!" });
            }
            return Json(new { success = false, message = "Lỗi: Không thể hủy đơn hàng này." });
        }

        // 5. API Duyệt yêu cầu hủy của khách (TC_ORD_007)
        [HttpPost]
        public IActionResult ApproveCancel(int id)
        {
            var order = _context.tbl_order.Find(id);
            if (order != null && order.OrderStatus == "Yêu cầu hủy")
            {
                order.OrderStatus = "Đã hủy";
                _context.SaveChanges();
                return Json(new { success = true, message = "Duyệt yêu cầu hủy thành công!" });
            }
            return Json(new { success = false, message = "Lỗi: Đơn không ở trạng thái Yêu cầu hủy." });
        }

        // 6. API Từ chối yêu cầu hủy của khách (TC_ORD_008)
        [HttpPost]
        public IActionResult RejectCancel(int id, string reason)
        {
            var order = _context.tbl_order.Find(id);
            if (order != null && order.OrderStatus == "Yêu cầu hủy")
            {
                order.OrderStatus = "Chờ xác nhận"; // Trả lại trạng thái cũ
                order.RejectReason = reason;
                _context.SaveChanges();
                return Json(new { success = true, message = "Đã từ chối yêu cầu hủy của khách!" });
            }
            return Json(new { success = false, message = "Lỗi: Không thể từ chối yêu cầu này." });
        }

    }
}
