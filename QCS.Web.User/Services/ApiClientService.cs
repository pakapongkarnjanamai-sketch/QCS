using QCS.Web.User.Models;
using System.Text.Json;
using System.Net.Http.Headers;

namespace QCS.Web.User.Services
{
    // Interface เพื่อให้ Mock Test ได้ง่าย และทำ Dependency Injection
    public interface IApiClientService
    {
        Task<List<MyRequestHistoryViewModel>> GetMyRequestsAsync();
        Task<ComparisonViewModel> GetRequestDetailAsync(int id);
        Task<bool> CreateRequestAsync(RequestViewModel model);
        Task<bool> ApproveRequestAsync(int stepId, string comment, bool isApproved);
    }

    public class ApiClientService : IApiClientService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }

        // 1. ดึงประวัติใบขอซื้อ (My Requests)
        public async Task<List<MyRequestHistoryViewModel>> GetMyRequestsAsync()
        {
            // ยิงไปที่ API Endpoint (ต้องไปสร้าง Controller ฝั่ง API ให้รองรับ)
            var response = await _httpClient.GetAsync("PurchaseRequest/MyRequests");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<MyRequestHistoryViewModel>>(content, _jsonOptions);
            }
            return new List<MyRequestHistoryViewModel>();
        }

        // 2. ดึงรายละเอียดเพื่อเปรียบเทียบ (Review)
        public async Task<ComparisonViewModel> GetRequestDetailAsync(int id)
        {
            var response = await _httpClient.GetAsync($"PurchaseRequest/{id}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                // Map ข้อมูลจาก API Model มาเป็น ViewModel (หรือถ้าชื่อตรงกันก็ Deserialize ได้เลย)
                return JsonSerializer.Deserialize<ComparisonViewModel>(content, _jsonOptions);
            }
            return null;
        }

        // 3. สร้างใบขอซื้อใหม่ (พร้อมอัปโหลดไฟล์)
        public async Task<bool> CreateRequestAsync(RequestViewModel model)
        {
            using var content = new MultipartFormDataContent();

            // ส่งข้อมูล Text
            content.Add(new StringContent(model.Title ?? ""), "Title");
            content.Add(new StringContent(model.RequestDate.ToString("O")), "RequestDate");
            content.Add(new StringContent(model.QuotationsJson ?? "[]"), "QuotationsJson");

            // ส่งไฟล์ (Loop Attachments)
            if (model.Attachments != null)
            {
                foreach (var file in model.Attachments)
                {
                    var fileContent = new StreamContent(file.OpenReadStream());
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                    content.Add(fileContent, "Attachments", file.FileName);
                }
            }

            var response = await _httpClient.PostAsync("PurchaseRequest/Create", content);
            return response.IsSuccessStatusCode;
        }

        // 4. อนุมัติ / ไม่อนุมัติ
        public async Task<bool> ApproveRequestAsync(int stepId, string comment, bool isApproved)
        {
            var payload = new
            {
                StepId = stepId,
                Comment = comment,
                IsApproved = isApproved
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("Approval/Action", jsonContent);

            return response.IsSuccessStatusCode;
        }
    }
}