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
    }
}