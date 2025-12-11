using System.ComponentModel;
using System.Reflection;

namespace QCS.Domain.Enum
{
    /// <summary>
    /// สถานะของเอกสาร Purchase Request ทั้งใบ
    /// </summary>
    public enum RequestStatus
    {
        [Description("แบบร่าง")]
        Draft = 0,

        [Description("รออนุมัติ")]
        Pending = 1,

        [Description("อนุมัติครบถ้วน")]
        Approved = 2,

        [Description("จบกระบวนการ")]
        Completed = 3,

        [Description("ไม่อนุมัติ")]
        Rejected = 9,

        [Description("ยกเลิก")]
        Cancelled = 99
    }

    /// <summary>
    /// สถานะของแต่ละขั้นตอนการอนุมัติ (Approval Step)
    /// </summary>
    public enum ApprovalStepStatus
    {
        [Description("ยังมาไม่ถึงขั้นตอนนี้")]
        Draft = 0,

        [Description("รอพิจารณา")]
        Pending = 1,

        [Description("อนุมัติผ่าน")]
        Approved = 2,

        [Description("ข้าม")]
        Skipped = 3,

        [Description("ตีกลับ/ไม่อนุมัติ")]
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
        public static bool IsPendingAction(this ApprovalStepStatus status)
        {
            return status == ApprovalStepStatus.Pending;
        }

        /// <summary>
        /// ตรวจสอบว่า ApprovalStep ผ่านแล้วหรือไม่ (รวมถึงกรณีข้าม)
        /// </summary>
        public static bool IsPassed(this ApprovalStepStatus status)
        {
            return status == ApprovalStepStatus.Approved ||
                   status == ApprovalStepStatus.Skipped;
        }
    }
}