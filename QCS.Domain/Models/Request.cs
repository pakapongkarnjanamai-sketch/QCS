using QCS.Domain.Enum;
using System;

namespace QCS.Domain.Models
{
    public class Request : BaseEntity
    {
        public string Code { get; set; } // Map to 'documentNo'
        public string Title { get; set; }
        public DateTime RequestDate { get; set; }
        public int Status { get; set; }
        public int? CurrentStepSequence { get; set; }

        public Guid? ApprovalDocumentId { get; set; }
        public string? ApprovalDocumentNumber { get; set; }
        public string? CurrentStepName { get; set; }
        public DateTime? StatusSyncedAt { get; set; }

        public string VendorCode { get; set; }
        public string VendorName { get; set; }

        public string? SourceSystem { get; set; }
        public string? SourceCode { get; set; }

        // [New] ย้ายมาไว้ที่ Header ตาม JSON Requirement
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string? Remark { get; set; }

        public virtual ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();
        public virtual ICollection<ApprovalStep> ApprovalSteps { get; set; } = new List<ApprovalStep>();


    }
}