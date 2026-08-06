using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace QCS.Domain.Enum
{
    /// <summary>
    /// สถานะของเอกสาร Request ทั้งใบ (ตรงตามสัญญา GPCS Document Service)
    /// </summary>
    public enum RequestStatus
    {
        [Display(Name = "แบบร่าง")]
        [Description("แบบร่าง")]
        Draft = 0,

        [Display(Name = "อยู่ระหว่างอนุมัติ")]
        [Description("อยู่ระหว่างอนุมัติ")]
        InProcess = 1,

        [Display(Name = "ส่งกลับแก้ไข")]
        [Description("ส่งกลับแก้ไข")]
        Returned = 2,

        [Display(Name = "ไม่อนุมัติ")]
        [Description("ไม่อนุมัติ")]
        Rejected = 3,

        [Display(Name = "รอวันที่มีผล")]
        [Description("รอวันที่มีผล")]
        WaitingEffective = 4,

        [Display(Name = "เสร็จสมบูรณ์")]
        [Description("เสร็จสมบูรณ์")]
        Completed = 5,

        [Display(Name = "ยกเลิก")]
        [Description("ยกเลิก")]
        Cancelled = 6
    }



    ///// <summary>
    ///// Extension methods สำหรับ Enum Status ต่างๆ
    ///// </summary>
    //public static class StatusExtensions
    //{
    //    /// <summary>
    //    /// ดึงชื่อภาษาไทยจาก Description attribute
    //    /// </summary>
    //    public static string GetDescription(this System.Enum value)
    //    {
    //        var field = value.GetType().GetField(value.ToString());
    //        var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
    //        return attribute?.Description ?? value.ToString();
    //    }

    //    /// <summary>
    //    /// ตรวจสอบว่า Request อยู่ในสถานะที่ยังดำเนินการได้หรือไม่
    //    /// </summary>
    //    public static bool IsActive(this RequestStatus status)
    //    {
    //        return status == RequestStatus.Draft ||
    //               status == RequestStatus.Pending;
    //    }

    //    /// <summary>
    //    /// ตรวจสอบว่า Request จบกระบวนการแล้วหรือไม่
    //    /// </summary>
    //    public static bool IsFinal(this RequestStatus status)
    //    {
    //        return status == RequestStatus.Approved ||
    //               status == RequestStatus.Rejected ||
    //               status == RequestStatus.Cancelled;
    //    }

    //    /// <summary>
    //    /// ตรวจสอบว่า ApprovalStep รอการพิจารณาอยู่หรือไม่
    //    /// </summary>
    //    public static bool IsPendingAction(this approvalStatus status)
    //    {
    //        return status == approvalStatus.InReview;
    //    }

    //    /// <summary>
    //    /// ตรวจสอบว่า ApprovalStep ผ่านแล้วหรือไม่ (รวมถึงกรณีข้าม)
    //    /// </summary>
    //    public static bool IsPassed(this approvalStatus status)
    //    {
    //        return status == approvalStatus.Approved ||
    //               status == approvalStatus.Skipped;
    //    }
    //}
}