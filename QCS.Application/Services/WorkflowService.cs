using Microsoft.Extensions.Caching.Memory;
using QCS.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QCS.Domain.DTOs;
using QCS.Domain.Models;
using System.Text;
using System.Text.Json;

namespace QCS.Application.Services
{
    public class WorkflowService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WorkflowService> _logger;
        private readonly ICurrentUserService _currentUserService;
        private readonly string _workflowApiBaseUrl;
        private readonly IMemoryCache _cache;

        public WorkflowService(
            HttpClient httpClient,
            ILogger<WorkflowService> logger,
            ICurrentUserService currentUserService,
            IConfiguration configuration,
            IMemoryCache cache)
        {
            _httpClient = httpClient;
            _logger = logger;
            _currentUserService = currentUserService;
            _workflowApiBaseUrl = configuration["ExternalServices:WorkflowApi"] ?? "http://ap-ntc2138-qawb/WorkflowApi/";
            _cache = cache;
        }

        public PermissionDto GetPermissions(Request request, WorkflowRouteDetailDto? workflowRoute)
        {
            // ... (Code เดิม ไม่เปลี่ยนแปลง) ...
            bool canApprove = false;
            bool canReject = false;
            bool canEdit = request.Status == (int)QCS.Domain.Enum.RequestStatus.Draft;

            if (request.Status == (int)QCS.Domain.Enum.RequestStatus.Pending && workflowRoute?.Steps != null)
            {
                var currentStepConfig = workflowRoute.Steps.FirstOrDefault(s => s.SequenceNo == request.CurrentStepId);
                if (currentStepConfig?.Assignments != null &&
                    currentStepConfig.Assignments.Any(a => string.Equals(a.NId, _currentUserService.UserId, StringComparison.OrdinalIgnoreCase)))
                {
                    canApprove = true;
                    canReject = true;
                }
            }

            return new PermissionDto
            {
                CanApprove = canApprove,
                CanReject = canReject,
                CanEdit = canEdit
            };
        }

        public async Task<WorkflowRouteDetailDto?> GetWorkflowRouteDetailAsync(int routeId, string? createdBy = null)
        {
            string creatorId = !string.IsNullOrEmpty(createdBy) ? createdBy : _currentUserService.UserId;

            // Key ขึ้นอยู่กับ Route และ เจ้าของเอกสาร (ไม่ขึ้นกับคนดู)
            string cacheKey = $"Workflow_Route_{routeId}_Creator_{creatorId}";

            // 1. ดึงข้อมูลดิบจาก Cache (Raw Data)
            var cachedData = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                // ตั้งเวลาหมดอายุ Cache (เช่น 1 นาที)
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);

                // เรียก API เพื่อเอาข้อมูลดิบ (ยังไม่ Mark CurrentUser)
                return await FetchWorkflowRouteFromApi(routeId, creatorId);
            });

            if (cachedData == null) return null;

            // 2. Deep Clone ข้อมูลออกมา
            // สำคัญมาก: เพราะ IMemoryCache เก็บ Reference ถ้าเราแก้ cachedData โดยตรง ข้อมูลใน Cache จะเสีย
            var json = JsonSerializer.Serialize(cachedData);
            var result = JsonSerializer.Deserialize<WorkflowRouteDetailDto>(json);

            // 3. ปรับแต่งข้อมูลสำหรับ User คนปัจจุบัน (Personalize)
            if (result != null)
            {
                MarkCurrentUser(result);

                // คำนวณ CanInitiate ใหม่สำหรับ User คนนี้
                var firstStep = result.Steps?.MinBy(s => s.SequenceNo);
                if (firstStep != null)
                {
                    result.CanInitiate = firstStep.Assignments == null ||
                                         !firstStep.Assignments.Any() ||
                                         firstStep.Assignments.Any(a => a.IsCurrentUser);
                }
            }

            return result;
        }

        // Method นี้ทำหน้าที่ดึงข้อมูลดิบอย่างเดียว ไม่ต้องยุ่งกับ Logic User
        private async Task<WorkflowRouteDetailDto?> FetchWorkflowRouteFromApi(int routeId, string creatorId)
        {
            try
            {
                string url = $"{_workflowApiBaseUrl.TrimEnd('/')}/api/WorkflowResolutions/resolve";

                var payload = new { routeId = routeId, createdBy = creatorId };
                var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, jsonContent);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();

                var apiResponse = JsonSerializer.Deserialize<WorkflowApiResponse>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (apiResponse == null) return null;

                return new WorkflowRouteDetailDto
                {
                    Id = apiResponse.RouteId,
                    RouteName = apiResponse.RouteName,
                    Steps = apiResponse.Steps?.Select(s => new WorkflowStepDto
                    {
                        Id = s.StepId,
                        SequenceNo = s.SequenceNo,
                        StepName = s.StepName,
                        Assignments = s.Assignees?.Select(a => new AssignmentDto
                        {
                            NId = a.NId,
                            EmployeeName = a.EmployeeName,
                            AssignmentType = a.AssignmentType,
                            IsCurrentUser = false // Default เป็น False เสมอใน Cache
                        }).ToList() ?? new List<AssignmentDto>()
                    }).ToList() ?? new List<WorkflowStepDto>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching workflow for RouteId: {RouteId}, Creator: {Creator}", routeId, creatorId);
                return null;
            }
        }

        private void MarkCurrentUser(WorkflowRouteDetailDto routeData)
        {
            if (!_currentUserService.IsAuthenticated) return;
            string currentNId = _currentUserService.UserId;

            if (routeData.Steps == null) return;

            var userAssignments = routeData.Steps
                .SelectMany(s => s.Assignments ?? Enumerable.Empty<AssignmentDto>())
                .Where(a => string.Equals(a.NId, currentNId, StringComparison.OrdinalIgnoreCase));

            foreach (var assign in userAssignments)
            {
                assign.IsCurrentUser = true;
            }
        }

        // ... (ส่วน Helper Methods และ Class ย่อย เหมือนเดิม) ...

        public async Task<string?> GetEmployeeNameFromWorkflowAsync(int routeId, string nId, string? createdBy = null)
        {
            // เรียกผ่าน GetWorkflowRouteDetailAsync ได้เลย เพราะมี Cache แล้ว ไม่ช้าแน่นอน
            var routeData = await GetWorkflowRouteDetailAsync(routeId, createdBy);

            return routeData?.Steps?
                .SelectMany(s => s.Assignments ?? Enumerable.Empty<AssignmentDto>())
                .FirstOrDefault(a => string.Equals(a.NId, nId, StringComparison.OrdinalIgnoreCase))?
                .EmployeeName;
        }

        private class WorkflowApiResponse
        {
            public int RouteId { get; set; }
            public string RouteName { get; set; } = string.Empty;
            public List<WorkflowApiStep>? Steps { get; set; }
        }
        private class WorkflowApiStep
        {
            public int StepId { get; set; }
            public int SequenceNo { get; set; }
            public string StepName { get; set; } = string.Empty;
            public List<WorkflowApiAssignee>? Assignees { get; set; }
        }
        private class WorkflowApiAssignee
        {
            public string NId { get; set; } = string.Empty;
            public string EmployeeName { get; set; } = string.Empty;
            public string AssignmentType { get; set; } = string.Empty;
        }
    }
}