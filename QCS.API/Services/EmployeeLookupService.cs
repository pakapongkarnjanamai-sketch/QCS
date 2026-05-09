using System.Text.Json;

namespace QCS.API.Services
{
    public interface IEmployeeLookupService
    {
        Task<IReadOnlyList<EmployeeDepartmentItem>> GetDepartmentItemsAsync(CancellationToken cancellationToken = default);
        Task<Dictionary<string, string>> GetDepartmentMapByNIdsAsync(IEnumerable<string> nIds, CancellationToken cancellationToken = default);
        Task<EmployeeFullItem?> GetEmployeeByNIdAsync(string nId, CancellationToken cancellationToken = default);
    }

    public sealed class EmployeeDepartmentItem
    {
        public string NId { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
    }

    public sealed class EmployeeFullItem
    {
        public string NId { get; set; } = string.Empty;
        public string EId { get; set; } = string.Empty;
        public string EnglishFirstName { get; set; } = string.Empty;
        public string EnglishLastName { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string CostCenter { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public sealed class EmployeeLookupService : IEmployeeLookupService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _employeeLookupDepartmentApi;
        private readonly string _employeeLookupFullApi;

        public EmployeeLookupService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _employeeLookupDepartmentApi = configuration["ExternalServices:EmployeeLookupDepartmentApi"]
                ?? throw new InvalidOperationException("ExternalServices:EmployeeLookupDepartmentApi configuration is required.");
            _employeeLookupFullApi = configuration["ExternalServices:EmployeeLookupFullApi"]
                ?? "https://ap-ntc2137-prwb/Utility/EmployeeServiceV2/api/EmployeeLookup/GetFull";
        }

        public async Task<IReadOnlyList<EmployeeDepartmentItem>> GetDepartmentItemsAsync(CancellationToken cancellationToken = default)
        {
            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(_employeeLookupDepartmentApi, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Employee lookup API returned {(int)response.StatusCode}.");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);

            return EnumerateLookupItems(document.RootElement)
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .Select(item => new EmployeeDepartmentItem
                {
                    NId = ReadText(item, "id", "Id", "nId", "NId", "nid", "NID").ToUpper(),
                    DepartmentName = ReadText(item, "name", "Name", "department", "Department"),
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.NId))
                .ToList();
        }

        public async Task<Dictionary<string, string>> GetDepartmentMapByNIdsAsync(IEnumerable<string> nIds, CancellationToken cancellationToken = default)
        {
            var nIdSet = nIds
                .Where(nId => !string.IsNullOrWhiteSpace(nId))
                .Select(nId => nId.Trim().ToUpper())
                .ToHashSet();

            if (nIdSet.Count == 0)
            {
                return new Dictionary<string, string>();
            }

            var items = await GetDepartmentItemsAsync(cancellationToken);

            return items
                .Where(item => nIdSet.Contains(item.NId))
                .GroupBy(item => item.NId)
                .ToDictionary(group => group.Key, group =>
                {
                    var names = group
                        .Select(item => item.DepartmentName)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct()
                        .OrderBy(name => name)
                        .ToList();

                    return names.Count == 0 ? "-" : string.Join(", ", names);
                });
        }

        public async Task<EmployeeFullItem?> GetEmployeeByNIdAsync(string nId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(nId))
            {
                return null;
            }

            var normalized = nId.Trim().ToUpperInvariant();
            var filter = JsonSerializer.Serialize(new object[] { "NID", "=", normalized });
            var requestUrl = $"{_employeeLookupFullApi}?filter={Uri.EscapeDataString(filter)}&take=1";

            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(requestUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Employee lookup API returned {(int)response.StatusCode}.");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);

            var item = EnumerateLookupItems(document.RootElement)
                .FirstOrDefault(x =>
                    string.Equals(ReadText(x, "NID", "nId", "NId", "nid", "id", "Id"), normalized, StringComparison.OrdinalIgnoreCase));

            if (item.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new EmployeeFullItem
            {
                NId = ReadText(item, "NID", "nId", "NId", "nid", "id", "Id").ToUpperInvariant(),
                EId = ReadText(item, "EId", "eId", "employeeId", "EmployeeID"),
                EnglishFirstName = ReadText(item, "EnglishFirstName", "FirstName"),
                EnglishLastName = ReadText(item, "EnglishLastName", "LastName"),
                Division = ReadText(item, "Division"),
                Department = ReadText(item, "Department"),
                Section = ReadText(item, "Section"),
                Position = ReadText(item, "Position"),
                CostCenter = ReadText(item, "CostCenter"),
                Email = ReadText(item, "Email", "email"),
            };
        }

        private static IEnumerable<JsonElement> EnumerateLookupItems(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Array)
            {
                return root.EnumerateArray();
            }

            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Array)
            {
                return data.EnumerateArray();
            }

            return Array.Empty<JsonElement>();
        }

        private static string ReadText(JsonElement source, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (source.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null)
                {
                    var text = value.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }

            return string.Empty;
        }
    }
}