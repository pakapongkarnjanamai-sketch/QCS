using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration; // เพิ่มสำหรับอ่าน config
using QCS.Domain.Models;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Net.Http;

namespace QCS.Application.Services
{
    public class WorkflowService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WorkflowService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly string _workflowApiBaseUrl; // เก็บ Base URL ไว้ใช้ซ้ำ

        public WorkflowService(
            HttpClient httpClient,
            ILogger<WorkflowService> logger,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration) // Inject Config เข้ามา
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;

            // อ่านค่าจาก Config หรือใช้ค่า Default
            _workflowApiBaseUrl = configuration["WorkflowApi:BaseUrl"] ?? "http://ap-ntc2138-qawb/WorkflowApi/";
        }

        // 1. ฟังก์ชันเดิมของคุณ (ดึง Route Detail ทั้งก้อน)
        public async Task<WorkflowRouteDetailDto> GetWorkflowRouteDetailAsync(int routeId)
        {
            try
            {
                string url = $"{_workflowApiBaseUrl.TrimEnd('/')}/api/WorkflowRoutes/{routeId}/detail";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<WorkflowRouteDetailDto>(jsonString, options);

                if (result != null)
                {
                    MarkCurrentUser(result);

                    // Logic เดิมของคุณ: เช็คสิทธิ์เริ่มต้น (CanInitiate)
                    var firstStep = result.Steps?.OrderBy(s => s.SequenceNo).FirstOrDefault();
                    if (firstStep != null)
                    {
                        if (firstStep.Assignments == null || !firstStep.Assignments.Any())
                        {
                            result.CanInitiate = true;
                        }
                        else
                        {
                            result.CanInitiate = firstStep.Assignments.Any(a => a.IsCurrentUser);
                        }
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

        // 2. ฟังก์ชันใหม่ (ดึงเฉพาะชื่อพนักงาน) ** เพิ่มตรงนี้ **
        public async Task<string> GetEmployeeNameFromWorkflowAsync(int routeId, string nId)
        {
            try
            {
                // ใช้ Logic เดียวกับฟังก์ชันข้างบน แต่เอามาแค่ชื่อ
                // (อาจจะดูซ้ำซ้อนเล็กน้อย แต่ถ้า Cache ได้จะดีมาก ในที่นี้เอาแบบ Simple ก่อน)
                var routeData = await GetWorkflowRouteDetailAsync(routeId);

                if (routeData?.Steps == null) return null;

                // วนหาในทุก Step ทุก Assignment
                foreach (var step in routeData.Steps)
                {
                    if (step.Assignments != null)
                    {
                        var assignment = step.Assignments
                            .FirstOrDefault(a => string.Equals(a.NId, nId, StringComparison.OrdinalIgnoreCase));

                        if (assignment != null)
                        {
                            return assignment.EmployeeName; // เจอแล้วคืนค่าเลย
                        }
                    }
                }

                return null; // หาไม่เจอ
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding employee name for NId: {NId}", nId);
                return null;
            }
        }

        // Helper เดิมของคุณ
        private void MarkCurrentUser(WorkflowRouteDetailDto routeData)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null || !user.Identity.IsAuthenticated) return;

            string fullIdentityName = user.Identity.Name;
            string currentNId = "";
            if (!string.IsNullOrEmpty(fullIdentityName))
            {
                var parts = fullIdentityName.Split('\\');
                currentNId = parts.Length > 1 ? parts[1] : parts[0];
            }

            if (routeData.Steps != null)
            {
                foreach (var step in routeData.Steps)
                {
                    if (step.Assignments != null)
                    {
                        foreach (var assign in step.Assignments)
                        {
                            if (string.Equals(assign.NId, currentNId, StringComparison.OrdinalIgnoreCase))
                            {
                                assign.IsCurrentUser = true;
                            }
                        }
                    }
                }
            }
        }
    }
}