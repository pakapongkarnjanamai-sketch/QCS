using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace QCS.Domain.Models
{
    public class Quotation : BaseEntity
    {
        public int Id { get; set; }
        public int PurchaseRequestId { get; set; }

        // ข้อมูลผู้ขาย
        public string VendorId { get; set; }      // รหัส Vendor (ที่รับมาจากหน้าบ้าน)
        public string VendorName { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalAmount { get; set; }

        // ข้อมูลเพิ่มเติมของใบเสนอราคา
        public int DocumentTypeId { get; set; }   // ประเภทเอกสาร
        public DateTime? ValidFrom { get; set; }  // วันที่เริ่ม
        public DateTime? ValidUntil { get; set; } // วันที่สิ้นสุด
        public string Comment { get; set; }       // หมายเหตุ

        public bool IsSelected { get; set; }      // สถานะการเลือก (Selected)

        // ความสัมพันธ์กับไฟล์แนบ (เก็บใน Table AttachmentFiles)
        public int? AttachmentFileId { get; set; }

        [ForeignKey("AttachmentFileId")]
        public AttachmentFile AttachmentFile { get; set; }

        // ชื่อไฟล์ดั้งเดิม
        public string OriginalFileName { get; set; }

        // Property นี้ไม่ได้ Map ลง Database จริง แต่เอาไว้ใส่ URL ตอนส่งข้อมูลกลับไปหน้าบ้าน
        [NotMapped]
        public string FilePath { get; set; }
    }
}