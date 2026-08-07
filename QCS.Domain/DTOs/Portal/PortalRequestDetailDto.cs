using QCS.Domain.Enum;
using System;
using System.Collections.Generic;

namespace QCS.Domain.DTOs.Portal
{
    public class PortalRequestDetailDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime RequestDate { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string RequesterNId { get; set; } = string.Empty;
        public string RequesterName { get; set; } = string.Empty;
        public string VendorCode { get; set; } = string.Empty;
        public string VendorName { get; set; } = string.Empty;
        public string? SourceSystem { get; set; }
        public string? SourceCode { get; set; }
        public RequestIntent Intent { get; set; }
        public string IntentName { get; set; } = string.Empty;
        public int? RenewedFromRequestId { get; set; }
        public string? RenewedFromCode { get; set; }
        public string OriginName { get; set; } = string.Empty;
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string? Remark { get; set; }
        public Guid? ApprovalDocumentId { get; set; }
        public string? ApprovalDocumentNumber { get; set; }
        public int? CurrentStepSequence { get; set; }
        public int CurrentStepId { get; set; }
        public string? CurrentStepName { get; set; }
        public bool CanRenew { get; set; }
        public PermissionDto Permissions { get; set; } = new();
        public List<PortalWorkflowStepDto> WorkflowSteps { get; set; } = new();
        public List<PortalDocumentDto> Documents { get; set; } = new();
        public List<PortalHistoryDto> Histories { get; set; } = new();
    }
}
