using Microsoft.AspNetCore.Mvc;

namespace QCS.Web.Admin.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Departments()
        {
            return View();
        }
        public IActionResult Roles()
        {
            return View();
        }
        public IActionResult Users()
        {
            return View();
        }
    }
}
