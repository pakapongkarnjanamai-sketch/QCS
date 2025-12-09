using Microsoft.AspNetCore.Http;

namespace QCS.Domain.DTOs
{
    public class UpdatePurchaseRequestDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int VendorId { get; set; }
        public string VendorName { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string? Remark { get; set; }

        public List<IFormFile>? NewAttachments { get; set; }
        public string? DeletedFileIds { get; set; }

        // === [NEW] สำหรับไฟล์ใหม่ (เหมือนหน้า Create) ===
        public string? QuotationsJson { get; set; }

        // === [NEW] สำหรับอัปเดตไฟล์เดิม (ระบุ ID และ Type ใหม่) ===
        public string? UpdatedQuotationsJson { get; set; }
    }

    // Class สำหรับรับข้อมูลไฟล์เดิมที่จะแก้
    public class UpdateQuotationItemDto
    {
        public int Id { get; set; }
        public int DocumentTypeId { get; set; }
    }
}