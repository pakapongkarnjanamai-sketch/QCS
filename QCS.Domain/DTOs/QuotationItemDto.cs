// File: QCS.Domain/DTOs/QuotationItemDto.cs
using System;

namespace QCS.Domain.DTOs
{
    public class QuotationItemDto
    {
        public string FileName { get; set; }

        // เพิ่มฟิลด์ใหม่ให้ตรงกับหน้าบ้าน
        public string VendorId { get; set; } // ใช้ string เพื่อรองรับทั้ง Int ID หรือ GUID
        public string VendorName { get; set; }
        public decimal TotalAmount { get; set; }
        public int DocumentTypeId { get; set; }

        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string Comment { get; set; }

        public bool IsSelected { get; set; }
    }
}