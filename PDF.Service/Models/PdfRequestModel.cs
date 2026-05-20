namespace PDF.Service.Models
{
    public class MergePdfRequest
    {
        public List<PdfFile> PdfFiles { get; set; } = new();
        public string DocumentName { get; set; } = string.Empty;
    }

    public class StampPdfRequest
    {
        public PdfFile PdfFile { get; set; } = new();
        public ApprovalData ApprovalData { get; set; } = new();
        public DrawSetting DrawSetting { get; set; } = new();
    }
    public class MergeAndStampRequest
    {
        public List<PdfFile> PdfFiles { get; set; } = new();
        public string DocumentName { get; set; } = string.Empty;
        public string ReferenceCode { get; set; } = string.Empty;
        public ApprovalData ApprovalData { get; set; } = new();
        public DrawSetting DrawSetting { get; set; } = new();
    }

    public class PdfFile
    {
        public string Name { get; set; } = string.Empty;
        public int DocumentTypeId { get; set; }
        public string ContentType { get; set; } = "application/pdf";
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public long Length { get; set; }
    }

    public class ApprovalData
    {
        public string Name { get; set; } = string.Empty;
        public List<Step> Step { get; set; } = new();
    }

    public class Step
    {
        public string StepName { get; set; } = string.Empty;
        public string Approver { get; set; } = string.Empty;
        public DateTime ApprovalDate { get; set; }
    }

    public class DrawSetting
    {
        public string Color { get; set; } = "#000000";
        public float FontSize { get; set; } = 12f;
        public float Margin { get; set; } = 20f;
        public AlignmentStamp AlignmentStamp { get; set; } = AlignmentStamp.TopRight;
    }

    public enum AlignmentStamp
    {
        TopLeft,
        TopCenter,
        TopRight,
        CenterLeft,
        Center,
        CenterRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

}
