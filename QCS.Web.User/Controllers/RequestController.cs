using Microsoft.AspNetCore.Mvc;
using QCS.Web.User.Security;

namespace QCS.Web.User.Controllers
{
    public class RequestController : Controller
    {
        private readonly ILogger<RequestController> _logger;
        private readonly PortalCutoverRedirector _cutover;

        public RequestController(ILogger<RequestController> logger, PortalCutoverRedirector cutover)
        {
            _logger = logger;
            _cutover = cutover;
        }

        /// <summary>
        /// With no id this is "create"; with one it is the request's detail-and-actions page, not
        /// purely an editor — which is why a bookmarked id maps to the SPA's DETAIL route rather
        /// than its edit form. Sending a bookmark to /requests/{id}/edit would open an edit form
        /// for a request the user may have no right to edit.
        /// </summary>
        [Route("Request/Form/{id?}")]
        public IActionResult Form(int? id)
        {
            if (_cutover.IsEnabledFor(Request))
            {
                return Redirect(id is > 0 ? _cutover.RequestDetail(id.Value) : _cutover.NewRequest());
            }

            return View(id);
        }
        //public IActionResult Code(string id)
        //{
        //    // ส่ง id (ที่เป็น String Code) ไปให้หน้า View
        //    return View("Code", id);
        //}


    }
}