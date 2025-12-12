using Microsoft.AspNetCore.Mvc;

namespace QCS.Web.User.Controllers
{
    public class QuotationController : Controller
    {
        public IActionResult List()
        {
            return View();
        }
        // === เพิ่มส่วนนี้ ===
        public IActionResult Index(string id)
        {
            // ส่ง id (ที่เป็น String Code) ไปให้หน้า View
            return View("Index", id);
        }
    }
}
