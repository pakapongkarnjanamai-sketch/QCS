using System.Text.Json.Serialization;

namespace QCS.Domain.DTOs
{
    /// <summary>
    /// Standard envelope returned by API endpoints.
    /// </summary>
    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int StatusCode { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IEnumerable<string>? Errors { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; set; }
    }

    /// <summary>
    /// Standard envelope carrying a typed <see cref="Data"/> payload.
    /// </summary>
    public class ApiResponse<T> : ApiResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public T? Data { get; set; }

        public static ApiResponse<T> Ok(T data, string message = "")
            => new() { Success = true, StatusCode = 200, Message = message, Data = data };

        public static ApiResponse<T> Fail(int statusCode, string message, IEnumerable<string>? errors = null)
            => new() { Success = false, StatusCode = statusCode, Message = message, Errors = errors };
    }
}
