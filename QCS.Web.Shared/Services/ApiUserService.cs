using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Text;
using QCS.Web.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http;


namespace QCS.Web.Shared.Services
{
    public interface IApiUserService
    {
        Task<ApiResponse<UserDto>> GetOrCreateUserAsync(string windowsIdentity);

    }
    public class ApiUserService : IApiUserService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiUserService> _logger;
        private readonly IMemoryCache _cache;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiUserService(IHttpClientFactory httpClientFactory, ILogger<ApiUserService> logger, IMemoryCache cache)
        {
            _httpClient = httpClientFactory.CreateClient("DocTrackerAPI");
            _logger = logger;
            _cache = cache;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = null
            };
        }

        public async Task<ApiResponse<UserDto>> GetOrCreateUserAsync(string windowsIdentity)
        {
            try
            {
                var cacheKey = $"user_{windowsIdentity}";

                // Check cache first
                if (_cache.TryGetValue(cacheKey, out ApiResponse<UserDto>? cachedUser))
                {
                    return cachedUser!;
                }

                var request = new CreateUserRequest { WindowsIdentity = windowsIdentity };
                var json = JsonSerializer.Serialize(request, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("api/users/windows-auth", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<ApiResponse<UserDto>>(responseContent, _jsonOptions);

                    // Cache for 5 minutes
                    if (result?.Success == true)
                    {
                        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
                    }

                    return result ?? new ApiResponse<UserDto> { Success = false, Message = "Invalid response" };
                }
                else
                {
                    _logger.LogError("Failed to get/create user. Status: {StatusCode}, Content: {Content}",
                        response.StatusCode, responseContent);
                    return new ApiResponse<UserDto>
                    {
                        Success = false,
                        Message = $"API call failed: {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling API to get/create user: {WindowsIdentity}", windowsIdentity);
                return new ApiResponse<UserDto>
                {
                    Success = false,
                    Message = "An error occurred while processing the request"
                };
            }
        }

    }
}
