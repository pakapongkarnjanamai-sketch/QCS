using Microsoft.AspNetCore.Mvc;
using QCS.Web.User.Security;

namespace QCS.Web.User.Controllers
{
    public class HomeController : Controller
    {
        private readonly PortalCutoverRedirector _cutover;

        public HomeController(PortalCutoverRedirector cutover)
        {
            _cutover = cutover;
        }

        /// <summary>
        /// The legacy landing page. Its list is chosen by the <c>view</c> query value, which is
        /// preserved across the cutover: each legacy view lands on the SPA route showing the same
        /// rows, in one hop.
        /// </summary>
        public IActionResult Index([FromQuery] string? view)
        {
            if (_cutover.IsEnabledFor(Request))
            {
                return Redirect(_cutover.Workspace(view));
            }

            return View();
        }
    }
}
