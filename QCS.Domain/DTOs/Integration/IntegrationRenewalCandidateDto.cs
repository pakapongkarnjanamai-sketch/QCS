namespace QCS.Domain.DTOs.Integration
{
    public sealed class IntegrationRenewalCandidateDto
    {
        public string Code { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string VendorCode { get; init; } = string.Empty;
        public string VendorName { get; init; } = string.Empty;
        public DateTime? ValidUntil { get; init; }
        public string RenewalWindowStatus { get; init; } = string.Empty;
    }
}
