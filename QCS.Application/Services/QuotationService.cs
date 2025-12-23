using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QCS.Domain.DTOs;
using QCS.Domain.Models;
using QCS.Infrastructure.Services;
using System.Text;
using System.Text.Json;

namespace QCS.Application.Services
{
    public interface IQuotationService
    {
        //IQueryable<RequestGridDto> GetGridQuery(string code = null);
        //Task<AttachmentResultDto?> GetAttachmentAsync(int id);
        Task<AttachmentResultDto> GenerateStampedPdfAsync(int requestId);
    }

    public class QuotationService : IQuotationService
    {
        // ✅ 1. เปลี่ยนมาใช้ UnitOfWork แทน Repository แยก
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _env;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public QuotationService(
            IUnitOfWork unitOfWork, // ✅ Inject เข้ามาแทน
            IWebHostEnvironment env,
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _env = env;
            _httpClient = httpClient;
            _configuration = configuration;
        }

        //public IQueryable<RequestGridDto> GetGridQuery(string code = null)
        //{
        //    var query = _unitOfWork.Repository<Request>().GetAll()
        //        .AsNoTracking();

        //    // ย้าย Logic การ Filter มาไว้ที่นี่
        //    if (!string.IsNullOrEmpty(code))
        //    {
        //        query = query.Where(x => x.Code == code);
        //    }

        //    // ทำ Projection เป็น DTO (Copy logic มาจาก RequestService เพื่อความ Consistent)
        //    return query.Select(r => new RequestGridDto
        //    {
        //        Id = r.Id,
        //        Code = r.Code,
        //        Title = r.Title,
        //        VendorCode = r.VendorCode,
        //        VendorName = r.VendorName,
        //        RequestDate = r.RequestDate,
        //        CurrentStepId = r.CurrentStepId,
        //        // เพิ่ม Field อื่นๆ ที่ RequestService มีถ้าจำเป็น
        //        RequesterName = r.ApprovalSteps
        //                    .Where(s => s.Sequence == 1)
        //                    .Select(s => s.ApproverName)
        //                    .FirstOrDefault() ?? "Unknown"
        //    });



        //}

        //public async Task<AttachmentResultDto?> GetAttachmentAsync(int fileId)
        //{
        //    // ✅ เรียกผ่าน UnitOfWork
        //    var q = await _unitOfWork.Repository<Quotation>().GetAll()
        //        .Include(x => x.AttachmentFile)
        //        .FirstOrDefaultAsync(x => x.Id == fileId);

        //    if (q == null) return null;

        //    if (q.AttachmentFile?.Data != null)
        //    {
        //        return new AttachmentResultDto
        //        {
        //            Data = q.AttachmentFile.Data,
        //            ContentType = q.AttachmentFile.ContentType ?? "application/octet-stream",
        //            FileName = q.FileName
        //        };
        //    }

        //    if (!string.IsNullOrEmpty(q.FilePath) && q.FilePath != "Database")
        //    {
        //        var path = Path.Combine(_env.WebRootPath, q.FilePath);
        //        if (System.IO.File.Exists(path))
        //        {
        //            return new AttachmentResultDto
        //            {
        //                Data = await System.IO.File.ReadAllBytesAsync(path),
        //                ContentType = q.ContentType ?? "application/octet-stream",
        //                FileName = q.FileName
        //            };
        //        }
        //    }

        //    return null;
        //}

        public async Task<AttachmentResultDto> GenerateStampedPdfAsync(int requestId)
        {
            // ✅ เรียกผ่าน UnitOfWork
            var request = await _unitOfWork.Repository<Request>().GetAll()
                .Include(r => r.Quotations).ThenInclude(q => q.AttachmentFile)
                .Include(r => r.ApprovalSteps)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null) throw new KeyNotFoundException("Request not found");

            var pdfRequest = new MergeAndStampRequestDto
            {
                DocumentName = request.Title,
                ReferenceCode = request.Code,
                PdfFiles = request.Quotations
                    .Where(q => q.AttachmentFile != null)
                    .Select(q => new PdfFileDto
                    {
                        Name = q.FileName,
                        DocumentType = MapDocumentType(q.DocumentTypeId),
                        ContentType = q.ContentType ?? "application/pdf",
                        Data = q.AttachmentFile.Data,
                        Length = q.FileSize
                    }).ToList(),
                ApprovalData = new ApprovalDataDto
                {
                    Name = request.VendorName,
                    Step = request.ApprovalSteps
                        .Where(s => s.Status == (int)QCS.Domain.Enum.RequestStatus.Approved)
                        .OrderBy(s => s.Sequence)
                        .Select(s => new StepDto
                        {
                            StepName = s.StepName,
                            Approver = s.ApproverName ?? s.ApproverNId ?? "Unknown",
                            ApprovalDate = s.ActionDate ?? DateTime.Now
                        }).ToList()
                },
                DrawSetting = new DrawSettingDto { Color = "#000000", FontSize = 8 }
            };

            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var jsonContent = new StringContent(JsonSerializer.Serialize(pdfRequest, jsonOptions), Encoding.UTF8, "application/json");

            var pdfServiceUrl = _configuration["ExternalServices:PdfServiceUrl"];
            var response = await _httpClient.PostAsync($"{pdfServiceUrl}/api/Pdf/merge-stamp", jsonContent);

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                throw new Exception($"PDF Service Error ({response.StatusCode}): {errorMsg}");
            }

            var fileBytes = await response.Content.ReadAsByteArrayAsync();

            return new AttachmentResultDto
            {
                Data = fileBytes,
                ContentType = "application/pdf",
                FileName = $"Approved_{request.Code}.pdf"
            };
        }

        private string MapDocumentType(int typeId)
        {
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