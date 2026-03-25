#nullable disable
using MTKPM_FE.Models;
using MTKPM_FE.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;

namespace MTKPM_FE.Controllers
{
    [AllowAnonymous]
    public class CartController : Controller
    {
        private readonly myContext _context;
        public const string CARTKEY = "cart";

        public CartController(myContext context)
        {
            _context = context;
        }

        private List<CartItemViewModel> GetCartItems()
        {
            return HttpContext.Session.Get<List<CartItemViewModel>>(CARTKEY) ?? new List<CartItemViewModel>();
        }

        private void SaveCartSession(List<CartItemViewModel> cart)
        {
            HttpContext.Session.Set(CARTKEY, cart);
        }

        public IActionResult Index()
        {
            return View(GetCartItems());
        }

        [HttpPost]
        public IActionResult AddToCart(int productId, int quantity = 1)
        {
            var product = _context.tbl_product.Find(productId);
            if (product == null) return Json(new { success = false, message = "Sản phẩm không tồn tại." });

            var cart = GetCartItems();
            var cartItem = cart.FirstOrDefault(p => p.ProductId == productId);
            int currentQty = cartItem != null ? cartItem.Quantity : 0;

            if (currentQty + quantity > product.product_stock)
            {
                return Json(new { success = false, message = "Số lượng vượt quá tồn kho. Hiện chỉ còn " + product.product_stock + " sản phẩm." });
            }

            if (cartItem != null)
            {
                cartItem.Quantity += quantity;
            }
            else
            {
                cart.Add(new CartItemViewModel
                {
                    ProductId = product.product_id,
                    ProductName = product.product_name ?? "Sản phẩm",
                    Price = product.product_discount_price ?? product.product_price,
                    Quantity = quantity,
                    ProductImage = product.product_image ?? "default.jpg"
                });
            }
            SaveCartSession(cart);
            return Json(new { success = true, message = "Đã thêm sản phẩm vào giỏ hàng." });
        }

        [HttpPost]
        public IActionResult UpdateCart(int productId, int quantity)
        {
            var product = _context.tbl_product.Find(productId);
            if (product == null) return Json(new { success = false, message = "Sản phẩm không tồn tại." });

            var cart = GetCartItems();
            var cartItem = cart.FirstOrDefault(p => p.ProductId == productId);

            if (cartItem != null)
            {
                if (quantity <= 0)
                {
                    cart.Remove(cartItem);
                    SaveCartSession(cart);
                    return Json(new { success = true, message = "Đã xóa khỏi giỏ hàng.", removed = true });
                }

                if (quantity > product.product_stock)
                {
                    return Json(new { success = false, message = "Số lượng vượt quá tồn kho. Hiện chỉ còn " + product.product_stock + " sản phẩm.", revertQty = cartItem.Quantity });
                }

                cartItem.Quantity = quantity;
                SaveCartSession(cart);
                return Json(new { success = true, message = "Cập nhật thành công.", currentQty = quantity });
            }
            return Json(new { success = false, message = "Không tìm thấy sản phẩm trong giỏ." });
        }
    }
}