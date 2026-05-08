using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace QCS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SessionController : ControllerBase
    {
        [HttpGet("Me")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult GetCurrentUser()
        {
            var fullName = User.FindFirst("FullName")?.Value;
            var windowsIdentity = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name;

            var displayName = !string.IsNullOrWhiteSpace(fullName)
                ? fullName
                : NormalizeWindowsIdentity(windowsIdentity);

            return Ok(new
            {
                displayName,
                windowsIdentity = windowsIdentity ?? string.Empty,
                isAuthenticated = User.Identity?.IsAuthenticated == true
            });
        }

        private static string NormalizeWindowsIdentity(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Unknown User";
            }

            var normalized = value.Trim();
            var separatorIndex = normalized.IndexOf('\\');
            if (separatorIndex >= 0 && separatorIndex < normalized.Length - 1)
            {
                return normalized[(separatorIndex + 1)..];
            }

            return normalized;
        }
    }
}
