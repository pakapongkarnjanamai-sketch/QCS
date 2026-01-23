using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QCS.Domain.DTOs;
using QCS.Domain.Enum;
using QCS.Domain.Models;
using System.Text.Json;
using System.Text;

namespace QCS.Application.Services
{
    public class WorkflowService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WorkflowService> _logger;
        private readonly ICurrentUserService _currentUserService;
        private readonly string _workflowApiBaseUrl;

        public WorkflowService(
            HttpClient httpClient,
            ILogger<WorkflowService> logger,
            ICurrentUserService currentUserService,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _currentUserService = currentUserService;
            _workflowApiBaseUrl = configuration["ExternalServices:WorkflowApi"] ?? "http://ap-ntc2138-qawb/WorkflowApi/";
        }

        public PermissionDto GetPermissions(Request request, WorkflowRouteDetailDto? workflowRoute)
        {
            bool canApprove = false;
            bool canReject = false;
            bool canEdit = request.Status == (int)RequestStatus.Draft;

            if (request.Status == (int)RequestStatus.Pending && workflowRoute?.Steps != null)
            {
                var currentStepConfig = workflowRoute.Steps.FirstOrDefault(s => s.SequenceNo == request.CurrentStepId);
                if (currentStepConfig?.Assignments != null && currentStepConfig.Assignments.Any(a => a.IsCurrentUser))
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
            try
            {
                // ใช้ Current User ถ้าไม่ได้ระบุ createdBy
                string creatorId = !string.IsNullOrEmpty(createdBy) ? createdBy : _currentUserService.UserId;

                string url = $"{_workflowApiBaseUrl.TrimEnd('/')}/api/WorkflowResolutions/resolve";

                var payload = new
                {
                    routeId = routeId,
                    createdBy = creatorId
                };

                var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, jsonContent);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();

                // ใช้ Custom Model สำหรับรับค่าจาก API ใหม่
                var apiResponse = JsonSerializer.Deserialize<WorkflowApiResponse>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (apiResponse == null) return null;

                // Map กลับไปยัง DTO เดิมของระบบ
                var result = new WorkflowRouteDetailDto
                {
                    Id = apiResponse.RouteId,
                    RouteName = apiResponse.RouteName,
                    Steps = apiResponse.Steps?.Select(s => new WorkflowStepDto
                    {
                        Id = s.StepId, // Map stepId -> Id
                        SequenceNo = s.SequenceNo,
                        StepName = s.StepName,
                        Assignments = s.Assignees?.Select(a => new AssignmentDto
                        {
                            NId = a.NId,
                            EmployeeName = a.EmployeeName,
                            AssignmentType = a.AssignmentType
                        }).ToList() ?? new List<AssignmentDto>()
                    }).ToList() ?? new List<WorkflowStepDto>()
                };

                // Logic เดิม: Mark Current User และ Check Initiate
                if (result != null)
                {
                    MarkCurrentUser(result);

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching workflow route detail for RouteId: {RouteId}", routeId);
                return null;
            }
        }

        public async Task<string?> GetEmployeeNameFromWorkflowAsync(int routeId, string nId, string? createdBy = null)
        {
            var routeData = await GetWorkflowRouteDetailAsync(routeId, createdBy);

            return routeData?.Steps?
                .SelectMany(s => s.Assignments ?? Enumerable.Empty<AssignmentDto>())
                .FirstOrDefault(a => string.Equals(a.NId, nId, StringComparison.OrdinalIgnoreCase))?
                .EmployeeName;
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

        // Inner Classes for New API Response Mapping
        private class WorkflowApiResponse
        {
            public int RouteId { get; set; }
            public string RouteName { get; set; }
            public List<WorkflowApiStep>? Steps { get; set; }
        }

        private class WorkflowApiStep
        {
            public int StepId { get; set; }
            public int SequenceNo { get; set; }
            public string StepName { get; set; }
            public List<WorkflowApiAssignee>? Assignees { get; set; }
        }

        private class WorkflowApiAssignee
        {
            public string NId { get; set; }
            public string EmployeeName { get; set; }
            public string AssignmentType { get; set; }
        }
    }
}