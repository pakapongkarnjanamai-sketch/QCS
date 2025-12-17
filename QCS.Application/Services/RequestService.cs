
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using QCS.Domain.DTOs;
using QCS.Domain.Enum;
using QCS.Domain.Models;
using QCS.Infrastructure.Data;
using System.Security.Cryptography;
using System.Text.Json;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace QCS.Application.Services
{
   
        public interface IRequestService
        {
            // 1. ส่งเป็น IQueryable เพื่อให้ Controller เอาไปใส่ DataSourceLoader ได้
            IQueryable<Request> GetMyRequestsQuery();

            // 2. สำหรับงานที่ต้องอนุมัติ (Logic ซับซ้อน)
            Task<IQueryable<Request>> GetMyTasksQueryAsync();

            // 3. สำหรับรายการที่อนุมัติแล้ว
            IQueryable<Request> GetApprovedListQuery();

            // ส่วน CRUD เดิมคงไว้
            Task<PurchaseRequestDetailDto?> GetByCodeAsync(string code);
            Task<PurchaseRequestDetailDto?> GetByIdAsync(int id);
            Task<Request> CreateAsync(CreatePurchaseRequestDto input, bool isSubmit);
            Task UpdateAsync(UpdatePurchaseRequestDto input, bool isSubmit);
            Task DeleteAsync(int id);
            Task<AttachmentResultDto?> GetAttachmentAsync(int id);
        }

    public class RequestService : IRequestService
    {
        private readonly AppDbContext _context;
        private readonly WorkflowService _workflowService;
        private readonly ICurrentUserService _currentUserService;

        public RequestService(AppDbContext context, WorkflowService workflowService, ICurrentUserService currentUserService)
        {
            _context = context;
            _workflowService = workflowService;
            _currentUserService = currentUserService;
        }

        // ==========================================================
        // ⚡ OPTIMIZED QUERIES (IQueryable)
        // ==========================================================

        public IQueryable<Request> GetMyRequestsQuery()
        {
            return _context.Requests
                .AsNoTracking()
                .Where(r => r.CreatedBy == _currentUserService.UserId && r.Status != (int)RequestStatus.Approved)
                .OrderByDescending(r => r.CreatedAt);
        }

        public IQueryable<Request> GetApprovedListQuery()
        {
            return _context.Requests
                .AsNoTracking()
                .Where(r => r.Status == (int)RequestStatus.Approved)
                .OrderByDescending(r => r.UpdatedAt);
        }

        public async Task<IQueryable<Request>> GetMyTasksQueryAsync()
        {
            var routeData = await _workflowService.GetWorkflowRouteDetailAsync(1);
            var myStepSequences = new List<int>();

            if (routeData?.Steps != null)
            {
                myStepSequences = routeData.Steps
                    .Where(s => s.Assignments != null && s.Assignments.Any(a => a.NId == _currentUserService.UserId))
                    .Select(s => s.SequenceNo)
                    .ToList();
            }

            if (!myStepSequences.Any())
            {
                return _context.Requests.AsNoTracking().Where(r => false);
            }

            return _context.Requests
                .AsNoTracking()
                .Where(r => r.Status == (int)RequestStatus.Pending &&
                            myStepSequences.Contains(r.CurrentStepId))
                .OrderBy(r => r.CreatedAt);
        }

        // ==========================================================
        // 🔍 GET DETAILS (Mapped to PurchaseRequestDetailDto)
        // ==========================================================

        public async Task<PurchaseRequestDetailDto?> GetByIdAsync(int id)
        {
            var request = await _context.Requests
                .AsNoTracking()
                .Include(r => r.Quotations)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return null;

            return await MapToDetailDto(request);
        }

        public async Task<PurchaseRequestDetailDto?> GetByCodeAsync(string code)
        {
            var request = await _context.Requests
                .AsNoTracking()
                .Include(r => r.Quotations)
                .FirstOrDefaultAsync(r => r.Code == code); // Fix: DocumentCode -> Code

            if (request == null) return null;

            return await MapToDetailDto(request);
        }

        private async Task<PurchaseRequestDetailDto> MapToDetailDto(Request r)
        {
            // Fix: Map fields according to PurchaseRequestDetailDto definition
            return new PurchaseRequestDetailDto
            {
                PurchaseRequestId = r.Id,      // Fix: Id -> PurchaseRequestId
                DocumentNo = r.Code,           // Fix: Code -> DocumentNo
                Title = r.Title,               // Fix: Subject -> Title
                                               // Amount = r.Amount,          // Removed: No Amount in Entity/DTO
                                               // Description = r.Description,// Removed: No Description in Entity/DTO

                VendorId = r.VendorId,
                VendorName = r.VendorName,
                ValidFrom = r.ValidFrom,
                ValidUntil = r.ValidUntil,
                Remark = r.Remark,

                Status = ((RequestStatus)r.Status).ToString(), // Fix: Convert int to string
                CurrentStepId = r.CurrentStepId,

                // RequesterName logic (Assuming CreatedBy is NId, might need lookup)
                RequesterName = r.CreatedBy,
                RequestDate = r.RequestDate, // Fix: CreatedAt -> RequestDate

                Quotations = r.Quotations.Select(q => new QuotationItemDto
                {
                    Id = q.Id,
                    FileName = q.FileName,
                    DocumentTypeId = q.DocumentTypeId
                }).ToList()
            };
        }

        // ==========================================================
        // 📝 CRUD OPERATIONS
        // ==========================================================

        public async Task<Request> CreateAsync(CreatePurchaseRequestDto input,  bool isSubmit)
        {
            string newCode = await GenerateDocumentCodeAsync();

            var request = new Request
            {
                Code = newCode,                // Fix: DocumentCode -> Code
                Title = input.Title,           // Fix: Subject -> Title
                                               // Amount = input.Amount,      // Removed
                                               // Description = input.Description, // Removed

                VendorId = input.VendorId,
                VendorName = input.VendorName,
                ValidFrom = input.ValidFrom,
                ValidUntil = input.ValidUntil,
                Remark = input.Remark,
                // Comment = input.Comment,    // Note: Comment is in DTO but not in Request entity directly? ignoring for now or map to log

                RequestDate = DateTime.Now,    // Fix: Set RequestDate
          
                Status = isSubmit ? (int)RequestStatus.Pending : (int)RequestStatus.Draft,
                CurrentStepId = isSubmit ? 1 : 0
            };

            // Fix: Attachments -> Attachments, MetaJson -> QuotationsJson
            if (input.Attachments != null && input.Attachments.Count > 0)
            {
                await ProcessAttachmentsAsync(request, input.Attachments, input.QuotationsJson);
            }

            _context.Requests.Add(request);
            await _context.SaveChangesAsync();

            return request;
        }

        public async Task UpdateAsync(UpdatePurchaseRequestDto input, bool isSubmit)
        {
            var request = await _context.Requests
                .Include(r => r.Quotations)
                .FirstOrDefaultAsync(r => r.Id == input.Id);

            if (request == null) throw new Exception("Request not found");

            // Update Fields
            request.Title = input.Title;       // Fix: Subject -> Title
                                               // request.Amount = input.Amount; // Removed
                                               // request.Description = input.Description; // Removed

            request.VendorId = input.VendorId;
            request.VendorName = input.VendorName;
            request.ValidFrom = input.ValidFrom;
            request.ValidUntil = input.ValidUntil;
            request.Remark = input.Remark;

            request.UpdatedAt = DateTime.Now;

            if (isSubmit)
            {
                request.Status = (int)RequestStatus.Pending;
                request.CurrentStepId = 1;
            }

            // Fix: NewAttachments -> NewAttachments, QuotationsJson -> QuotationsJson
            if (input.NewAttachments != null && input.NewAttachments.Count > 0)
            {
                await ProcessAttachmentsAsync(request, input.NewAttachments, input.QuotationsJson);
            }

            // Handle Deleted Files if needed (input.DeletedFileIds)
            // ...

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var request = await _context.Requests.FindAsync(id);
            if (request != null)
            {
                _context.Requests.Remove(request);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<AttachmentResultDto?> GetAttachmentAsync(int id)
        {
            var q = await _context.Quotations
                .AsNoTracking()
                .Include(x => x.AttachmentFile)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (q?.AttachmentFile == null) return null;

            return new AttachmentResultDto
            {
                FileName = q.FileName,
                ContentType = q.ContentType,
                Data = q.AttachmentFile.Data
            };
        }

        // ==========================================================
        // 🛠 PRIVATE HELPERS
        // ==========================================================

        private async Task<string> GenerateDocumentCodeAsync()
        {
            var prefix = $"PR-{DateTime.Now:yyyyMM}-";
            var lastRequest = await _context.Requests
                .Where(r => r.Code.StartsWith(prefix)) // Fix: DocumentCode -> Code
                .OrderByDescending(r => r.Code)
                .FirstOrDefaultAsync();

            int runningNo = 1;
            if (lastRequest != null)
            {
                var parts = lastRequest.Code.Split('-');
                if (parts.Length >= 3 && int.TryParse(parts.Last(), out int lastNo))
                {
                    runningNo = lastNo + 1;
                }
            }

            return $"{prefix}{runningNo:D3}";
        }

        private async Task ProcessAttachmentsAsync(Request pr, List<IFormFile> files, string? jsonMeta)
        {
            var metaList = string.IsNullOrEmpty(jsonMeta)
                ? new List<QuotationItemDto>()
                : JsonSerializer.Deserialize<List<QuotationItemDto>>(jsonMeta, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (pr.Quotations == null) pr.Quotations = new List<Quotation>();

            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    byte[] fileData;
                    using (var ms = new MemoryStream())
                    {
                        await file.CopyToAsync(ms);
                        fileData = ms.ToArray();
                    }

                    var attachment = new AttachmentFile
                    {
                        FileName = file.FileName,
                        ContentType = file.ContentType,
                        FileSize = file.Length,
                        Data = fileData,
                        CreatedAt = DateTime.Now
                    };

                    var meta = metaList?.FirstOrDefault(m => m.FileName == file.FileName);
                    int typeId = meta != null ? meta.DocumentTypeId : 10;

                    pr.Quotations.Add(new Quotation
                    {
                        FileName = file.FileName,
                        ContentType = file.ContentType,
                        FileSize = file.Length,
                        DocumentTypeId = typeId,
                        FilePath = "Database",
                        CreatedAt = DateTime.Now,
                        AttachmentFile = attachment
                    });
                }
            }
        }
    }
}