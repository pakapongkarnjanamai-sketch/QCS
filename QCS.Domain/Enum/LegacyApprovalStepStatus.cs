namespace QCS.Domain.Enum
{
    /// <summary>
    /// สถานะของ ApprovalStep ในระบบเดิมก่อนย้ายไปใช้ GPCS Central Approval
    /// ใช้สำหรับการอ่านข้อมูลประวัติเดิมเท่านั้น
    /// </summary>
    public enum LegacyApprovalStepStatus
    {
        Draft = 0,
        Pending = 1,
        Approved = 2,
        Rejected = 9
    }
}
