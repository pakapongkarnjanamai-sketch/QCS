using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QCS.Domain.DTOs
{
    public class CreatePurchaseRequestDto
    {
        [Required(ErrorMessage = "กรุณาระบุวันที่ขอซื้อ")]
        public DateTime RequestDate { get; set; }

        [Required(ErrorMessage = "กรุณาระบุรหัสผู้ขอ")]
        public string RequesterId { get; set; }

        [StringLength(100)]
        public string Department { get; set; }

        public string Remarks { get; set; }

        // รายการสินค้า (มีได้หลายรายการ)
        [Required]
        [MinLength(1, ErrorMessage = "ต้องมีรายการสินค้าอย่างน้อย 1 รายการ")]
        public List<PurchaseRequestItemDto> Items { get; set; }
    }
}
