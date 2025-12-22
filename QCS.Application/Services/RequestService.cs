using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using QCS.Domain.DTOs;
using QCS.Domain.Enum;
using QCS.Domain.Models;
using QCS.Infrastructure.Services;
using System.Text.Json;

namespace QCS.Application.Services
{
    public interface IRequestService
    {
        IQueryable<RequestGridDto> GetMyRequestsQuery();
        Task<IQueryable<RequestGridDto>> GetMyTasksQueryAsync();
        IQueryable<RequestGridDto> GetMyApprovedListQuery();
        IQueryable<RequestGridDto> GetApprovedListQuery();
        IQueryable<RequestGridDto> GetRejectedRequestsQuery();
        Task<RequestDetailDto?> GetByCodeAsync(string code);
        Task<RequestDetailDto?> GetByIdAsync(int id);
        Task<Request> CreateAsync(CreateRequestDto input, bool isSubmit);
        Task UpdateAsync(UpdateRequestDto input, bool isSubmit);
        Task DeleteAsync(int id);
        Task<AttachmentResultDto?> GetAttachmentAsync(int id);

        Task ApproveAsync(ApprovalActionDto input);
        Task RejectAsync(ApprovalActionDto input);
    }

    public class RequestService : IRequestService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly WorkflowService _workflowService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IWebHostEnvironment _env;
        private readonly IFileService _fileService;

        // ✅ กำหนดค่าคงที่ เพื่อให้อ่านง่ายและแก้ที่เดียว
        private const int MainWorkflowId = 1;
        private const int CompletedStepId = 99;

        public RequestService(
            IUnitOfWork unitOfWork,
            WorkflowService workflowService,
            ICurrentUserService currentUserService,
            IWebHostEnvironment env,
            IFileService fileService)
        {
            _unitOfWork = unitOfWork;
            _workflowService = workflowService;
            _currentUserService = currentUserService;
            _env = env;
            _fileService = fileService;
        }

        public async Task ApproveAsync(ApprovalActionDto input)
        {
            var requestRepo = _unitOfWork.Repository<Request>();
            var request = await requestRepo.GetAll()
                .Include(r => r.ApprovalSteps)
                .FirstOrDefaultAsync(r => r.Id == input.RequestId);

            if (request == null) throw new KeyNotFoundException("ไม่พบเอกสาร Purchase Request");

            var currentStepObj = request.ApprovalSteps.FirstOrDefault(s => s.Sequence == request.CurrentStepId);
            if (currentStepObj == null) throw new Exception("ไม่พบข้อมูล Step ปัจจุบัน");

            var currentUserId = _currentUserService.UserId;
            string approverName = await GetApproverNameAsync(MainWorkflowId, currentUserId);

            // Update Step ปัจจุบัน
            currentStepObj.Status = (int)RequestStatus.Approved;
            currentStepObj.ActionDate = DateTime.Now;
            currentStepObj.Comment = input.Comment;
            currentStepObj.ApproverNId = currentUserId;
            currentStepObj.ApproverName = approverName;

            // หา Step ถัดไป
            var nextStep = request.ApprovalSteps
                .Where(s => s.Sequence > currentStepObj.Sequence)
                .OrderBy(s => s.Sequence)
                .FirstOrDefault();

            if (nextStep != null)
            {
                request.CurrentStepId = nextStep.Sequence;
                nextStep.Status = (int)RequestStatus.Pending;
            }
            else
            {
                request.Status = (int)RequestStatus.Approved;
                request.CurrentStep = WorkflowStep.Completed;
            }

            await requestRepo.UpdateAsync(request);
            await _unitOfWork.CommitAsync();
        }

        public async Task RejectAsync(ApprovalActionDto input)
        {
            var requestRepo = _unitOfWork.Repository<Request>();
            var request = await requestRepo.GetAll()
                .Include(r => r.ApprovalSteps)
                .FirstOrDefaultAsync(r => r.Id == input.RequestId);

            if (request == null) throw new KeyNotFoundException("ไม่พบเอกสาร Purchase Request");

            var currentStepObj = request.ApprovalSteps.FirstOrDefault(s => s.Sequence == request.CurrentStepId);
            if (currentStepObj == null) throw new Exception("ไม่พบข้อมูล Step ปัจจุบัน");

            var currentUserId = _currentUserService.UserId;
            string approverName = await GetApproverNameAsync(MainWorkflowId, currentUserId);

            currentStepObj.Status = (int)RequestStatus.Rejected;
            currentStepObj.ActionDate = DateTime.Now;
            currentStepObj.Comment = input.Comment;
            currentStepObj.ApproverNId = currentUserId;
            currentStepObj.ApproverName = approverName;

            request.Status = (int)RequestStatus.Rejected;
            request.CurrentStep = WorkflowStep.Rejected;

            var remainingSteps = request.ApprovalSteps.Where(s => s.Sequence > currentStepObj.Sequence);
            foreach (var step in remainingSteps) step.Status = (int)RequestStatus.Cancelled;

            await requestRepo.UpdateAsync(request);
            await _unitOfWork.CommitAsync();
        }

        public IQueryable<RequestGridDto> GetMyRequestsQuery()
        {
            return _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Where(r => r.CreatedBy == _currentUserService.UserId
                         && r.Status != (int)RequestStatus.Approved
                         && r.Status != (int)RequestStatus.Rejected)
                .Select(r => new RequestGridDto
                {
                    Id = r.Id,
                    Code = r.Code,
                    Title = r.Title,
                    VendorCode = r.VendorCode,
                    VendorName = r.VendorName,
                    RequestDate = r.RequestDate,
                    CurrentStepId = r.CurrentStepId,
                });
        }

        public IQueryable<RequestGridDto> GetRejectedRequestsQuery()
        {
            return _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Where(r => r.CreatedBy == _currentUserService.UserId && r.Status == (int)RequestStatus.Rejected)
                .Select(r => new RequestGridDto
                {
                    Id = r.Id,
                    Code = r.Code,
                    Title = r.Title,
                    VendorCode = r.VendorCode,
                    VendorName = r.VendorName,
                    RequestDate = r.RequestDate,
                    CurrentStepId = r.CurrentStepId
                });
        }

        public IQueryable<RequestGridDto> GetApprovedListQuery()
        {
            return _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Where(r => r.Status == (int)RequestStatus.Approved)
                .Select(r => new RequestGridDto
                {
                    Id = r.Id,
                    Code = r.Code,
                    Title = r.Title,
                    VendorCode = r.VendorCode,
                    VendorName = r.VendorName,
                    RequestDate = r.RequestDate,
                    CurrentStepId = r.CurrentStepId,
                    RequesterName = r.ApprovalSteps
                            .Where(s => s.Sequence == 1)
                            .Select(s => s.ApproverName)
                            .FirstOrDefault() ?? "Unknown"
                });
        }

        public async Task<IQueryable<RequestGridDto>> GetMyTasksQueryAsync()
        {
            var routeData = await _workflowService.GetWorkflowRouteDetailAsync(MainWorkflowId);
            var myStepSequences = routeData?.Steps?
                .Where(s => s.Assignments != null && s.Assignments.Any(a => a.NId == _currentUserService.UserId))
                .Select(s => s.SequenceNo)
                .ToList() ?? new List<int>();

            if (!myStepSequences.Any())
            {
                return Enumerable.Empty<RequestGridDto>().AsQueryable();
            }

            return _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Where(r => r.Status == (int)RequestStatus.Pending && myStepSequences.Contains(r.CurrentStepId))
                .Select(r => new RequestGridDto
                {
                    Id = r.Id,
                    Code = r.Code,
                    Title = r.Title,
                    VendorCode = r.VendorCode,
                    VendorName = r.VendorName,
                    RequestDate = r.RequestDate,
                    CurrentStepId = r.CurrentStepId,
                    RequesterName = r.ApprovalSteps
                            .Where(s => s.Sequence == 1)
                            .Select(s => s.ApproverName)
                            .FirstOrDefault() ?? "Unknown"
                });
        }

        public IQueryable<RequestGridDto> GetMyApprovedListQuery()
        {
            return _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Where(r => r.CreatedBy == _currentUserService.UserId && r.Status == (int)RequestStatus.Approved)
                .Select(r => new RequestGridDto
                {
                    Id = r.Id,
                    Code = r.Code,
                    Title = r.Title,
                    VendorCode = r.VendorCode,
                    VendorName = r.VendorName,
                    RequestDate = r.RequestDate,
                    CurrentStepId = r.CurrentStepId
                });
        }

        public async Task<RequestDetailDto?> GetByIdAsync(int id)
        {
            var request = await _unitOfWork.Repository<Request>().GetAll()
                .Include(r => r.Quotations).ThenInclude(q => q.AttachmentFile)
                .Include(r => r.ApprovalSteps)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return null;

            var workflowRoute = await _workflowService.GetWorkflowRouteDetailAsync(MainWorkflowId);

            if (workflowRoute?.Steps != null)
            {
                foreach (var routeStep in workflowRoute.Steps)
                {
                    var actualStep = request.ApprovalSteps.FirstOrDefault(s => s.Sequence == routeStep.SequenceNo);
                    if (actualStep != null)
                    {
                        routeStep.Status = actualStep.Status;
                        routeStep.ActionDate = actualStep.ActionDate;
                        routeStep.Comment = actualStep.Comment;
                        routeStep.ApproverName = actualStep.ApproverName;
                        routeStep.ApproverNId = actualStep.ApproverNId;
                    }
                }
            }

            return new RequestDetailDto
            {
                RequestId = request.Id,
                DocumentNo = request.Code,
                Title = request.Title,
                RequestDate = request.RequestDate,
                Status = request.Status.ToString(),
                CurrentStepId = request.CurrentStepId,
                VendorCode = request.VendorCode,
                VendorName = request.VendorName,
                ValidFrom = request.ValidFrom,
                ValidUntil = request.ValidUntil,
                Remark = request.Remark,
                Quotations = request.Quotations.Select(q => new QuotationItemDto
                {
                    Id = q.Id,
                    DocumentTypeId = q.DocumentTypeId,
                    OriginalFileName = q.FileName,
                    FilePath = q.FilePath
                }).ToList(),
                Permissions = _workflowService.GetPermissions(request, workflowRoute),
                WorkflowRoute = workflowRoute
            };
        }

        public async Task<RequestDetailDto?> GetByCodeAsync(string code)
        {
            var id = await _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Where(r => r.Code == code)
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            return id == 0 ? null : await GetByIdAsync(id);
        }

        public async Task<Request> CreateAsync(CreateRequestDto input, bool isSubmit)
        {
            var requestRepo = _unitOfWork.Repository<Request>();
            var routeData = await _workflowService.GetWorkflowRouteDetailAsync(MainWorkflowId);
            if (routeData?.Steps == null) throw new Exception("Workflow definition not found");

            var sortedSteps = routeData.Steps.OrderBy(s => s.SequenceNo).ToList();
            var newDocNo = await GenerateDocNoAsync();

            int currentStepId = 1;
            int docStatus = isSubmit ? (int)RequestStatus.Pending : (int)RequestStatus.Draft;

            if (isSubmit)
            {
                var nextStep = sortedSteps.FirstOrDefault(s => s.SequenceNo > 1);
                currentStepId = nextStep != null ? nextStep.SequenceNo : CompletedStepId;
                if (currentStepId == CompletedStepId) docStatus = (int)RequestStatus.Approved;
            }

            var pr = new Request
            {
                Code = newDocNo,
                Title = input.Title,
                RequestDate = DateTime.Now,
                Status = docStatus,
                CurrentStepId = currentStepId,
                VendorCode = input.VendorCode,
                VendorName = input.VendorName,
                ValidFrom = input.ValidFrom,
                ValidUntil = input.ValidUntil,
                Remark = input.Remark
            };

            foreach (var step in sortedSteps)
            {
                var actionDate = (step.SequenceNo == 1 && isSubmit) ? (DateTime?)DateTime.Now : null;
                var stepStatus = step.SequenceNo switch
                {
                    1 => isSubmit ? (int)RequestStatus.Approved : (int)RequestStatus.Pending,
                    2 => isSubmit ? (int)RequestStatus.Pending : (int)RequestStatus.Draft,
                    _ => (int)RequestStatus.Draft
                };

                pr.ApprovalSteps.Add(new ApprovalStep
                {
                    Sequence = step.SequenceNo,
                    StepName = step.StepName,
                    Status = stepStatus,
                    ActionDate = actionDate,
                    ApproverNId = (step.SequenceNo == 1 && isSubmit) ? _currentUserService.UserId : null,
                    ApproverName = (step.SequenceNo == 1 && isSubmit) ? await GetApproverNameAsync(MainWorkflowId, _currentUserService.UserId) : null,
                    Comment = (step.SequenceNo == 1 && isSubmit) ? input.Comment : null
                });
            }

            // ✅ ใช้ Helper ที่ปลอดภัยแทน Reflection
            var files = GetFilesFromInput(input);
            if (files != null && files.Any())
            {
                var newQuotations = await _fileService.PrepareFilesForUploadAsync(files, input.QuotationsJson);
                foreach (var q in newQuotations)
                {
                    pr.Quotations.Add(q);
                }
            }

            await requestRepo.AddAsync(pr);
            await _unitOfWork.CommitAsync();

            return pr;
        }

        public async Task UpdateAsync(UpdateRequestDto input, bool isSubmit)
        {
            var requestRepo = _unitOfWork.Repository<Request>();
            var quotationRepo = _unitOfWork.Repository<Quotation>();

            var pr = await requestRepo.GetAll()
                .Include(r => r.Quotations)
                .Include(r => r.ApprovalSteps)
                .FirstOrDefaultAsync(r => r.Id == input.Id);

            if (pr == null) throw new KeyNotFoundException("Document not found");
            if (pr.Status != (int)RequestStatus.Draft) throw new InvalidOperationException("Cannot edit non-draft document");

            pr.Title = input.Title;
            pr.VendorCode = input.VendorCode;
            pr.VendorName = input.VendorName;
            pr.ValidFrom = input.ValidFrom;
            pr.ValidUntil = input.ValidUntil;
            pr.Remark = input.Remark;

            if (isSubmit)
            {
                pr.Status = (int)RequestStatus.Pending;
                var step1 = pr.ApprovalSteps.FirstOrDefault(s => s.Sequence == 1);
                if (step1 != null)
                {
                    step1.Status = (int)RequestStatus.Approved;
                    step1.ActionDate = DateTime.Now;
                    step1.ApproverNId = _currentUserService.UserId;
                    step1.ApproverName = await GetApproverNameAsync(MainWorkflowId, _currentUserService.UserId);
                    step1.Comment = input.Comment;
                }

                var step2 = pr.ApprovalSteps.FirstOrDefault(s => s.Sequence == 2);
                if (step2 != null)
                {
                    step2.Status = (int)RequestStatus.Pending;
                    step2.ApproverNId = null;
                    step2.ApproverName = null;
                    pr.CurrentStepId = 2;
                }
            }

            if (!string.IsNullOrEmpty(input.UpdatedQuotationsJson))
            {
                var updates = JsonSerializer.Deserialize<List<QuotationItemDto>>(input.UpdatedQuotationsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                updates?.ForEach(item => {
                    var f = pr.Quotations.FirstOrDefault(q => q.Id == item.Id);
                    if (f != null) f.DocumentTypeId = item.DocumentTypeId;
                });
            }

            if (!string.IsNullOrEmpty(input.DeletedFileIds))
            {
                var ids = input.DeletedFileIds.Split(',').Select(int.Parse).ToList();
                var toRemove = pr.Quotations.Where(q => ids.Contains(q.Id)).ToList();
                await quotationRepo.DeleteRangeAsync(toRemove);
            }

            // ✅ ใช้ Helper ที่ปลอดภัยแทน Reflection
            var files = GetFilesFromInput(input);
            if (files != null && files.Any())
            {
                var newQuotations = await _fileService.PrepareFilesForUploadAsync(files, input.QuotationsJson);
                foreach (var q in newQuotations)
                {
                    pr.Quotations.Add(q);
                }
            }

            await requestRepo.UpdateAsync(pr);
            await _unitOfWork.CommitAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var requestRepo = _unitOfWork.Repository<Request>();
            var pr = await requestRepo.GetByIdAsync(id);
            if (pr != null)
            {
                await requestRepo.DeleteAsync(pr);
                await _unitOfWork.CommitAsync();
            }
        }

        public async Task<AttachmentResultDto?> GetAttachmentAsync(int fileId)
        {
            var q = await _unitOfWork.Repository<Quotation>().GetAll()
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

        private async Task<string> GenerateDocNoAsync()
        {
            var todayStr = DateTime.Now.ToString("yyyyMMdd");
            var prefix = $"QC-{todayStr}-";
            var countToday = await _unitOfWork.Repository<Request>().GetAll().CountAsync(x => x.Code.StartsWith(prefix));
            return $"{prefix}{(countToday + 1):D3}";
        }

        private async Task<string> GetApproverNameAsync(int routeId, string nId)
        {
            var name = await _workflowService.GetEmployeeNameFromWorkflowAsync(routeId, nId);
            return !string.IsNullOrEmpty(name) ? name : nId;
        }

        // ✅ ปรับปรุง Helper Method: ไม่ใช้ dynamic/reflection แล้ว
        // หมายเหตุ: ควรให้ CreateRequestDto และ UpdateRequestDto สืบทอดจาก IHasAttachments
        // หรือถ้ายังไม่ได้ทำ สามารถเปลี่ยน input เป็น object แล้ว cast ก็ได้ แต่แนะนำ Interface ดีที่สุด
        private List<IFormFile> GetFilesFromInput(object input)
        {
            if (input is IHasAttachments dto)
            {
                // เรียกผ่าน Interface กลาง ไม่ต้องเช็คชื่อ Property แล้ว
                return dto.GetUploadFiles() ?? new List<IFormFile>();
            }

            // Fallback (เผื่อกรณีอื่น)
            return new List<IFormFile>();
        }
    }
}