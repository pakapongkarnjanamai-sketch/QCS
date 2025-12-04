using Microsoft.AspNetCore.Mvc;
using QCS.Domain.Models;
using QCS.Infrastructure.Services;

namespace QCS.Api.Controllers
{

    public class RoleController : GenericController<Role>
    {
        public RoleController(IRepository<Role> repository, ILogger<GenericController<Role>> logger)
           : base(repository, logger)
        {

        }
    }

    public class UserRoleController : GenericController<UserRole>
    {
        public UserRoleController(IRepository<UserRole> repository, ILogger<GenericController<UserRole>> logger)
           : base(repository, logger)
        {

        }
    }
}
