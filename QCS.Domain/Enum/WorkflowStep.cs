using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QCS.Domain.Enum
{
    public enum WorkflowStep
    {
        // สถานะเริ่มต้น (ยังไม่ส่ง Workflow)
        Draft = 0,

        // ขั้นตอนตาม Workflow Route (ID ต้องตรงกับ Database Workflow)
        Purchaser = 1,  // จัดซื้อตรวจสอบ
        Verifier = 2,   // ผู้ตรวจสอบ
        Manager = 3,    // ผู้จัดการอนุมัติ

        // สถานะจบการทำงาน
        Completed = 99,  // อนุมัติครบทุกขั้นตอนแล้ว
        Rejected = -1    // ถูกไม่อนุมัติ (ถ้าต้องการเก็บแยก)
    }
}
