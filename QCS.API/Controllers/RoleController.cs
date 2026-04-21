using QCS.Application.Services;
using QCS.Application.Abstractions;
using QCS.Domain.Models;

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
