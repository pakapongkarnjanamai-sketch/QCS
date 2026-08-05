using Microsoft.AspNetCore.Http;

namespace QCS.Domain.DTOs.Portal
{
    public class UploadPortalAttachmentDto
    {
        public IFormFile File { get; set; } = null!;

        /// <summary>
        /// Required. Deliberately nullable with no default: defaulting to Original Quotation let a
        /// caller that never chose a type satisfy the submit rule, which requires exactly that type.
        /// </summary>
        public int? DocumentTypeId { get; set; }
    }
}
