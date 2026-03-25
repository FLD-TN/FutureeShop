using MTKPM_FE.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;

namespace MTKPM_FE.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly myContext _db;

        public CheckoutController(myContext db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var custIdStr = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(custIdStr))
            {
                // TC_CHK_001: Chưa đăng nhập bị đẩy về trang Login kèm thông báo
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để thanh toán.";
                return RedirectToAction("Login", "Account", new { returnUrl = "/Checkout" });
            }

            // Lấy tổng tiền giỏ hàng từ Session (Giả định bạn đã có hàm GetCartItems)
            var cart = HttpContext.Session.Get<System.Collections.Generic.List<CartItemViewModel>>("cart");
            if (cart == null || !cart.Any())
            {
                return RedirectToAction("Index", "Cart");
            }

            ViewBag.TotalCartPrice = cart.Sum(c => c.Price * c.Quantity);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult PlaceOrder(Order model)
        {
            var custIdStr = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(custIdStr)) return RedirectToAction("Login", "Account");

            // =========================================================
            ModelState.Remove("Customer");
            ModelState.Remove("OrderDetails");
            ModelState.Remove("PaymentStatus");
            ModelState.Remove("OrderStatus");
            ModelState.Remove("CancelReason");
            ModelState.Remove("RejectReason");
            // =========================================================

            // Validate logic cho SĐT (TC_CHK_004, TC_CHK_005)
            if (string.IsNullOrEmpty(model.PhoneNumber) || !System.Text.RegularExpressions.Regex.IsMatch(model.PhoneNumber, @"^(03|05|07|08|09)\d{8}$"))
            {
                ModelState.AddModelError("PhoneNumber", "Số điện thoại không hợp lệ");
            }

            // Nếu Form có lỗi (thiếu tên, sđt sai...) -> Tải lại trang và báo lỗi đỏ
            if (!ModelState.IsValid)
            {
                var cart = HttpContext.Session.Get<System.Collections.Generic.List<CartItemViewModel>>("cart");
                ViewBag.TotalCartPrice = cart?.Sum(c => c.Price * c.Quantity) ?? 0;
                return View("Index", model);
            }

            // Gán thông tin ẩn
            model.CustomerId = int.Parse(custIdStr);
            model.CreatedAt = DateTime.Now;
            model.OrderStatus = "Chờ xác nhận";
            model.PaymentStatus = "Pending";

            // Lấy tổng tiền từ Session để chống Fake giá từ HTML
            var sessionCart = HttpContext.Session.Get<System.Collections.Generic.List<CartItemViewModel>>("cart");
            model.TotalAmount = sessionCart?.Sum(c => c.Price * c.Quantity) ?? 0;

            // LOGIC TÍNH TIỀN QUÉT QR: 30% cho COD, 100% cho ONLINE
            if (model.PaymentMethod == "COD")
            {
                model.DepositAmount = Math.Round((model.TotalAmount * 0.3m) / 1000m) * 1000m;
            }
            else
            {
                model.DepositAmount = model.TotalAmount;
            }

            // Lưu vào Database
            _db.tbl_order.Add(model);
            _db.SaveChanges();

            // LƯU CHI TIẾT ĐƠN HÀNG (OrderDetail)
            if (sessionCart != null && sessionCart.Any())
            {
                foreach (var item in sessionCart)
                {
                    var orderDetail = new OrderDetail
                    {
                        OrderID = model.OrderID,
                        ProductID = item.ProductId,
                        Quantity = item.Quantity,
                        Price = item.Price
                    };
                    _db.tbl_orderdetail.Add(orderDetail);
                }
                _db.SaveChanges();
            }

            // Xóa giỏ hàng sau khi tạo đơn thành công
            HttpContext.Session.Remove("cart");

            // Chuyển sang trang Payment để hiển thị QR
            return RedirectToAction("CheckoutQR", "Payment", new { orderId = model.OrderID });
        }
    }
}