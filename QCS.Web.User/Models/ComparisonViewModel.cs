using System.ComponentModel.DataAnnotations;

namespace QCS.Web.User.Models
{
    public class ComparisonViewModel
    {
        // Header Info
        public int PurchaseRequestId { get; set; }
        public string DocumentNo { get; set; }
        public string Title { get; set; }

        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}")]
        public DateTime RequestDate { get; set; }
        public string Status { get; set; }
        public string RequesterName { get; set; } // ชื่อผู้ขอซื้อ

        // Approval Info (ใช้สำหรับส่งค่ากลับไปอนุมัติ)
        public int CurrentStepId { get; set; } // ID ของ Step ที่กำลังรออนุมัติอยู่
        public string Comment { get; set; }

        // Detail Info (รายการใบเสนอราคาที่จะเอามาเทียบกัน)
        public List<QuotationComparisonItem> Quotations { get; set; } = new List<QuotationComparisonItem>();
    }

    public class QuotationComparisonItem
    {
        public int Id { get; set; }
        public string VendorName { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsSelected { get; set; } // เป็นเจ้าที่ถูกเลือก (Winner) หรือไม่

        // File Info
        public string OriginalFileName { get; set; }
        public string FilePath { get; set; } // หรือ Download Url
    }
}