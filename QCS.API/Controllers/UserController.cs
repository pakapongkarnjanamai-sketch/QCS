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
        public override IActionResult Put(int key, string values)
        {
            try
            {
                var user = _repository.GetAll()
                    .Include(u => u.UserRoles)
                    .Include(u => u.UserDepartments)
                    .FirstOrDefault(u => u.Id == key);

                if (user == null)
                    return NotFound();

                // Parse the incoming data
                var updateData = JsonConvert.DeserializeObject<Dictionary<string, object>>(values);

                // Update basic user properties
                JsonConvert.PopulateObject(values, user);

                // Handle roleIds if present
                if (updateData.ContainsKey("roleIds"))
                {
                    var roleIdsJson = updateData["roleIds"]?.ToString();
                    List<int> newRoleIds = new List<int>();

                    if (!string.IsNullOrEmpty(roleIdsJson))
                    {
                        try
                        {
                            newRoleIds = JsonConvert.DeserializeObject<List<int>>(roleIdsJson) ?? new List<int>();
                        }
                        catch
                        {
                            if (int.TryParse(roleIdsJson, out int singleRoleId))
                            {
                                newRoleIds = new List<int> { singleRoleId };
                            }
                        }
                    }

                    // Remove existing user roles
                    var existingUserRoles = _context.UserRoles
                        .Where(ur => ur.UserId == user.Id)
                        .ToList();

                    _context.UserRoles.RemoveRange(existingUserRoles);

                    // Add new user roles
                    foreach (var roleId in newRoleIds)
                    {
                        var userRole = new UserRole
                        {
                            UserId = user.Id,
                            RoleId = roleId,
                            IsActive = true
                        };
                        _context.UserRoles.Add(userRole);
                    }
                }

                // Handle departmentIds if present
                if (updateData.ContainsKey("departmentIds"))
                {
                    var departmentIdsJson = updateData["departmentIds"]?.ToString();
                    List<int> newDepartmentIds = new List<int>();

                    if (!string.IsNullOrEmpty(departmentIdsJson))
                    {
                        try
                        {
                            newDepartmentIds = JsonConvert.DeserializeObject<List<int>>(departmentIdsJson) ?? new List<int>();
                        }
                        catch
                        {
                            if (int.TryParse(departmentIdsJson, out int singleDepartmentId))
                            {
                                newDepartmentIds = new List<int> { singleDepartmentId };
                            }
                        }
                    }

                    // Remove existing user departments
                    var existingUserDepartments = _context.UserDepartments
                        .Where(ud => ud.UserId == user.Id)
                        .ToList();

                    _context.UserDepartments.RemoveRange(existingUserDepartments);

                    // Add new user departments
                    foreach (var departmentId in newDepartmentIds)
                    {
                        var userDepartment = new UserDepartment
                        {
                            UserId = user.Id,
                            DepartmentId = departmentId,
                            IsActive = true
                        };
                        _context.UserDepartments.Add(userDepartment);
                    }
                }

                // Update user
                _repository.Update(user);

                // Save changes
                _context.SaveChanges();

                // Reload user with updated roles and departments
                var updatedUser = _repository.GetAll()
                    .Include(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                    .Include(u => u.UserDepartments)
                        .ThenInclude(ud => ud.Department)
                    .FirstOrDefault(u => u.Id == key);

                return Ok(updatedUser);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user with ID {UserId}", key);
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPost]
        public override IActionResult Post(string values)
        {
            try
            {
                var createData = JsonConvert.DeserializeObject<Dictionary<string, object>>(values);
                var user = JsonConvert.DeserializeObject<User>(values);

                // Set default values
                user.CreatedAt = _dateTime.Now;
                user.IsActive = true;

                // Add user using DbContext
                _context.Users.Add(user);
                _context.SaveChanges(); // Save to get the ID

                // Handle roleIds if present
                if (createData.ContainsKey("roleIds"))
                {
                    var roleIdsJson = createData["roleIds"]?.ToString();
                    List<int> roleIds = new List<int>();

                    if (!string.IsNullOrEmpty(roleIdsJson))
                    {
                        try
                        {
                            roleIds = JsonConvert.DeserializeObject<List<int>>(roleIdsJson) ?? new List<int>();
                        }
                        catch
                        {
                            if (int.TryParse(roleIdsJson, out int singleRoleId))
                            {
                                roleIds = new List<int> { singleRoleId };
                            }
                        }
                    }

                    // Add user roles
                    foreach (var roleId in roleIds)
                    {
                        var userRole = new UserRole
                        {
                            UserId = user.Id,
                            RoleId = roleId,
                            IsActive = true
                        };
                        _context.UserRoles.Add(userRole);
                    }
                }

                // Handle departmentIds if present
                if (createData.ContainsKey("departmentIds"))
                {
                    var departmentIdsJson = createData["departmentIds"]?.ToString();
                    List<int> departmentIds = new List<int>();

                    if (!string.IsNullOrEmpty(departmentIdsJson))
                    {
                        try
                        {
                            departmentIds = JsonConvert.DeserializeObject<List<int>>(departmentIdsJson) ?? new List<int>();
                        }
                        catch
                        {
                            if (int.TryParse(departmentIdsJson, out int singleDepartmentId))
                            {
                                departmentIds = new List<int> { singleDepartmentId };
                            }
                        }
                    }

                    // Add user departments
                    foreach (var departmentId in departmentIds)
                    {
                        var userDepartment = new UserDepartment
                        {
                            UserId = user.Id,
                            DepartmentId = departmentId,
                            IsActive = true
                        };
                        _context.UserDepartments.Add(userDepartment);
                    }
                }

                _context.SaveChanges();

                // Reload user with roles and departments
                var userWithRelations = _context.Users
                    .Include(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                    .Include(u => u.UserDepartments)
                        .ThenInclude(ud => ud.Department)
                    .FirstOrDefault(u => u.Id == user.Id);

                return Ok(userWithRelations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return BadRequest(new { Message = ex.Message });
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
