using PDF.Service.Models;

namespace PDF.Service.Interface
{
    public interface IPdfGeneratorService
    {
        PdfFile Merge(List<PdfFile> pdfFiles, string documentName);

        PdfFile Stamp(PdfFile pdfFile, ApprovalData approvalData, DrawSetting drawSetting, string referenceCode = "");
    }
}