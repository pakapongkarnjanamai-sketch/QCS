using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace QCS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VendorController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public VendorController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public async Task<IActionResult> GetVendors()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("VendorApi");

                // ยิงไปที่ Endpoint ปลายทาง "Suppliers"
                // คุณสามารถรับ Query String จาก Frontend มาส่งต่อได้ถ้าต้องการ (เช่น ?filter=...)
                var response = await client.GetAsync("Suppliers" + Request.QueryString);

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
    }
}