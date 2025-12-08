using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QCS.Domain.DTOs
{
    public class CreatePurchaseRequestDto
    {
        [Required]
        public string Title { get; set; }

        // ข้อมูล Header ที่ย้ายมา
        public int VendorId { get; set; }
        public string VendorName { get; set; }

        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string Comment { get; set; }

        // รับไฟล์แนบจริง (Binary) จาก FormData
        public List<IFormFile> Attachments { get; set; }

        // รับ Metadata ของไฟล์ (เช่น DocumentTypeId) เป็น JSON String
        // เพราะ FormData ส่ง Array of Object ซับซ้อนไม่ได้ ต้องส่งเป็น JSON String แล้วมา Parse เอา
        public string QuotationsJson { get; set; }
    }

  
  
}