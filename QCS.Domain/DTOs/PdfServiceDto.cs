using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace QCS.Domain.DTOs
{
    // โครงสร้าง Request หลัก
    public class MergeAndStampRequestDto
    {
        public List<PdfFileDto> PdfFiles { get; set; } = new();
        public string DocumentName { get; set; }
        public string ReferenceCode { get; set; }
        public ApprovalDataDto ApprovalData { get; set; }
        public DrawSettingDto DrawSetting { get; set; }
    }

    public class PdfFileDto
    {
        public string Name { get; set; }
        public int DocumentTypeId { get; set; } // "Quotation", "Spec", etc.
        public string ContentType { get; set; } = "application/pdf";
        public byte[] Data { get; set; }
        public long Length { get; set; }
    }

    public class ApprovalDataDto
    {
        public string Name { get; set; } // ชื่อเอกสารหรือชื่อโปรเจคที่จะแสดงบนหัวกระดาษ (ถ้ามี)
        public List<StepDto> Step { get; set; } = new();
    }

    public class StepDto
    {
        public string StepName { get; set; }
        public string Approver { get; set; }
        public DateTime ApprovalDate { get; set; }
    }

    public class DrawSettingDto
    {
        public string Color { get; set; } = "#000000";
        public float FontSize { get; set; } = 10f;
        public float Margin { get; set; } = 20f;
        // 0=TopLeft, 1=TopCenter, 2=TopRight, 3=MiddleLeft, 4=MiddleCenter, 5=MiddleRight, 6=BottomLeft, 7=BottomCenter, 8=BottomRight
        public int AlignmentStamp { get; set; } = 8;
    }

    public class PreviewMergeStampRequestDto
    {
        public int? RequestId { get; set; }
        public string? DocumentName { get; set; }
        public string? ReferenceCode { get; set; }
        public string QuotationsJson { get; set; } = "[]";
        public List<IFormFile>? NewAttachments { get; set; }
    }
}
