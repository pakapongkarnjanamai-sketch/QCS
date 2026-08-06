using System.Collections.Generic;

namespace QCS.Domain.DTOs.Portal
{
    public class UpdatePortalDocumentsDto
    {
        public List<PortalDocumentUpdateDto> Documents { get; set; } = new();
    }

    public class PortalDocumentUpdateDto
    {
        public int Id { get; set; }
        public int DocumentTypeId { get; set; }
    }
}