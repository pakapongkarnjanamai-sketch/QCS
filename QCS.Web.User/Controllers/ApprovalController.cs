using Microsoft.AspNetCore.Mvc;

namespace QCS.Web.User.Controllers
{
    public class ApprovalController : Controller
    {
        //// หน้าจอเปรียบเทียบราคาและกดอนุมัติ
        //public async Task<IActionResult> Review(int id) // id = PurchaseRequestId
        //{
        //    var data = await _api.GetRequestDetailAsync(id);

        //    // แปลงข้อมูลเพื่อแสดงใน View (Comparison Table)
        //    var model = new ComparisonViewModel
        //    {
        //        Title = data.Title,
        //        Quotations = data.Quotations, // รายการราคาของแต่ละเจ้า
        //        CurrentStepId = data.CurrentStepId
        //    };

        //    return View(model);
        //}

        //[HttpPost]
        //public async Task<IActionResult> Approve(int stepId, string comment)
        //{
        //    await _api.ApproveRequestAsync(stepId, comment, isApproved: true);
        //    return RedirectToAction("Index", "Home");
        //}

        //[HttpPost]
        //public async Task<IActionResult> Reject(int stepId, string comment)
        //{
        //    await _api.ApproveRequestAsync(stepId, comment, isApproved: false);
        //    return RedirectToAction("Index", "Home");
        //}
    }
}
