using QCS.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QCS.Domain.DTOs
{
    public class PurchaseRequestDetailDto
    {
        public int PurchaseRequestId { get; set; }
        public string DocumentNo { get; set; }
        public string Title { get; set; }
        public DateTime RequestDate { get; set; }
        public string Status { get; set; }
        public string RequesterName { get; set; }
        public int CurrentStepId { get; set; }

        public int VendorId { get; set; }
        public string VendorName { get; set; }

        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string? Remark { get; set; }

        public PermissionDto Permissions { get; set; } = new();

        // === [NEW] เพิ่มส่วนนี้ครับ ===
        public WorkflowRouteDetailDto WorkflowRoute { get; set; }
        // ==========================

        public List<QuotationItemDto> Quotations { get; set; }
    }

    public class PermissionDto
    {
        public bool CanApprove { get; set; }
        public bool CanReject { get; set; }
        public bool CanEdit { get; set; }
    }

   
}