using Microsoft.AspNetCore.Http;
using QCS.Application.Abstractions;

namespace QCS.Infrastructure.Services
{
    /// <summary>
    /// HttpContext-based implementation of <see cref="ICurrentUserService"/>.
    /// </summary>
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string UserId
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;

                if (user?.Identity?.IsAuthenticated != true)
                    return "SYSTEM";

                var fullName = user.Identity.Name;
                if (string.IsNullOrEmpty(fullName))
                    return "SYSTEM";

                var parts = fullName.Split('\\');
                return parts.Length > 1 ? parts[1] : parts[0];
            }
        }

        public string FullName => _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "SYSTEM";

        public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    }
}
