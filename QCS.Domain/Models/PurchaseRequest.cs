using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QCS.Domain.Models
{
    public class PurchaseRequest : BaseEntity
    {
        public int Id { get; set; }
        public string DocumentNo { get; set; } // เช่น PR-2024-001
        public string Title { get; set; }
        public DateTime RequestDate { get; set; }
        public string Status { get; set; } // Draft, InReview, Approved, Rejected

        public List<Quotation> Quotations { get; set; } // รายการใบเสนอราคาที่เอามาเทียบ
        public List<ApprovalStep> ApprovalSteps { get; set; } // ประวัติการอนุมัติ
    }
}
