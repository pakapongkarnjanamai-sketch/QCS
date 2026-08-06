using System.Text.Json;
using QCS.Application.Abstractions;

namespace QCS.Infrastructure.Services
{
    public sealed class EmployeeDirectoryAdapter : IEmployeeDirectory
    {
        private readonly HttpClient _httpClient;

        public EmployeeDirectoryAdapter(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<EmployeeOrgDetails?> GetEmployeeDetailsAsync(string nId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(nId)) return null;

            var normalized = nId.Trim().ToUpperInvariant();
            var filter = JsonSerializer.Serialize(new object[] { "NID", "=", normalized });
            var requestUrl = $"?filter={Uri.EscapeDataString(filter)}&take=1";

            using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);

            var root = document.RootElement;
            JsonElement item = default;

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                item = root[0];
            }
            else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
            {
                item = data[0];
            }

            if (item.ValueKind != JsonValueKind.Object) return null;

            var deptCode = ReadText(item, "Department", "DepartmentCode", "DeptCode", "Division");
            if (string.IsNullOrWhiteSpace(deptCode))
            {
                throw new InvalidOperationException(
                    $"Employee directory returned no department code for '{normalized}'.");
            }

            var deptName = ReadText(item, "DepartmentName", "Department", "Division");
            var firstName = ReadText(item, "EnglishFirstName", "FirstName", "ThaiFirstName");
            var lastName = ReadText(item, "EnglishLastName", "LastName", "ThaiLastName");
            var fullName = $"{firstName} {lastName}".Trim();
            if (string.IsNullOrWhiteSpace(fullName)) fullName = normalized;

            return new EmployeeOrgDetails(normalized, fullName, deptCode, deptName);
        }

        private static string ReadText(JsonElement element, params string[] propertyNames)
        {
            foreach (var propName in propertyNames)
            {
                if (element.TryGetProperty(propName, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString() ?? string.Empty;
                }
            }
            return string.Empty;
        }
    }
}
