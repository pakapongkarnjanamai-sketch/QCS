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
        [Display(Name = "จบกระบวนการ")]
        [Description("จบกระบวนการ")]
        Completed = 3,
        [Display(Name = "ไม่อนุมัติ")]
        [Description("ไม่อนุมัติ")]
        Rejected = 9,
        [Display(Name = "ยกเลิก")]
        [Description("ยกเลิก")]
        Cancelled = 99
    }

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
        [Display(Name = "อนุมัติผ่าน")]
        [Description("อนุมัติผ่าน")]
        Approved = 2,
        [Display(Name = "ข้าม")]
        [Description("ข้าม")]
        Skipped = 3,
        [Display(Name = "ไม่อนุมัติ")]
        [Description("ไม่อนุมัติ")]
        Rejected = 9
    }

    /// <summary>
    /// Extension methods สำหรับ Enum Status ต่างๆ
    /// </summary>
    public static class StatusExtensions
    {
        /// <summary>
        /// ดึงชื่อภาษาไทยจาก Description attribute
        /// </summary>
        public static string GetDescription(this System.Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? value.ToString();
        }

        /// <summary>
        /// ตรวจสอบว่า PurchaseRequest อยู่ในสถานะที่ยังดำเนินการได้หรือไม่
        /// </summary>
        public static bool IsActive(this RequestStatus status)
        {
            return status == RequestStatus.Draft ||
                   status == RequestStatus.Pending;
        }

        /// <summary>
        /// ตรวจสอบว่า PurchaseRequest จบกระบวนการแล้วหรือไม่
        /// </summary>
        public static bool IsFinal(this RequestStatus status)
        {
            return status == RequestStatus.Approved ||
                   status == RequestStatus.Completed ||
                   status == RequestStatus.Rejected ||
                   status == RequestStatus.Cancelled;
        }

        /// <summary>
        /// ตรวจสอบว่า ApprovalStep รอการพิจารณาอยู่หรือไม่
        /// </summary>
        public static bool IsPendingAction(this approvalStatus status)
        {
            return status == approvalStatus.InReview;
        }

        /// <summary>
        /// ตรวจสอบว่า ApprovalStep ผ่านแล้วหรือไม่ (รวมถึงกรณีข้าม)
        /// </summary>
        public static bool IsPassed(this approvalStatus status)
        {
            return status == approvalStatus.Approved ||
                   status == approvalStatus.Skipped;
        }
    }
}