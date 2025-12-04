// File: QCS.Domain/Models/AttachmentFile.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QCS.Domain.Models
{
    public class AttachmentFile : BaseEntity
    {
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; }

        [StringLength(100)]
        public string ContentType { get; set; } // เช่น application/pdf

        public long FileSize { get; set; }

        // เก็บข้อมูลไฟล์ในรูปแบบ Binary
        public byte[] Data { get; set; }
    }
}