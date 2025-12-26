using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace QCS.Domain.Enum
{
    /// <summary>
    /// สถานะของแต่ละขั้นตอนการอนุมัติ (Approval Step)
    /// </summary>
    public enum ApprovalStatus
    {
        [Display(Name = "ไม่ถึงขั้นตอน")]
        [Description("ไม่ถึงขั้นตอน")]
        Next = 0,
        [Display(Name = "รออนุมัติ")]
        [Description("รออนุมัติ")]
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
