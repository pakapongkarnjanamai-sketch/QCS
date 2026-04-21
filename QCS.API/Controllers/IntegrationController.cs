using Microsoft.AspNetCore.Mvc;
using QCS.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using QCS.Application.Services;
using QCS.Domain.DTOs;
using QCS.Infrastructure.Services;

namespace QCS.API.Controllers
{
    /// <summary>
    /// Controller สำหรับให้โปรแกรมภายนอกเชื่อมต่อเพื่อดึงข้อมูล
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class IntegrationController : ControllerBase
    {
        private readonly IRequestService _requestService;
        private readonly IDateTime  _dateTime;
        public IntegrationController(IRequestService requestService, IDateTime dateTime)
        {
            _requestService = requestService;
            _dateTime = dateTime;
        }

        /// <summary>
        /// 1. GetRequestAll: ดึงข้อมูล List ของ Request ที่เป็น Status อนุมัติครบถ้วน
        /// </summary>
        /// <returns>List of Approved Requests</returns>
        [HttpGet("GetRequestAll")]
        public async Task<ActionResult<List<RequestGridDto>>> GetRequestAll()
        {
            try
            {
                // เรียกใช้ Query จาก Service เดิมที่มีอยู่แล้ว (GetApprovedListQuery) 
                // ซึ่งมีการ Filter Status = Approved (2) ไว้ให้แล้วใน Service
                var query = _requestService.GetApprovedListQuery();

                var result = await query.Where(a=>a.ValidUntil >= _dateTime.Now).ToListAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// 2. GetRequestByVendorCode: ดึงข้อมูล List ของ Request ที่เป็น Status อนุมัติครบถ้วน โดยหาจาก VendorCode
        /// </summary>
        /// <param name="vendorCode">รหัส Vendor ที่ต้องการค้นหา</param>
        /// <returns>List of Approved Requests filtered by VendorCode</returns>
        [HttpGet("GetRequestByVendorCode")]
        public async Task<ActionResult<List<RequestGridDto>>> GetRequestByVendorCode(string vendorCode)
        {
            if (string.IsNullOrWhiteSpace(vendorCode))
            {
                return BadRequest("VendorCode is required.");
            }

            try
            {
                // ใช้ Query เดิมและเพิ่มเงื่อนไขการกรอง VendorCode เข้าไป
                var query = _requestService.GetApprovedListQuery();

                var result = await query
                    .Where(r => r.VendorCode == vendorCode && r.ValidUntil >= _dateTime.Now)
                    .ToListAsync();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}