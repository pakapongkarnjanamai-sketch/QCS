namespace QCS.Domain.DTOs.Portal
{
    public sealed class PortalSetupResolutionDto
    {
        public string Flow { get; init; } = string.Empty;
        public int Intent { get; init; }
        public string Origin { get; init; } = string.Empty;
        public string? SourceCode { get; init; }
        public string? SourceTitle { get; init; }
        public int? RenewedFromRequestId { get; init; }
        public string? RenewedFromCode { get; init; }
        public string VendorCode { get; init; } = string.Empty;
        public string VendorName { get; init; } = string.Empty;
    }
}
