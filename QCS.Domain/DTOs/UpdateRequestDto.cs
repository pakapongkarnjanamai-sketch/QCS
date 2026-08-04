using Microsoft.AspNetCore.Http;

namespace QCS.Domain.DTOs
{
    public class UpdateRequestDto : IHasAttachments
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string VendorCode { get; set; }
        public string VendorName { get; set; }
        public string? SourceSystem { get; set; }
        public string? SourceCode { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string? Remark { get; set; }
        public string? Comment { get; set; }
        public List<IFormFile>? NewAttachments { get; set; }
        public string? DeletedFileIds { get; set; }
        public string? QuotationsJson { get; set; }
        public string? UpdatedQuotationsJson { get; set; }
        public List<IFormFile>? GetUploadFiles() => NewAttachments;
    }
}