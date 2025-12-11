using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Mvc;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using QCS.Api.Controllers;
using QCS.Application.Services;
using QCS.Domain.Models;
using QCS.Infrastructure.Data;
using QCS.Infrastructure.Services;
using QCS.Web.Shared.Models;

namespace QCS.API.Controllers
{
    public class UsersController : GenericController<User>
    {
        private readonly AppDbContext _context;
        public readonly IDateTime _dateTime;
        public UsersController(IRepository<User> repository, ILogger<GenericController<User>> logger, AppDbContext context,IDateTime dateTime)
           : base(repository, logger)
        {
            _dateTime = dateTime;
            _context = context;
        }
        [HttpGet]
        public override object Get(DataSourceLoadOptions loadOptions)
        {
            try
            {
                var users = _repository.GetAll()
                    .Include(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                    .Include(u => u.UserDepartments)
                        .ThenInclude(ud => ud.Department)
                    .ToList();

                return DataSourceLoader.Load(users.AsQueryable(), loadOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading users data");
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPut]
        public override async Task<IActionResult> Put(int key, [FromForm] string values) // แก้ไข: เพิ่ม async Task และ [FromForm] ให้ตรง Base
        {
            try
            {
                var user = await _repository.GetAll()
                    .Include(u => u.UserRoles)
                    .Include(u => u.UserDepartments)
                    .FirstOrDefaultAsync(u => u.Id == key); // แก้ไข: ใช้ Async

                if (user == null)
                    return NotFound();

                var updateData = JsonConvert.DeserializeObject<Dictionary<string, object>>(values);
                JsonConvert.PopulateObject(values, user);

                // --- Handle Roles ---
                if (updateData.ContainsKey("roleIds"))
                {
                    var roleIdsJson = updateData["roleIds"]?.ToString();
                    List<int> newRoleIds = ParseIdsList(roleIdsJson);

                    // Remove existing
                    var existingUserRoles = _context.UserRoles.Where(ur => ur.UserId == user.Id);
                    _context.UserRoles.RemoveRange(existingUserRoles);

                    // Add new
                    foreach (var roleId in newRoleIds)
                    {
                        await _context.UserRoles.AddAsync(new UserRole { UserId = user.Id, RoleId = roleId, IsActive = true });
                    }
                }

                // --- Handle Departments ---
                if (updateData.ContainsKey("departmentIds"))
                {
                    var departmentIdsJson = updateData["departmentIds"]?.ToString();
                    List<int> newDepartmentIds = ParseIdsList(departmentIdsJson);

                    var existingUserDepartments = _context.UserDepartments.Where(ud => ud.UserId == user.Id);
                    _context.UserDepartments.RemoveRange(existingUserDepartments);

                    foreach (var deptId in newDepartmentIds)
                    {
                        await _context.UserDepartments.AddAsync(new UserDepartment { UserId = user.Id, DepartmentId = deptId, IsActive = true });
                    }
                }

                // แก้ไข: ใช้ UpdateAsync แทน Update
                await _repository.UpdateAsync(user);
                await _context.SaveChangesAsync(); // แก้ไข: ใช้ SaveChangesAsync

                // Reload data for response
                var updatedUser = await _repository.GetAll()
                    .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                    .Include(u => u.UserDepartments).ThenInclude(ud => ud.Department)
                    .FirstOrDefaultAsync(u => u.Id == key);

                return Ok(updatedUser);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user with ID {UserId}", key);
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPost]
        public override async Task<IActionResult> Post([FromForm] string values) // แก้ไข: เพิ่ม async Task และ [FromForm]
        {
            try
            {
                var createData = JsonConvert.DeserializeObject<Dictionary<string, object>>(values);
                var user = JsonConvert.DeserializeObject<User>(values);

                user.CreatedAt = _dateTime.Now;
                user.IsActive = true;

                await _repository.AddAsync(user); // แก้ไข: ใช้ AddAsync ของ Repository
                await _repository.SaveChangesAsync(); // Save เพื่อให้ได้ User.Id มาใช้ต่อ

                // --- Handle Roles ---
                if (createData.ContainsKey("roleIds"))
                {
                    var roleIdsJson = createData["roleIds"]?.ToString();
                    List<int> roleIds = ParseIdsList(roleIdsJson);

                    foreach (var roleId in roleIds)
                    {
                        await _context.UserRoles.AddAsync(new UserRole { UserId = user.Id, RoleId = roleId, IsActive = true });
                    }
                }

                // --- Handle Departments ---
                if (createData.ContainsKey("departmentIds"))
                {
                    var departmentIdsJson = createData["departmentIds"]?.ToString();
                    List<int> departmentIds = ParseIdsList(departmentIdsJson);

                    foreach (var deptId in departmentIds)
                    {
                        await _context.UserDepartments.AddAsync(new UserDepartment { UserId = user.Id, DepartmentId = deptId, IsActive = true });
                    }
                }

                if (createData.ContainsKey("roleIds") || createData.ContainsKey("departmentIds"))
                {
                    await _context.SaveChangesAsync();
                }

                var userWithRelations = await _repository.GetAll()
                    .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                    .Include(u => u.UserDepartments).ThenInclude(ud => ud.Department)
                    .FirstOrDefaultAsync(u => u.Id == user.Id);

                return Ok(userWithRelations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return BadRequest(new { Message = ex.Message });
            }
        }
        // Helper Method เพื่อลดโค้ดซ้ำในการ Parse JSON List
        private List<int> ParseIdsList(string? json)
        {
            if (string.IsNullOrEmpty(json)) return new List<int>();
            try
            {
                return JsonConvert.DeserializeObject<List<int>>(json) ?? new List<int>();
            }
            catch
            {
                if (int.TryParse(json, out int singleId)) return new List<int> { singleId };
                return new List<int>();
            }
        }
        [HttpPost("windows-auth")]
        public async Task<ActionResult<ApiResponse<UserDto>>> GetOrCreateUserFromWindows([FromBody] CreateUserRequest request)
        {
            try
            {
                // 1. Validate input และ extract NID
                if (string.IsNullOrWhiteSpace(request?.WindowsIdentity))
                {
                    return BadRequest(new ApiResponse<UserDto>
                    {
                        Success = false,
                        Message = "Windows identity is required"
                    });
                }

                string nid = request.WindowsIdentity.Split('\\').LastOrDefault();
                if (string.IsNullOrWhiteSpace(nid))
                {
                    return BadRequest(new ApiResponse<UserDto>
                    {
                        Success = false,
                        Message = "Invalid Windows identity format"
                    });
                }

                // 2. Query optimization - เลือกเฉพาะ field ที่ต้องการ
                var user = await _repository.GetAll()
                    .Where(u => u.NID == nid)
                    .Select(u => new
                    {
                        u.Id,
                        u.NID,
                        u.EmployeeID,
                        u.FirstName,
                        u.LastName,
                        u.Email,
                        u.PhoneNumber,
                        u.LastLogin,
                        Roles = u.UserRoles.Select(ur => new RoleDto
                        {
                            Id = ur.Role.Id,
                            Name = ur.Role.Name,
                            Description = ur.Role.Description,
                            IsActive = ur.Role.IsActive
                        }).ToList()
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound(new ApiResponse<UserDto>
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                // 3. Update LastLogin แยกต่างหาก (เพื่อ performance)
                await _repository.GetAll()
                    .Where(u => u.NID == nid)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.LastLogin, _dateTime.Now));

                // 4. Create UserDto โดยใช้ AutoMapper หรือ direct mapping
                var userDto = new UserDto
                {
                    Id = user.Id,
                    NID = user.NID,
                    EmployeeID = user.EmployeeID,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    LastLogin = _dateTime.Now,
                    Roles = user.Roles
                };

                return Ok(new ApiResponse<UserDto>
                {
                    Success = true,
                    Data = userDto,
                    Message = "User retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrCreateUserFromWindows for identity: {Identity}",
                    request?.WindowsIdentity);

                return StatusCode(500, new ApiResponse<UserDto>
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }
        [HttpGet("by-nid/{nid}")]
        public IActionResult GetUserByNid(string nid)
        {
            var user = _context.Users
                .Where(u => u.NID == nid)
                .Select(u => new {
                    u.Id,
                    u.NID,
                    FullName = u.FirstName + " " + u.LastName,
                    u.Email
                })
                .FirstOrDefault();

            if (user == null)
                return NotFound(new { message = "User not found" });

            return Ok(user);
        }

        [HttpGet("by-role/{roleId}")]
        public IActionResult GetUsersByRole(int roleId)
        {
            var users = _context.UserRoles
                .Where(ur => ur.RoleId == roleId)
                .Select(ur => new {
                    ur.User.Id,
                    ur.User.NID,
                    FullName = ur.User.FirstName + " " + ur.User.LastName
                })
                .ToList();

            return Ok(users);
        }

        [HttpGet("current")]
        public IActionResult GetCurrentUser()
        {
            var userName = User.Identity?.Name ?? "";
            var normalizedUserName = userName.ToLower();

            var user = _context.Users
                .Where(u => u.NID.ToLower() == normalizedUserName)
                .Select(u => new {
                    u.Id,
                    u.NID,
                    FullName = u.FirstName + " " + u.LastName
                })
                .FirstOrDefault();

            return Ok(user);
        }
    }

}
