using Microsoft.AspNetCore.Mvc;

namespace PDF.Admin.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
       
    }
}
