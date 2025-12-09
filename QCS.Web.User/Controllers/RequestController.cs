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

      

        public IActionResult Index()
        {
            return View();
        }
   

        [Route("Request/Form/{id?}")]
        public IActionResult Form(int? id)
        {
            return View(id);
        }
    }
}