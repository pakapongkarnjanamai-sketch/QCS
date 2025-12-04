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
        [HttpPost("windows-auth")]
        public async Task<ActionResult<ApiResponse<UserDto>>> GetOrCreateUserFromWindows([FromBody] CreateUserRequest request)
        {
            var userDto = new UserDto
            {
                Id = 1,
                NID = "",
                EmployeeID = "",
                FirstName = "",
                LastName = "",
                Email = "",
                PhoneNumber = "",
                LastLogin = DateTime.UtcNow,
                
            };

            return Ok(new ApiResponse<UserDto>
            {
                Success = true,
                Data = userDto,
                Message = "User retrieved successfully"
            });
        }
    }

}
