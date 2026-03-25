using MTKPM_FE.Helpers;
using MTKPM_FE.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MTKPM_FE.Controllers
{
    public class HomeController : Controller
    {
        private readonly myContext _context;

        public HomeController(myContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _context.tbl_product.OrderByDescending(p => p.CreatedAt).Take(8).ToListAsync();
            ViewBag.News = await _context.tbl_blog.OrderByDescending(b => b.CreatedAt).Take(4).ToListAsync();
            return View(products);
        }

        public async Task<IActionResult> AllProducts(List<int> categoryIds, string categorySlug, string sortOrder, int? page, string searchTerm, int? minPrice, int? maxPrice)
        {
            var productsQuery = _context.tbl_product.AsNoTracking();

            int pageSize = 12; // TC_SCH_018: Phân trang 12 sản phẩm
            int pageNumber = (page ?? 1);

            ViewBag.CurrentCategories = categoryIds ?? new List<int>();
            ViewBag.CurrentSlug = categorySlug;
            ViewBag.CurrentSort = sortOrder;
            ViewBag.CurrentSearch = searchTerm;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.Categories = await _context.tbl_category.ToListAsync();

            // 1. CHỈ CHẶN LỖI LOGIC GIÁ (Số âm hoặc Từ > Đến)
            // Đã xóa bỏ ModelState.IsValid gây lỗi crash trang
            if ((minPrice.HasValue && minPrice < 0) ||
                (maxPrice.HasValue && maxPrice < 0) ||
                (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice))
            {
                ViewBag.SearchError = "giá trị nhập vào không hợp lệ.";
                return View(await PaginatedList<Product>.CreateAsync(productsQuery.Where(p => false), 1, pageSize));
            }

            // 2. ÁP DỤNG CÁC ĐIỀU KIỆN LỌC (Nếu có)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                productsQuery = productsQuery.Where(p => p.product_name.Contains(searchTerm) || p.product_description.Contains(searchTerm));
            }

            if (categoryIds != null && categoryIds.Any())
            {
                productsQuery = productsQuery.Where(p => p.cat_id.HasValue && categoryIds.Contains(p.cat_id.Value));
            }
            else if (!string.IsNullOrEmpty(categorySlug))
            {
                if (categorySlug.ToLower() == "sanphammoi")
                {
                    productsQuery = productsQuery.OrderByDescending(p => p.CreatedAt);
                    sortOrder = "newest";
                    ViewBag.CurrentSort = sortOrder;
                }
            }

            if (minPrice.HasValue) productsQuery = productsQuery.Where(p => p.product_price >= minPrice.Value);
            if (maxPrice.HasValue) productsQuery = productsQuery.Where(p => p.product_price <= maxPrice.Value);

            // 3. KIỂM TRA: CHỈ BÁO LỖI NẾU KHÁCH "CÓ GÕ TÌM/LỌC" MÀ KHÔNG CÓ KẾT QUẢ
            // Nếu khách không gõ gì (bỏ trống form) thì biến này = false -> Sẽ hiển thị TẤT CẢ sản phẩm
            bool hasFilter = !string.IsNullOrWhiteSpace(searchTerm) || minPrice.HasValue || maxPrice.HasValue || (categoryIds != null && categoryIds.Any());

            

            // 4. SẮP XẾP SẢN PHẨM
            switch (sortOrder)
            {
                case "price_asc": productsQuery = productsQuery.OrderBy(p => p.product_discount_price ?? p.product_price); break;
                case "price_desc": productsQuery = productsQuery.OrderByDescending(p => p.product_discount_price ?? p.product_price); break;
                case "rating_desc": productsQuery = productsQuery.OrderByDescending(p => p.product_rating); break;
                case "name_asc": productsQuery = productsQuery.OrderBy(p => p.product_name); break;
                case "newest": productsQuery = productsQuery.OrderByDescending(p => p.CreatedAt); break;
                default: productsQuery = productsQuery.OrderByDescending(p => p.product_id); break;
            }

            return View(await PaginatedList<Product>.CreateAsync(productsQuery, pageNumber, pageSize));
        }

        // --- CÁC HÀM CÒN LẠI GIỮ NGUYÊN ---
        public IActionResult About() => View();
        public IActionResult Contact() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(ContactMessage contactMessage)
        {
            var customerIdSession = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(customerIdSession)) return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Contact", "Home") });

            if (ModelState.IsValid)
            {
                var customerId = int.Parse(customerIdSession);
                var customer = await _context.tbl_customer.FindAsync(customerId);
                _context.tbl_feedback.Add(new Feedback { user_name = customer?.customer_name ?? contactMessage.Name, user_message = contactMessage.Message, Content = contactMessage.Message, Rating = 0, CreatedAt = DateTime.Now, CustomerId = customerId });
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cảm ơn bạn đã gửi tin nhắn. Chúng tôi sẽ phản hồi sớm nhất!";
                return RedirectToAction("Contact");
            }
            return View(contactMessage);
        }

        public async Task<IActionResult> ProductDetail(int id)
        {
            var product = await _context.tbl_product.FindAsync(id);
            if (product == null) return NotFound();
            var recentlyViewed = HttpContext.Session.Get<List<int>>("RecentlyViewed") ?? new List<int>();
            if (!recentlyViewed.Contains(id)) recentlyViewed.Insert(0, id);
            var limitedList = recentlyViewed.Take(5).ToList();
            HttpContext.Session.Set("RecentlyViewed", limitedList);
            ViewBag.RecentlyViewedProducts = await _context.tbl_product.Where(p => limitedList.Contains(p.product_id) && p.product_id != id).ToListAsync();
            return View(product);
        }

        [HttpGet]
        public async Task<IActionResult> SearchProducts(string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return Json(new List<Product>());
            var products = await _context.tbl_product.Where(p => p.product_name.ToLower().Contains(q.ToLower())).Take(10).Select(p => new { p.product_id, p.product_name, p.product_image, p.product_price, p.product_discount_price }).ToListAsync();
            return Json(products);
        }
    }
}