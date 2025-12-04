using Microsoft.AspNetCore.Mvc;

namespace QCS.Web.User.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
       
    }
}
