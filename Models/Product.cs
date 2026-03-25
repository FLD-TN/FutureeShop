using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MTKPM_FE.Models
{
    public class Product
    {
        [Key]
        public int product_id { get; set; }

        [Required(ErrorMessage = "Tên sản phẩm không được để trống")]
        public string product_name { get; set; }

        [Required(ErrorMessage = "Giá sản phẩm không được để trống")]
        [Range(0, int.MaxValue, ErrorMessage = "Giá không hợp lệ")] // Không cho phép giá âm (TC_PROD_013)
        public int product_price { get; set; }

        public string product_description { get; set; }
        public string product_image { get; set; }

        public int? cat_id { get; set; }
        public Category Category { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? product_discount_price { get; set; }
        public int product_stock { get; set; } = 0; // Số lượng tồn kho           
        public double product_rating { get; set; } = 0.0;
        public int product_review_count { get; set; } = 0;

        [NotMapped]
        public bool IsNew { get; set; } = false;
        public ICollection<ProductImage> ProductImages { get; set; }
    }
}