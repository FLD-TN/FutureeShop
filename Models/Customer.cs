using System.ComponentModel.DataAnnotations;

namespace MTKPM_FE.Models
{
    public class Customer
    {
        [Key]
        public int customer_id { get; set; }

        [Required(ErrorMessage = "Họ tên không được để trống")]
        public string customer_name { get; set; }

        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string customer_email { get; set; }

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        public string customer_password { get; set; }

        [Required(ErrorMessage = "Số điện thoại không được để trống")]
        [RegularExpression(@"^(0[3|5|7|8|9])+([0-9]{8})$", ErrorMessage = "Số điện thoại không hợp lệ")]
        public string customer_phone { get; set; }

        public string customer_gender { get; set; }
        public string customer_country { get; set; }
        public string customer_city { get; set; }
        public string customer_address { get; set; }
        public string customer_image { get; set; }
        public bool IsLocked { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}