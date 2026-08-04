using Microsoft.AspNetCore.Http;

namespace QCS.Domain.DTOs.Portal
{
    public class UploadPortalAttachmentDto
    {
        public IFormFile File { get; set; } = null!;
        public int DocumentTypeId { get; set; } = 10;
    }
}
