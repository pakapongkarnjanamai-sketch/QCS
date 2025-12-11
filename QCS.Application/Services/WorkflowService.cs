using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using QCS.Domain.Models;
using System.Text.Json;

namespace QCS.Application.Services
{
    public class WorkflowService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WorkflowService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly string _workflowApiBaseUrl;

        public WorkflowService(
            HttpClient httpClient,
            ILogger<WorkflowService> logger,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _workflowApiBaseUrl = configuration["WorkflowApi:BaseUrl"] ?? "http://ap-ntc2138-qawb/WorkflowApi/";
        }

        public async Task<WorkflowRouteDetailDto?> GetWorkflowRouteDetailAsync(int routeId)
        {
            try
            {
                string url = $"{_workflowApiBaseUrl.TrimEnd('/')}/api/WorkflowRoutes/{routeId}/detail";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<WorkflowRouteDetailDto>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result != null)
                {
                    MarkCurrentUser(result);

                    // Logic: เช็คสิทธิ์เริ่มต้น
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

        public async Task<string?> GetEmployeeNameFromWorkflowAsync(int routeId, string nId)
        {
            // Optimization: ดึงข้อมูล Route มาแล้วใช้ LINQ ค้นหาทันที
            var routeData = await GetWorkflowRouteDetailAsync(routeId);

            return routeData?.Steps?
                .SelectMany(s => s.Assignments ?? Enumerable.Empty<AssignmentDto>())
                .FirstOrDefault(a => string.Equals(a.NId, nId, StringComparison.OrdinalIgnoreCase))?
                .EmployeeName;
        }

        private void MarkCurrentUser(WorkflowRouteDetailDto routeData)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return;

            string currentNId = GetCurrentNId(user.Identity.Name);

            if (routeData.Steps == null) return;

            // ใช้ LINQ เพื่อ Update Flag IsCurrentUser แทน Loop ซ้อน
            var userAssignments = routeData.Steps
                .SelectMany(s => s.Assignments ?? Enumerable.Empty<AssignmentDto>())
                .Where(a => string.Equals(a.NId, currentNId, StringComparison.OrdinalIgnoreCase));

            foreach (var assign in userAssignments)
            {
                assign.IsCurrentUser = true;
            }
        }

        private string GetCurrentNId(string? identityName)
        {
            if (string.IsNullOrEmpty(identityName)) return string.Empty;
            var parts = identityName.Split('\\');
            return parts.Length > 1 ? parts[1] : parts[0];
        }
    }
}