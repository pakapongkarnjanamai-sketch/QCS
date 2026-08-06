namespace QCS.Domain.DTOs.Portal
{
    public class PortalDocumentDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public int DocumentTypeId { get; set; }
        public string DocumentTypeName { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string? ReferenceCode { get; set; }
        public long FileSize { get; set; }
        public string ViewUrl { get; set; } = string.Empty;
    }
}
