using System;

namespace QCS.Domain.DTOs.Portal
{
    public class PortalHistoryDto
    {
        public int SequenceNo { get; set; }
        public string StepName { get; set; } = string.Empty;
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string? ApproverNId { get; set; }
        public string? ApproverName { get; set; }
        public DateTime? ActionDate { get; set; }
        public string? Comment { get; set; }
    }
}
