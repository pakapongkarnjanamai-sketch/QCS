using Microsoft.AspNetCore.Http; // จำเป็นสำหรับ IHttpContextAccessor
using Microsoft.Extensions.Logging;
using QCS.Domain.Models;


using System.Text.Json;

namespace QCS.Application.Services
{
    public class WorkflowIntegrationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WorkflowIntegrationService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor; // 1. เพิ่มตัวนี้

        // 2. Inject เข้ามาใน Constructor
        public WorkflowIntegrationService(
            HttpClient httpClient,
            ILogger<WorkflowIntegrationService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<WorkflowRouteDetailDto> GetWorkflowRouteDetailAsync(int routeId)
        {
            try
            {
                // ... (Code การยิง API เดิม) ...
                string url = $"http://ap-ntc2138-qawb/WorkflowApi/api/WorkflowRoutes/{routeId}/detail";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var jsonString = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<WorkflowRouteDetailDto>(jsonString, options);

                // === 3. เพิ่ม Logic ตรวจสอบ User ตรงนี้ ===
                if (result != null)
                {
                    MarkCurrentUser(result);
                    var firstStep = result.Steps?.OrderBy(s => s.SequenceNo).FirstOrDefault();
                    if (firstStep != null)
                    {
                        // ถ้าไม่มี assignments เลย = ใครก็ได้ (true)
                        // ถ้ามี assignments ต้องเช็คว่ามี User ปัจจุบันหรือไม่
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
                _logger.LogError(ex, "Error fetching workflow route");
                return null;
            }
        }

        // ฟังก์ชันสำหรับเช็คว่าใครคือ Current User
        private void MarkCurrentUser(WorkflowRouteDetailDto routeData)
        {
            // ดึง User จาก Token/Cookie
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null || !user.Identity.IsAuthenticated) return;

            // ดึง NId (เช่น "DOMAIN\u1234")
            string fullIdentityName = user.Identity.Name;

            // ตัด Domain ออกให้เหลือแค่ u1234 เพื่อเทียบกับ Workflow API
            string currentNId = "";
            if (!string.IsNullOrEmpty(fullIdentityName))
            {
                var parts = fullIdentityName.Split('\\');
                currentNId = parts.Length > 1 ? parts[1] : parts[0];
            }

            // วน Loop ข้อมูลเพื่อ Mark Flag
            if (routeData.Steps != null)
            {
                foreach (var step in routeData.Steps)
                {
                    if (step.Assignments != null)
                    {
                        foreach (var assign in step.Assignments)
                        {
                            // เปรียบเทียบแบบ Case Insensitive
                            if (string.Equals(assign.NId, currentNId, StringComparison.OrdinalIgnoreCase))
                            {
                                assign.IsCurrentUser = true; // Set ค่าให้ Frontend รู้
                            }
                        }
                    }
                }
            }
        }
    }
}