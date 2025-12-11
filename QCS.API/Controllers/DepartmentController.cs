using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Mvc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QCS.Api.Controllers;
using QCS.Domain.Models;
using QCS.Infrastructure.Data;
using QCS.Infrastructure.Services;

namespace QCS.Api.Controllers
{
    public class DepartmentController : GenericController<Department>
    {
        private readonly AppDbContext _context;

        public DepartmentController(IRepository<Department> repository, ILogger<GenericController<Department>> logger, AppDbContext context)
            : base(repository, logger)
        {
            _context = context;
        }

        [HttpGet]
        public override object Get(DataSourceLoadOptions loadOptions)
        {
            try
            {
                // ใช้ IQueryable เพื่อให้ DataSourceLoader ทำงานที่ Database Level (Performance ดีกว่า)
                var departments = _repository.GetAll()
                    .Include(d => d.UserDepartments);

                return DataSourceLoader.Load(departments, loadOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading departments data");
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        // แก้ไข: เปลี่ยน Return Type เป็น async Task<IActionResult> ให้ตรงกับ Base Class
        public override async Task<IActionResult> GetById(int id)
        {
            try
            {
                var department = await _repository.GetAll()
                    .Include(d => d.UserDepartments)
                        .ThenInclude(ud => ud.User)
                    // แก้ไข: ใช้ FirstOrDefaultAsync เพื่อให้ทำงานแบบ Async จริงๆ
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (department == null)
                    return NotFound();

                return Ok(department);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department with id {Id}", id);
                return BadRequest(new { Message = ex.Message });
            }
        }
    }

    public class UserDepartmentController : GenericController<UserDepartment>
    {
        public UserDepartmentController(IRepository<UserDepartment> repository, ILogger<GenericController<UserDepartment>> logger)
            : base(repository, logger)
        {

        }
    }
}