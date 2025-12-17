using Microsoft.AspNetCore.Mvc;
using QCS.Domain.Enum;
using QCS.Infrastructure.Services;

namespace QCS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EnumController : ControllerBase
    {
        // API Endpoint: api/Enum/all
        // ดึงข้อมูล Enum ทั้งหมดในระบบทีเดียว
        [HttpGet("all")]
        public IActionResult GetAllEnums()
        {
            var data = new
            {
                RequestStatus = EnumHelper.ToList<RequestStatus>(),
                WorkflowStep = EnumHelper.ToList<WorkflowStep>(),
                DocumentType = EnumHelper.ToList<DocumentType>(),
                approvalStatus = EnumHelper.ToList<approvalStatus>()
            };

            return Ok(data);
        }

        // API Endpoint: api/Enum/RequestStatus
        // ดึงเฉพาะ RequestStatus (เผื่ออยากเรียกแยก)
        [HttpGet("RequestStatus")]
        public IActionResult GetRequestStatus()
        {
            return Ok(EnumHelper.ToList<RequestStatus>());
        }

        // API Endpoint: api/Enum/DocumentType
        // ดึงเฉพาะ DocumentType (เผื่ออยากเรียกแยก)
        [HttpGet("DocumentType")]
        public IActionResult GetDocumentType()
        {
            return Ok(EnumHelper.ToList<DocumentType>());
        }
    }
}