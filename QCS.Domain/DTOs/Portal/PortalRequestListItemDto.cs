using System;

namespace QCS.Domain.DTOs.Portal
{
    public class PortalRequestListItemDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string VendorCode { get; set; } = string.Empty;
        public string VendorName { get; set; } = string.Empty;
        public DateTime RequestDate { get; set; }
        public int CurrentStepId { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string RequesterName { get; set; } = string.Empty;
        public string RequesterNId { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
    }
}
