using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QCS.Domain.Enum
{
    /// <summary>
    /// สถานะของแต่ละขั้นตอนการอนุมัติ (Approval Step)
    /// </summary>
    public enum approvalStatus
    {
        [Display(Name = "ยังมาไม่ถึงขั้นตอนนี้")]
        [Description("ยังมาไม่ถึงขั้นตอนนี้")]
        Next = 0,
        [Display(Name = "รอพิจารณา")]
        [Description("รอพิจารณา")]
        InReview = 1,
        [Display(Name = "อนุมัติ")]
        [Description("อนุมัติ")]
        Approved = 2,
        [Display(Name = "ข้าม")]
        [Description("ข้าม")]
        Skipped = 3,
        [Display(Name = "ไม่อนุมัติ")]
        [Description("ไม่อนุมัติ")]
        Rejected = 9
    }
}
