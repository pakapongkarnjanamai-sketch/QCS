using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace QCS.Domain.DTOs
{
    public class CreateRequestDto : IHasAttachments
    {
        [Required]
        public string Title { get; set; }
        public string VendorName { get; set; }
        public string? VendorCode { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string? Remark { get; set; }
        public string? Comment { get; set; }
        // รับไฟล์แนบจริง (Binary) จาก FormData
        public List<IFormFile> Attachments { get; set; }
        public string QuotationsJson { get; set; }
        public List<IFormFile>? GetUploadFiles() => Attachments;
    }
}