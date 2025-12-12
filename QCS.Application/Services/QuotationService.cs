using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QCS.Domain.DTOs;

using QCS.Domain.Models;
using QCS.Infrastructure.Data;
using System.Text;
using System.Text.Json;

namespace QCS.Application.Services
{
    public interface IQuotationService
    {
        IQueryable<PurchaseRequest> GetQueryable();
        Task<AttachmentResultDto?> GetAttachmentAsync(int id);
        Task<AttachmentResultDto> GenerateStampedPdfAsync(int purchaseRequestId);
    }

    public class QuotationService : IQuotationService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public QuotationService(
            AppDbContext context,
            IWebHostEnvironment env,
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _context = context;
            _env = env;
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public IQueryable<PurchaseRequest> GetQueryable()
        {
            return _context.PurchaseRequests
                .Include(x => x.Quotations)
                .Include(x => x.ApprovalSteps)
                .AsNoTracking();
        }

        public async Task<AttachmentResultDto?> GetAttachmentAsync(int id)
        {
            var q = await _context.Quotations
                .Include(x => x.AttachmentFile)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (q == null) return null;

            // 1. Try DB
            if (q.AttachmentFile?.Data != null)
            {
                return new AttachmentResultDto
                {
                    Data = q.AttachmentFile.Data,
                    ContentType = q.AttachmentFile.ContentType ?? "application/octet-stream",
                    FileName = q.FileName
                };
            }

            // 2. Try Disk (Legacy Support)
            if (!string.IsNullOrEmpty(q.FilePath) && q.FilePath != "Database")
            {
                var path = Path.Combine(_env.WebRootPath, q.FilePath);
                if (System.IO.File.Exists(path))
                {
                    return new AttachmentResultDto
                    {
                        Data = await System.IO.File.ReadAllBytesAsync(path),
                        ContentType = q.ContentType ?? "application/octet-stream",
                        FileName = q.FileName
                    };
                }
            }

            return null;
        }

        // ==========================================================
        // 📄 GENERATE STAMPED PDF (Call External API)
        // ==========================================================
        public async Task<AttachmentResultDto> GenerateStampedPdfAsync(int purchaseRequestId)
        {
            // 1. ดึงข้อมูล Request (PR) พร้อม Vendor, Quotations และ ApprovalSteps
            var request = await _context.PurchaseRequests
                         // ต้องการชื่อ Vendor ไปแสดง
                .Include(x => x.Quotations)
                .ThenInclude(q => q.AttachmentFile) // ดึงไฟล์ PDF
                .Include(x => x.ApprovalSteps)      // ดึงข้อมูลการอนุมัติ
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == purchaseRequestId);

            if (request == null)
                throw new Exception("Purchase Request not found.");

            // Validation: ต้องอนุมัติแล้วเท่านั้น (เปิดใช้งานเมื่อระบบ Flow สมบูรณ์)
            // if (request.Status != RequestStatus.Approved) 
            //    throw new Exception("Document is not fully approved.");

            // Validation: ต้องมีไฟล์แนบ
            var quotations = request.Quotations.Where(q => q.AttachmentFile != null && q.AttachmentFile.Data != null).ToList();
            if (!quotations.Any())
                throw new Exception("No valid PDF files found in quotations.");

            // 2. เตรียมข้อมูล DTO สำหรับส่งไป PDF Service
            var pdfRequest = new MergeAndStampRequestDto
            {
                DocumentName = request.Code,
                VendorName = request.VendorName ?? "Unknown Vendor",

                // ตั้งค่าการ Stamp (สี, ตำแหน่ง, ขนาดฟอนต์)
                DrawSetting = new DrawSettingDto
                {
                    Color = "#000000",      // สีดำ
                    AlignmentStamp = 8,     // 8 = BottomRight (อิงตาม Enum ของ PDF Service)
                    FontSize = 10,
                    Margin = 20
                },

                // ข้อมูลลำดับการอนุมัติ
                ApprovalData = new ApprovalDataDto
                {
                    Name = $"PR Ref: {request.Code}",
                    Step = request.ApprovalSteps
                        .OrderBy(s => s.Sequence)
                        .Select(s => new StepDto
                        {
                            StepName = $"Step {s.StepName}", // หรือใช้ s.RoleName
                            Approver = s.ApproverName ?? "System Admin", // ชื่อคนอนุมัติ
                            ApprovalDate = s.ActionDate ?? DateTime.Now
                        }).ToList()
                },

                // รายการไฟล์ PDF ที่จะนำมารวม
                PdfFiles = quotations.Select(q => new PdfFileDto
                {
                    Name = q.FileName,
                    ContentType = "application/pdf",
                    DocumentType = MapDocumentType(q.DocumentTypeId), // แปลง ID เป็นชื่อประเภทเอกสาร
                    Data = q.AttachmentFile.Data,
                    Length = q.AttachmentFile.Data.Length
                }).ToList()
            };

            // 3. เรียก API ภายนอก (PDF Service)
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = null }; // ส่ง JSON แบบ PascalCase ตาม C#
            var jsonContent = new StringContent(JsonSerializer.Serialize(pdfRequest, jsonOptions), Encoding.UTF8, "application/json");

            // ดึง URL จาก appsettings.json (Key: PdfServiceUrl) -> "http://localhost:5226"
            var pdfServiceUrl = _configuration["PdfServiceUrl"] ?? "http://localhost:5226";
            var response = await _httpClient.PostAsync($"{pdfServiceUrl}/api/Pdf/merge-stamp", jsonContent);

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                throw new Exception($"PDF Service Error ({response.StatusCode}): {errorMsg}");
            }

            // 4. รับไฟล์ PDF ที่ประมวลผลเสร็จแล้วกลับมา
            var fileBytes = await response.Content.ReadAsByteArrayAsync();

            return new AttachmentResultDto
            {
                Data = fileBytes,
                ContentType = "application/pdf",
                FileName = $"Approved_{request.Code}.pdf"
            };
        }

        // Helper: แปลง DocumentTypeId เป็น String (ปรับตาม Enum DocumentType ของคุณ)
        private string MapDocumentType(int typeId)
        {
            // อ้างอิง Enum: 10=Quotation, 20=Comparison, 30=Specs (ตัวอย่าง)
            return typeId switch
            {
                10 => "Main Quotation",
                20 => "Comparison Sheet",
                30 => "Specification",
                _ => "Attachment"
            };
        }
    }
}