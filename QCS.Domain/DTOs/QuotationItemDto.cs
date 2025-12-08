// File: QCS.Domain/DTOs/QuotationItemDto.cs
using System;

namespace QCS.Domain.DTOs
{
    public class QuotationItemDto
    {
        public string FileName { get; set; } // ใช้จับคู่กับไฟล์ใน Attachments
        public int DocumentTypeId { get; set; }

        // ข้อมูลอื่นๆ ที่อาจจะมีในแต่ละไฟล์ (ถ้ามี)
        // public string Description { get; set; }
    }
}
