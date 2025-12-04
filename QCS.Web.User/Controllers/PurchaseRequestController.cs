using Microsoft.AspNetCore.Mvc;
using QCS.Web.User.Models;
using System.Text.Json;
using System.Net.Http.Headers;

namespace QCS.Web.User.Controllers
{
    public class PurchaseRequestController : Controller
    {
        private readonly ILogger<PurchaseRequestController> _logger;
        // Inject Service สำหรับเรียก API (ในที่นี้สมมติว่าชื่อ IApiClientService)
        // private readonly IApiClientService _apiClient; 

        public PurchaseRequestController(ILogger<PurchaseRequestController> logger)
        {
            _logger = logger;
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RequestViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // 1. แปลง JSON string กลับเป็น List<QuotationItem>
                    var quotations = JsonSerializer.Deserialize<List<QuotationItem>>(model.QuotationsJson ?? "[]");

                    // 2. (จำลอง) การเตรียมข้อมูลส่งไป API 
                    // ในโปรเจคจริง คุณจะส่ง model.Attachments และ quotations ไปยัง QCS.API
                    // ผ่าน HttpClient หรือ Service ที่คุณสร้างไว้

                    /* ตัวอย่างการส่งข้อมูลไป API (Pseudo-code)
                    using var content = new MultipartFormDataContent();
                    content.Add(new StringContent(model.Title), "Title");
                    // Loop files...
                    foreach(var file in model.Attachments) {
                        var fileContent = new StreamContent(file.OpenReadStream());
                        content.Add(fileContent, "Files", file.FileName);
                    }
                    await _apiClient.PostAsync("PurchaseRequest/Create", content); 
                    */

                    TempData["SuccessMessage"] = "สร้างใบขอซื้อสำเร็จ!";
                    return RedirectToAction("Index", "Home");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating request");
                    ModelState.AddModelError("", "เกิดข้อผิดพลาดในการบันทึกข้อมูล");
                }
            }

            return View(model);
        }
    }
}