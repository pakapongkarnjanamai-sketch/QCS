using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QCS.Domain.Models
{
    public class Quotation : BaseEntity
    {
        public int Id { get; set; }
        public int PurchaseRequestId { get; set; }
        public string VendorName { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsSelected { get; set; } // เลือกเจ้านี้หรือไม่?

        // ข้อมูลไฟล์ PDF ต้นฉบับ
        public string FilePath { get; set; } // Path ที่เก็บไฟล์ใน Server
        public string OriginalFileName { get; set; }
    }
}
