using System.ComponentModel.DataAnnotations.Schema;

namespace QCS.Domain.Models
{
    public class Quotation : BaseEntity
    {
        public int PurchaseRequestId { get; set; }
        [ForeignKey("PurchaseRequestId")]
        public virtual PurchaseRequest PurchaseRequest { get; set; }

        public string FileName { get; set; } // Map to 'originalFileName'
        public string FilePath { get; set; } // เก็บ Path จริงใน Server/Blob
        public string ContentType { get; set; } // เช่น application/pdf
        public long FileSize { get; set; }

        public int DocumentTypeId { get; set; } // 10, 20, 30
    }
}