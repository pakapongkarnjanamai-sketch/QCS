using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QCS.Web.User.Controllers
{
    [Authorize] // บังคับ Login ทุก Action
    public class QuotationController : Controller
    {
        private readonly ILogger<QuotationController> _logger;

        public QuotationController(ILogger<QuotationController> logger)
        {
            _logger = logger;
        }

        public IActionResult List()
        {
            return View();
        }

        // แนะนำให้เปลี่ยนชื่อจาก View เป็น Detail หรือ Details เพื่อลดความสับสน
        // แต่ Route ยังคงใช้ /Quotation/View/{id} ได้ถ้าต้องการ
        [Route("Quotation/View/{id}")]
        public IActionResult Detail(string id)
        {
            _logger.LogInformation("Accessing Quotation Detail for ID: {Id}", id);
            // ส่ง id (ที่เป็น String Code) ไปให้หน้า View ชื่อ "View.cshtml"
            return View("View", id);
        }
    }
}