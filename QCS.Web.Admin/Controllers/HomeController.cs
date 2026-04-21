using Microsoft.AspNetCore.Mvc;

namespace QCS.Web.Admin.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction(nameof(Workspace));
        }

        public IActionResult Workspace()
        {
            return View();
        }

    }
}
