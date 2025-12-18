using System;

namespace QCS.Domain.DTOs
{
    public class RequestGridDto
    {
        public int Id { get; set; }
        public string Code { get; set; }        // เลขที่เอกสาร
        public string Title { get; set; }       // หัวข้อ
        public string VendorCode { get; set; }
        public string VendorName { get; set; }  // ผู้ขาย
        public DateTime RequestDate { get; set; } // วันที่ขอ
        public int CurrentStepId { get; set; }         // สถานะ (ส่งเป็น int ให้ Frontend ไปทำ Badge เอง)
        public string RequesterName { get; set; } // ชื่อผู้ขอ (สำคัญสำหรับ My Tasks)
    }
}