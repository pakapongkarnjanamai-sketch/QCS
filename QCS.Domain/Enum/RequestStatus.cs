using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace QCS.Domain.Enum
{
    /// <summary>
    /// สถานะของเอกสาร Request ทั้งใบ
    /// </summary>
    public enum RequestStatus
    {
        [Display(Name = "แบบร่าง")]
        [Description("แบบร่าง")]
        Draft = 0,

        [Display(Name = "รออนุมัติ")]
        [Description("รออนุมัติ")]
        Pending = 1,

        [Display(Name = "อนุมัติครบถ้วน")]
        [Description("อนุมัติครบถ้วน")]
        Approved = 2,

        [Display(Name = "ไม่อนุมัติ")]
        [Description("ไม่อนุมัติ")]
        Rejected = 9,

        //[Display(Name = "ยกเลิก")]
        //[Description("ยกเลิก")]
        //Cancelled = 99
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