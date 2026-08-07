using System;

namespace QCS.Domain.DTOs.Portal
{
    public class RenewalCandidateDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string VendorCode { get; set; } = string.Empty;
        public string VendorName { get; set; } = string.Empty;
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string? SourceSystem { get; set; }
        public string? SourceCode { get; set; }
        public DateTime RequestDate { get; set; }
        public int OriginalQuotationCount { get; set; }
        public string RenewalWindowStatus { get; set; } = string.Empty;
    }
}
