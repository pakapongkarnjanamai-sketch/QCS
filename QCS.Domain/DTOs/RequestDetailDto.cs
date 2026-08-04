using QCS.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QCS.Domain.DTOs
{
    public class RequestDetailDto
    {
        public int RequestId { get; set; }
        public string Code { get; set; }
        public string Title { get; set; }
        public DateTime RequestDate { get; set; }
        public string Status { get; set; }
        public string RequesterName { get; set; }
        public int CurrentStepId { get; set; }

        public string VendorCode { get; set; }
        public string VendorName { get; set; }
        public string? SourceSystem { get; set; }
        public string? SourceCode { get; set; }

        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string? Remark { get; set; }

        public PermissionDto Permissions { get; set; } = new();

        public WorkflowRouteDetailDto WorkflowRoute { get; set; }

        public List<QuotationItemDto> Quotations { get; set; }
    }

 

   
}