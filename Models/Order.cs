using MTKPM_FE.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MTKPM_FE.Models
{
    public class Order
    {
        [Key]
        public int OrderID { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public string Email { get; set; }

        public decimal TotalAmount { get; set; } // Tổng tiền đơn hàng (100%)
        public decimal DepositAmount { get; set; } = 0; // Số tiền cần quét QR (30% nếu COD, 100% nếu Online)
        public bool IsDeposited { get; set; } = false; // Khách đã quét QR chuyển khoản chưa?
        public string PaymentMethod { get; set; } = "COD"; // "COD" hoặc "ONLINE"
        public string? CancelReason { get; set; }
        public string? RejectReason { get; set; }
        public string PaymentStatus { get; set; } = "Pending";
        public string OrderStatus { get; set; } = "New"; // "Chờ xác nhận", "Đã hủy"...

        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    }
}