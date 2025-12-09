using QCS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace QCS.Domain.Models
{
    public class PurchaseRequest : BaseEntity
    {
        public string Code { get; set; } // Map to 'documentNo'
        public string Title { get; set; }
        public DateTime RequestDate { get; set; }
        public int Status { get; set; }
        public int CurrentStepId { get; set; }
        [NotMapped]
        public WorkflowStep CurrentStep
        {
            get => (WorkflowStep)CurrentStepId;
            set => CurrentStepId = (int)value;
        }
        public int VendorId { get; set; }
        public string VendorName { get; set; }

        // [New] ย้ายมาไว้ที่ Header ตาม JSON Requirement
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string Comment { get; set; } // ใช้แทน Description

        public virtual ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();
        public virtual ICollection<ApprovalStep>  ApprovalSteps  { get; set; } = new List<ApprovalStep>();


    }
}