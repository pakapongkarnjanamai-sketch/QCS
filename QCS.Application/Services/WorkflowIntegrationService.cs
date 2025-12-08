using Microsoft.Extensions.Logging;
using QCS.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace QCS.Application.Services
{
    public class WorkflowIntegrationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WorkflowIntegrationService> _logger;

        public WorkflowIntegrationService(HttpClient httpClient, ILogger<WorkflowIntegrationService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<WorkflowRouteDetailDto> GetWorkflowRouteDetailAsync(int routeId)
        {
            try
            {
                // URL ของ Workflow API (ควรย้ายไป config ใน appsettings.json ในอนาคต)
                // สมมติว่า protocol เป็น http
                string url = $"http://ap-ntc2138-qawb/WorkflowApi/api/WorkflowRoutes/{routeId}/detail";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true // เพื่อให้อ่าน field json ตัวเล็ก/ใหญ่ ได้ตรงกัน
                };

                var result = JsonSerializer.Deserialize<WorkflowRouteDetailDto>(jsonString, options);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching workflow route");
                return null; // หรือ throw exception ตามต้องการ
            }
        }
    }
}