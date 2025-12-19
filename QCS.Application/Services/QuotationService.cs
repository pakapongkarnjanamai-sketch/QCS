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
        IQueryable<Request> GetQueryable();
        Task<AttachmentResultDto?> GetAttachmentAsync(int id);
        Task<AttachmentResultDto> GenerateStampedPdfAsync(int requestId);
    }

    public class QuotationService : IQuotationService
    {
        private readonly IRepository<Request> _requestRepository;
        private readonly IRepository<Quotation> _quotationRepository;
        private readonly IWebHostEnvironment _env;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public QuotationService(
            IRepository<Request> requestRepository,
            IRepository<Quotation> quotationRepository,
            IWebHostEnvironment env,
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _requestRepository = requestRepository;
            _quotationRepository = quotationRepository;
            _env = env;
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public IQueryable<Request> GetQueryable()
        {
            return _requestRepository.GetAll()
                .Include(x => x.Quotations)
                .Include(x => x.ApprovalSteps)
                .AsNoTracking();
        }

        public async Task<AttachmentResultDto?> GetAttachmentAsync(int fileId)
        {
            var q = await _quotationRepository.GetAll()
                .Include(x => x.AttachmentFile)
                .FirstOrDefaultAsync(x => x.Id == fileId);

            if (q == null) return null;

            if (q.AttachmentFile?.Data != null)
            {
                return new AttachmentResultDto
                {
                    Data = q.AttachmentFile.Data,
                    ContentType = q.AttachmentFile.ContentType ?? "application/octet-stream",
                    FileName = q.FileName
                };
            }

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

        public async Task<AttachmentResultDto> GenerateStampedPdfAsync(int requestId)
        {
            var request = await _requestRepository.GetAll()
                .Include(r => r.Quotations).ThenInclude(q => q.AttachmentFile)
                .Include(r => r.ApprovalSteps)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null) throw new KeyNotFoundException("Request not found");

            // ✅ สร้าง Request DTO ตามโครงสร้าง MergeAndStampRequestDto
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