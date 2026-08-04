using System;
using System.Collections.Generic;

namespace QCS.Domain.DTOs.Portal
{
    public class PortalWorkflowStepDto
    {
        public int Id { get; set; }
        public int SequenceNo { get; set; }
        public string StepName { get; set; } = string.Empty;
        public int? Status { get; set; }
        public string? StatusName { get; set; }
        public DateTime? ActionDate { get; set; }
        public string? ApproverNId { get; set; }
        public string? ApproverName { get; set; }
        public string? Comment { get; set; }
        public List<PortalAssignmentDto> Assignments { get; set; } = new();
    }

    public class PortalAssignmentDto
    {
        public string NId { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string AssignmentType { get; set; } = string.Empty;
        public bool IsCurrentUser { get; set; }
    }
}
