// QCS.Domain/Models/ApprovalStep.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QCS.Domain.Models
{
    public class ApprovalStep : BaseEntity
    {
        public int PurchaseRequestId { get; set; }
        
        [ForeignKey("PurchaseRequestId")]
        public virtual PurchaseRequest PurchaseRequest { get; set; }

        public int Sequence { get; set; }
        public string StepName { get; set; } = string.Empty;

        // --- เพิ่ม 2 คอลัมน์นี้ ---
        public string? ApproverNId { get; set; }  // รหัสพนักงาน (User ID) ของคนอนุมัติ
        public string? ApproverName { get; set; } // ชื่อ-นามสกุล ของคนอนุมัติ
        // -----------------------

        public int Status { get; set; } // 0=Draft, 1=Pending, 2=Approved, 9=Rejected
        public DateTime? ActionDate { get; set; }
        public string? Comment { get; set; }
    }
}