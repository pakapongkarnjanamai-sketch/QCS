using Microsoft.AspNetCore.Mvc;

namespace QCS.Web.User.Controllers
{
    public class RequestController : Controller
    {
        private readonly ILogger<RequestController> _logger;

        public RequestController(ILogger<RequestController> logger)
        {
            _logger = logger;
        }

        [Route("Request/Form/{id?}")]
        public IActionResult Form(int? id)
        {
            return View(id);
        }
        public IActionResult Code(string id)
        {
            // ส่ง id (ที่เป็น String Code) ไปให้หน้า View
            return View("Code", id);
        }


    }
}