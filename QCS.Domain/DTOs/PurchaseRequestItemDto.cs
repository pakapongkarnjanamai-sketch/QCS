using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QCS.Domain.DTOs
{
    public class PurchaseRequestItemDto
    {
        [Required(ErrorMessage = "กรุณาระบุชื่อสินค้า")]
        public string ProductName { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "จำนวนต้องมากกว่า 0")]
        public int Quantity { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "ราคาต้องมากกว่า 0")]
        public decimal UnitPrice { get; set; }

        public string VendorName { get; set; }
    }
}
