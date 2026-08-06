using System.ComponentModel.DataAnnotations.Schema;

namespace QCS.Domain.Models
{
    public class Quotation : BaseEntity
    {
        public int RequestId { get; set; }
        [ForeignKey("RequestId")]
        public virtual Request Request { get; set; }

        public string FileName { get; set; } // Map to 'originalFileName'
        public string FilePath { get; set; } // เก็บ Path จริงใน Server/Blob
        public string ContentType { get; set; } // เช่น application/pdf
        public long FileSize { get; set; }

        public int DocumentTypeId { get; set; } // 10, 20, 30
        public int SortOrder { get; set; }

        public int? AttachmentFileId { get; set; }

        [ForeignKey("AttachmentFileId")]
        public virtual AttachmentFile AttachmentFile { get; set; }
    }
}