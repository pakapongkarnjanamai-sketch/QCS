using System.Text.Json;

namespace QCS.API.Services
{
    public interface IEmployeeLookupService
    {
        Task<IReadOnlyList<EmployeeDepartmentItem>> GetDepartmentItemsAsync(CancellationToken cancellationToken = default);
        Task<Dictionary<string, string>> GetDepartmentMapByNIdsAsync(IEnumerable<string> nIds, CancellationToken cancellationToken = default);
    }

    public sealed class EmployeeDepartmentItem
    {
        public string NId { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
    }

    public sealed class EmployeeLookupService : IEmployeeLookupService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _employeeLookupDepartmentApi;

        public EmployeeLookupService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _employeeLookupDepartmentApi = configuration["ExternalServices:EmployeeLookupDepartmentApi"]
                ?? throw new InvalidOperationException("ExternalServices:EmployeeLookupDepartmentApi configuration is required.");
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