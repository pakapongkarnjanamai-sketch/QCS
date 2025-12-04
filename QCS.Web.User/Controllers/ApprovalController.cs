using Microsoft.AspNetCore.Mvc;
using QCS.Web.User.Models;
using QCS.Web.User.Services;

namespace QCS.Web.User.Controllers
{
    public class ApprovalController : Controller
    {
        private readonly IApiClientService _api;
        private readonly ILogger<ApprovalController> _logger;

        public ApprovalController(IApiClientService api, ILogger<ApprovalController> logger)
        {
            _api = api;
            _logger = logger;
        }

        // GET: Approval (อาจจะเป็นรายการรออนุมัติ - Pending List)
        public async Task<IActionResult> Index()
        {
            // ในอนาคตคุณอาจเพิ่ม Method GetPendingApprovalsAsync() ใน Service
            // เพื่อดึงเฉพาะงานที่รอ User คนนี้อนุมัติ
            // var tasks = await _api.GetPendingApprovalsAsync();
            // return View(tasks);

            return View(); // หรือ Redirect ไปหน้า Home Dashboard
        }

        // GET: Approval/Review/5
        // หน้าสำหรับผู้อนุมัติเข้าดูรายละเอียดและเปรียบเทียบราคา
        public async Task<IActionResult> Review(int id)
        {
            if (id <= 0) return BadRequest();

            try
            {
                // ดึงข้อมูลรายละเอียดใบขอซื้อ + ตารางเปรียบเทียบ
                var model = await _api.GetRequestDetailAsync(id);

                if (model == null)
                {
                    TempData["ErrorMessage"] = "ไม่พบข้อมูลเอกสาร หรือคุณไม่มีสิทธิ์เข้าถึง";
                    return RedirectToAction("Index", "Home");
                }

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading review page for request {Id}", id);
                return View("Error");
            }
        }

        // POST: Approval/Approve
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int stepId, string comment)
        {
            return await ProcessApproval(stepId, comment, true);
        }

        // POST: Approval/Reject
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int stepId, string comment)
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                // บังคับให้ใส่เหตุผลกรณี Reject
                TempData["ErrorMessage"] = "กรุณาระบุเหตุผลที่ไม่อนุมัติ (Comment)";
                return RedirectToAction("Review", new { id = GetRequestIdFromStep(stepId) });
                // หมายเหตุ: ในทางปฏิบัติเรามักจะส่ง ID เอกสารมาด้วยใน Form เพื่อให้ Redirect ถูก
            }

            return await ProcessApproval(stepId, comment, false);
        }

        // Helper Method
        private async Task<IActionResult> ProcessApproval(int stepId, string comment, bool isApproved)
        {
            try
            {
                var success = await _api.ApproveRequestAsync(stepId, comment, isApproved);

                if (success)
                {
                    string actionText = isApproved ? "อนุมัติ" : "ไม่อนุมัติ";
                    TempData["SuccessMessage"] = $"บันทึกผลการ{actionText}เรียบร้อยแล้ว";
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    TempData["ErrorMessage"] = "เกิดข้อผิดพลาดในการบันทึกข้อมูล โปรดลองใหม่อีกครั้ง";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing approval step {StepId}", stepId);
                TempData["ErrorMessage"] = "เกิดข้อผิดพลาดจากระบบ";
            }

            return RedirectToAction("Index", "Home");
        }

        // Mock function (ในใช้งานจริงควรส่ง RequestId มากับ Form Data)
        private int GetRequestIdFromStep(int stepId)
        {
            // Logic หา RequestId หรือให้ Redirect กลับไปหน้า Home แทน
            return 0;
        }
    }
}