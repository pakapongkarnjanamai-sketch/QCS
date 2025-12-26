using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QCS.Domain.Enum
{
    public enum WorkflowStep
    {
        // สถานะเริ่มต้น (ยังไม่ส่ง Workflow)
        [Display(Name = "บันทึก")]
        Draft = 0,

        // ขั้นตอนตาม Workflow Route (ID ต้องตรงกับ Database Workflow)
        [Display(Name = "บันทึก")]
        Purchaser = 1,
        [Display(Name = "รออนุมัติ")]
        Verifier = 2,
        [Display(Name = "รออนุมัติ")]
        Manager = 3,

        // สถานะจบการทำงาน
        [Display(Name = "อนุมัติครบถ้วน")]
        Completed = 99,
        [Display(Name = "ไม่อนุมัติ")]
        Rejected = -1
    }
}
