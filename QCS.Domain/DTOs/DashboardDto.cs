using QCS.Domain.Models;
using System.Collections.Generic;

namespace QCS.Domain.DTOs
{
    public class DashboardDto
    {
        // สำหรับ Widget ด้านบน
        public int TotalCreated { get; set; }      // เอกสารที่ฉันสร้างทั้งหมด
        public int TotalPending { get; set; }      // เอกสารที่ฉันสร้างและรออนุมัติ
        public int TotalCompleted { get; set; }    // เอกสารที่จบแล้ว (อนุมัติ/ไม่อนุมัติ)

        // สำหรับ Badge บน Tab
        public int MyRequestCount { get; set; }    // จำนวนใน Tab "เอกสารของฉัน"
        public int MyTaskCount { get; set; }       // จำนวนใน Tab "งานรออนุมัติ"
    }
}