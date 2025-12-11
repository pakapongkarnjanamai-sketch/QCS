using System;

namespace QCS.Domain.DTOs
{
    public class QuotationItemDto
    {
        // เพิ่ม Id (Nullable เผื่อใช้กรณีไฟล์ใหม่ยังไม่มี ID)
        public int Id { get; set; }

        // ใช้รับชื่อไฟล์ (บางทีใช้ FileName บางทีใช้ OriginalFileName ก็ให้มีทั้งคู่)
        public string FileName { get; set; }
        public string OriginalFileName { get; set; }

        public int DocumentTypeId { get; set; }

        // เพิ่ม FilePath สำหรับส่ง URL ให้หน้าบ้าน Download
        public string FilePath { get; set; }
    }
}