using Microsoft.AspNetCore.Mvc;

namespace QCS.Web.User.Controllers
{
    public class QuotationController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        // === เพิ่มส่วนนี้ ===
        public IActionResult Detail(string id)
        {
            // ส่ง id (ที่เป็น String Code) ไปให้หน้า View
            return View("Detail", id);
        }
    }
}
