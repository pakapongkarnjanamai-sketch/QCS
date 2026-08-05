using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QCS.Web.User.Security;

namespace QCS.Web.User.Controllers
{
    [Authorize] // บังคับ Login ทุก Action
    public class QuotationController : Controller
    {
        private readonly ILogger<QuotationController> _logger;
        private readonly PortalCutoverRedirector _cutover;

        public QuotationController(ILogger<QuotationController> logger, PortalCutoverRedirector cutover)
        {
            _logger = logger;
            _cutover = cutover;
        }

        public IActionResult List()
        {
            // Straight to the SPA, NOT through Home/Index. Bouncing off the legacy landing page
            // first would make this a two-hop redirect, which PLAN-040 forbids.
            if (_cutover.IsEnabledFor(Request))
            {
                return Redirect(_cutover.Quotations());
            }

            return RedirectToAction("Index", "Home", new { view = "all-approved" });
        }

        // แนะนำให้เปลี่ยนชื่อจาก View เป็น Detail หรือ Details เพื่อลดความสับสน
        // แต่ Route ยังคงใช้ /Quotation/View/{id} ได้ถ้าต้องการ
        [Route("Quotation/View/{id}")]
        public IActionResult Detail(string id)
        {
            if (_cutover.IsEnabledFor(Request) && !string.IsNullOrWhiteSpace(id))
            {
                return Redirect(_cutover.QuotationDetail(id));
            }

            _logger.LogInformation("Accessing Quotation Detail for ID: {Id}", id);
            // ส่ง id (ที่เป็น String Code) ไปให้หน้า View ชื่อ "View.cshtml"
            return View("View", id);
        }
    }
}