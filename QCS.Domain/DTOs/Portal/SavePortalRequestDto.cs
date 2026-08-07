using QCS.Domain.Enum;
using System;

namespace QCS.Domain.DTOs.Portal
{
    public class SavePortalRequestDto
    {
        public RequestIntent Intent { get; set; } = RequestIntent.New;
        public int? RenewedFromRequestId { get; set; }
        public string? Title { get; set; }
        public string? VendorCode { get; set; }
        public string? VendorName { get; set; }
        public string? SourceSystem { get; set; }
        public string? SourceCode { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string? Remark { get; set; }
    }
}
