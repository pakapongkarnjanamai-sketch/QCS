using Microsoft.AspNetCore.Mvc;

namespace QCS.Web.User.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult PurchaseRequest()
        {
            return View();
        }
        public IActionResult Quotation()
        {
            return View();
        }
        public IActionResult ApprovalStep()
        {
            return View();
        }

    }
}
