using Microsoft.AspNetCore.Mvc;


namespace QCS.Web.User.Controllers
{
    public class PurchaseRequestController : Controller
    {
        private readonly ILogger<PurchaseRequestController> _logger;

        public PurchaseRequestController(ILogger<PurchaseRequestController> logger)
        {
            _logger = logger;
        }

        public IActionResult Create()
        {
            return View();
        }

        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Detail(int id)
        {
            // ส่ง id ไปให้ View เพื่อให้ JS เอาไปยิง API ต่อ
            return View(id);
        }

        [Route("PurchaseRequest/Form/{id?}")]
        public IActionResult Form(int? id)
        {
            return View(id);
        }
    }
}