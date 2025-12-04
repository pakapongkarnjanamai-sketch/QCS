namespace QCS.Web.User.Models
{
    public class QuotationItem
    {
        public string TempId { get; set; } // ใช้เชื่อมโยงกับไฟล์ใน Frontend
        public string VendorName { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsSelected { get; set; }
        public string FileName { get; set; }
    }
}
