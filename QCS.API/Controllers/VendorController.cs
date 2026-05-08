using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using QCS.Application.Services;
using DevExtreme.AspNet.Mvc;
using DevExtreme.AspNet.Data;
using QCS.Domain.DTOs;

namespace QCS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VendorController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IRequestService _requestService;

        public VendorController(IHttpClientFactory httpClientFactory, IRequestService requestService)
        {
            _httpClientFactory = httpClientFactory;
            _requestService = requestService;
        }

        private static string GetStringValue(JsonElement source, params string[] keys)
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

            return "-";
        }

        private static IEnumerable<JsonElement> EnumerateVendorItems(JsonElement root)
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

        private async Task<List<VendorGridDto>> LoadVendorGridRowsAsync()
        {
            var client = _httpClientFactory.CreateClient("VendorApi");
            var response = await client.GetAsync("Vendors/LookupVendors");

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Error calling Vendor API ({(int)response.StatusCode})");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);

            var quotationCounts = _requestService
                .GetAllApprovedRequestsQuery()
                .Where(request => !string.IsNullOrWhiteSpace(request.VendorCode))
                .GroupBy(request => (request.VendorCode ?? string.Empty).Trim().ToUpper())
                .ToDictionary(group => group.Key, group => group.Count());

            var rows = new List<VendorGridDto>();

            foreach (var item in EnumerateVendorItems(document.RootElement))
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var vendorCode = GetStringValue(item, "vendorCode", "VendorCode", "code", "Code", "supplierCode");
                var vendorName = GetStringValue(item, "vendorName", "VendorName", "name", "Name", "supplierName");
                var normalizedCode = vendorCode.Trim().ToUpper();

                rows.Add(new VendorGridDto
                {
                    VendorCode = vendorCode,
                    VendorName = vendorName,
                    TaxId = GetStringValue(item, "taxId", "TaxId", "taxNo", "TaxNo"),
                    ContactName = GetStringValue(item, "contactName", "ContactName", "contact", "Contact"),
                    Phone = GetStringValue(item, "phone", "Phone", "tel", "Tel", "telephone", "Telephone"),
                    Email = GetStringValue(item, "email", "Email", "mail", "Mail"),
                    Address = GetStringValue(item, "address", "Address"),
                    QuotationCount = quotationCounts.TryGetValue(normalizedCode, out var count) ? count : 0,
                });
            }

            return rows;
        }

        [HttpGet]
        public async Task<IActionResult> GetVendors()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("VendorApi");

                // ยิงไปที่ Endpoint ปลายทาง "Suppliers"
                // คุณสามารถรับ Query String จาก Frontend มาส่งต่อได้ถ้าต้องการ (เช่น ?filter=...)
                var response = await client.GetAsync("Vendors/LookupVendors" + Request.QueryString);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();

                    // ส่ง Raw JSON กลับไปให้ Frontend เลย (Proxy Pass-through)
                    // หรือจะ Deserialize มาจัดการก่อนก็ได้
                    return Content(content, "application/json");
                }

                return StatusCode((int)response.StatusCode, "Error calling Vendor API");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }

        [HttpGet("Grid")]
        public async Task<object> GetVendorGrid(DataSourceLoadOptions loadOptions)
        {
            var rows = await LoadVendorGridRowsAsync();
            var query = rows.AsQueryable();
            return DataSourceLoader.Load(query, loadOptions);
        }

        [HttpGet("Lookup")]
        public async Task<object> GetVendorLookup(DataSourceLoadOptions loadOptions)
        {
            var rows = await LoadVendorGridRowsAsync();
            var query = rows
                .Where(row => !string.IsNullOrWhiteSpace(row.VendorCode) && row.VendorCode != "-")
                .GroupBy(row => row.VendorCode.Trim().ToUpper())
                .Select(group => group.First())
                .Select(item => new VendorLookupDto
                {
                    VendorCode = item.VendorCode,
                    VendorName = string.IsNullOrWhiteSpace(item.VendorName) ? item.VendorCode : item.VendorName,
                })
                .AsQueryable();

            return DataSourceLoader.Load(query, loadOptions);
        }

        public sealed class QuotationCountsRequest
        {
            public List<string> VendorCodes { get; set; } = new();
        }

        [HttpPost("QuotationCounts")]
        public IActionResult GetQuotationCounts([FromBody] QuotationCountsRequest? request)
        {
            var normalizedCodes = (request?.VendorCodes ?? new List<string>())
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim().ToUpper())
                .Distinct()
                .ToList();

            if (!normalizedCodes.Any())
            {
                return Ok(Array.Empty<object>());
            }

            var counts = _requestService
                .GetAllApprovedRequestsQuery()
                .Where(request => normalizedCodes.Contains((request.VendorCode ?? string.Empty).ToUpper()))
                .GroupBy(request => request.VendorCode)
                .Select(group => new
                {
                    vendorCode = group.Key,
                    quotationCount = group.Count()
                })
                .ToList();

            return Ok(counts);
        }
    }
}