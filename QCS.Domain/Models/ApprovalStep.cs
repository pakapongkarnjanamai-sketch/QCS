using System.ComponentModel.DataAnnotations.Schema;

namespace QCS.Domain.Models
{
    public class ApprovalStep : BaseEntity
    {
        public int PurchaseRequestId { get; set; }
        [ForeignKey("PurchaseRequestId")] // ถ้าจำเป็น
        public virtual PurchaseRequest PurchaseRequest { get; set; }

        public int Sequence { get; set; }
        public string ApproverName { get; set; } // หรือ ApproverId
        public string Role { get; set; }

        public int Status { get; set; } // <--- ต้องเป็น int ให้ตรงกับ Consts ใหม่

        public DateTime? ActionDate { get; set; }
        public string? Comment { get; set; }
    }
}