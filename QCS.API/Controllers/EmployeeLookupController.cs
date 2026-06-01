using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QCS.API.Services;

namespace QCS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EmployeeLookupController : ControllerBase
    {
        private readonly IEmployeeLookupService _employeeLookupService;

        public EmployeeLookupController(IEmployeeLookupService employeeLookupService)
        {
            _employeeLookupService = employeeLookupService;
        }

        [HttpGet("GetDepartment")]
        public async Task<object> GetDepartment(DataSourceLoadOptions loadOptions, [FromQuery] string[]? nIds)
        {
            var items = await _employeeLookupService.GetDepartmentItemsAsync();

            var normalizedNIds = (nIds ?? Array.Empty<string>())
                .Where(nId => !string.IsNullOrWhiteSpace(nId))
                .Select(nId => nId.Trim().ToUpper())
                .ToHashSet();

            var query = items
                .Where(item => normalizedNIds.Count == 0 || normalizedNIds.Contains(item.NId))
                .Select(item => new
                {
                    id = item.NId,
                    name = item.DepartmentName,
                })
                .AsQueryable();

            return DataSourceLoader.Load(query, loadOptions);
        }

        [HttpGet("FullName/{nId}")]
        public async Task<IActionResult> GetFullName(string nId)
        {
            var employee = await _employeeLookupService.GetEmployeeByNIdAsync(nId);
            if (employee == null)
                return Ok(new { fullName = "" });

            var fullName = $"{employee.EnglishFirstName} {employee.EnglishLastName}".Trim();
            return Ok(new { fullName });
        }
    }
}