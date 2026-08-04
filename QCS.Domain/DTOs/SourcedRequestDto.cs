namespace QCS.Domain.DTOs
{
    public sealed class SourcedDocumentDto
    {
        public int Id { get; init; }
        public string FileName { get; init; } = string.Empty;
        public int DocumentTypeId { get; init; }
        public string DocumentTypeName { get; init; } = string.Empty;
    }

    public sealed class SourcedRequestDto
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? VendorCode { get; init; }
        public string? VendorName { get; init; }
        public DateTime? RequestDate { get; init; }
        public int Status { get; init; }
        public string StatusName { get; init; } = string.Empty;
        public int? CurrentStepId { get; init; }
        public string? CurrentStepName { get; init; }
        public string RequesterNId { get; init; } = string.Empty;
        public string RequesterName { get; init; } = string.Empty;
        public DateTime? ValidFrom { get; init; }
        public DateTime? ValidUntil { get; init; }
        public string? Remark { get; init; }
        public IReadOnlyList<SourcedDocumentDto> Documents { get; init; } = [];
    }
}