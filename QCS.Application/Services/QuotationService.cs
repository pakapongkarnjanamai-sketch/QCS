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
        private readonly IDateTime _dateTime;
        private readonly IWebHostEnvironment _env;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public QuotationService(
            IUnitOfWork unitOfWork, // ✅ Inject เข้ามาแทน
            IDateTime dateTime,
            IWebHostEnvironment env,
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _dateTime = dateTime;
            _env = env;
            _httpClient = httpClient;
            _configuration = configuration;
        }

     

        public async Task<AttachmentResultDto> GenerateStampedPdfAsync(int requestId)
        {
            // ✅ เรียกผ่าน UnitOfWork
            var request = await _unitOfWork.Repository<Request>().GetAll()
                .Include(r => r.Quotations).ThenInclude(q => q.AttachmentFile)
                .Include(r => r.ApprovalSteps)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null) throw new KeyNotFoundException("Request not found");

            var fallbackApprovalDate = _dateTime.Now;

            var pdfRequest = new MergeAndStampRequestDto
            {
                DocumentName = request.Code +"_"+ request.Title,
                ReferenceCode = request.Code,
                PdfFiles = request.Quotations
                    .Where(q => q.AttachmentFile != null)
                    .Select(q => new PdfFileDto
                    {
                        Name = q.FileName,
                        DocumentTypeId = q.DocumentTypeId,
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
                            ApprovalDate = s.ActionDate ?? fallbackApprovalDate
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

    }
}