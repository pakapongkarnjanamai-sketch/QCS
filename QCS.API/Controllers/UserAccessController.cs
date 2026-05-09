using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QCS.API.Services;
using QCS.Domain.Models;
using QCS.Infrastructure.Data;

namespace QCS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    public class UserAccessController : ControllerBase
    {
        private const string RootNId = "N4734";

        private readonly AppDbContext _context;
        private readonly IEmployeeLookupService _employeeLookupService;

        public UserAccessController(AppDbContext context, IEmployeeLookupService employeeLookupService)
        {
            _context = context;
            _employeeLookupService = employeeLookupService;
        }

        [HttpGet("Grid")]
        public async Task<object> Grid(DataSourceLoadOptions loadOptions, CancellationToken cancellationToken)
        {
            await EnsureRootUserAsync(cancellationToken);

            var query = _context.AdminUserAccesses
                .AsNoTracking()
                .Select(u => new
                {
                    u.Id,
                    u.NId,
                    fullName = (u.EnglishFirstName + " " + u.EnglishLastName).Trim(),
                    u.EmployeeId,
                    u.Division,
                    u.Department,
                    u.Section,
                    u.Position,
                    u.CostCenter,
                    u.Email,
                    accessLevel = u.AccessLevel.ToString(),
                    u.IsActive,
                    u.LastSyncedAt,
                });

            return DataSourceLoader.Load(query, loadOptions);
        }

        [HttpPost("Register")]
        public async Task<IActionResult> Register([FromBody] RegisterUserAccessRequest request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.NId))
            {
                return Problem("NID is required.", statusCode: StatusCodes.Status400BadRequest);
            }

            var normalizedNId = NormalizeNId(request.NId);
            var requestedLevel = ParseAccessLevel(request.AccessLevel);
            var effectiveLevel = string.Equals(normalizedNId, RootNId, StringComparison.OrdinalIgnoreCase)
                ? AdminAccessLevel.SuperAdmin
                : requestedLevel;

            var employee = await _employeeLookupService.GetEmployeeByNIdAsync(normalizedNId, cancellationToken);
            if (employee == null)
            {
                return Problem($"NID '{normalizedNId}' was not found in EmployeeLookup/GetFull.", statusCode: StatusCodes.Status404NotFound);
            }

            var user = await _context.AdminUserAccesses
                .FirstOrDefaultAsync(u => u.NId == normalizedNId, cancellationToken);

            if (user == null)
            {
                user = new AdminUserAccess
                {
                    NId = normalizedNId,
                };
                await _context.AdminUserAccesses.AddAsync(user, cancellationToken);
            }

            ApplyEmployeeProfile(user, employee);
            user.AccessLevel = effectiveLevel;
            user.IsActive = true;
            user.LastSyncedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            await EnsureRootUserAsync(cancellationToken);

            return Ok(new { success = true });
        }

        [HttpGet("Preview")]
        public async Task<IActionResult> Preview([FromQuery] string nId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(nId))
            {
                return Problem("NID is required.", statusCode: StatusCodes.Status400BadRequest);
            }

            var normalizedNId = NormalizeNId(nId);
            var employee = await _employeeLookupService.GetEmployeeByNIdAsync(normalizedNId, cancellationToken);
            if (employee == null)
            {
                return Problem($"NID '{normalizedNId}' was not found in EmployeeLookup/GetFull.", statusCode: StatusCodes.Status404NotFound);
            }

            return Ok(CreatePreviewResponse(employee));
        }

        [HttpPut("{id:int}/AccessLevel")]
        public async Task<IActionResult> UpdateAccessLevel(int id, [FromBody] UpdateUserAccessLevelRequest request, CancellationToken cancellationToken)
        {
            var user = await _context.AdminUserAccesses.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
            if (user == null)
            {
                return NotFound($"UserAccess id '{id}' not found.");
            }

            user.AccessLevel = string.Equals(user.NId, RootNId, StringComparison.OrdinalIgnoreCase)
                ? AdminAccessLevel.SuperAdmin
                : ParseAccessLevel(request.AccessLevel);

            user.IsActive = request.IsActive;
            user.LastSyncedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            await EnsureRootUserAsync(cancellationToken);

            return Ok(new { success = true });
        }

        [HttpPost("{id:int}/Refresh")]
        public async Task<IActionResult> RefreshFromLookup(int id, CancellationToken cancellationToken)
        {
            var user = await _context.AdminUserAccesses.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
            if (user == null)
            {
                return NotFound($"UserAccess id '{id}' not found.");
            }

            var employee = await _employeeLookupService.GetEmployeeByNIdAsync(user.NId, cancellationToken);
            if (employee == null)
            {
                return Problem($"NID '{user.NId}' was not found in EmployeeLookup/GetFull.", statusCode: StatusCodes.Status404NotFound);
            }

            ApplyEmployeeProfile(user, employee);
            user.LastSyncedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new { success = true });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            var user = await _context.AdminUserAccesses.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
            if (user == null)
            {
                return NotFound($"UserAccess id '{id}' not found.");
            }

            if (string.Equals(user.NId, RootNId, StringComparison.OrdinalIgnoreCase))
            {
                return Problem("N4734 is fixed as SuperAdmin and cannot be deleted.", statusCode: StatusCodes.Status400BadRequest);
            }

            _context.AdminUserAccesses.Remove(user);
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new { success = true });
        }

        private async Task EnsureRootUserAsync(CancellationToken cancellationToken)
        {
            var normalized = RootNId.ToUpperInvariant();
            var root = await _context.AdminUserAccesses.FirstOrDefaultAsync(u => u.NId == normalized, cancellationToken);

            if (root == null)
            {
                root = new AdminUserAccess
                {
                    NId = normalized,
                    IsActive = true,
                };

                var employee = await _employeeLookupService.GetEmployeeByNIdAsync(normalized, cancellationToken);
                if (employee != null)
                {
                    ApplyEmployeeProfile(root, employee);
                }

                root.AccessLevel = AdminAccessLevel.SuperAdmin;
                root.LastSyncedAt = DateTime.UtcNow;

                await _context.AdminUserAccesses.AddAsync(root, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            if (root.AccessLevel != AdminAccessLevel.SuperAdmin || !root.IsActive)
            {
                root.AccessLevel = AdminAccessLevel.SuperAdmin;
                root.IsActive = true;
                root.LastSyncedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        private static void ApplyEmployeeProfile(AdminUserAccess target, EmployeeFullItem source)
        {
            target.NId = NormalizeNId(source.NId);
            target.EmployeeId = source.EId?.Trim() ?? string.Empty;
            target.EnglishFirstName = source.EnglishFirstName?.Trim() ?? string.Empty;
            target.EnglishLastName = source.EnglishLastName?.Trim() ?? string.Empty;
            target.Division = source.Division?.Trim() ?? string.Empty;
            target.Department = source.Department?.Trim() ?? string.Empty;
            target.Section = source.Section?.Trim() ?? string.Empty;
            target.Position = source.Position?.Trim() ?? string.Empty;
            target.CostCenter = source.CostCenter?.Trim() ?? string.Empty;
            target.Email = source.Email?.Trim() ?? string.Empty;
        }

        private static UserAccessPreviewResponse CreatePreviewResponse(EmployeeFullItem source)
        {
            var normalizedNId = NormalizeNId(source.NId);
            var fullName = string.Join(" ", new[]
            {
                source.EnglishFirstName?.Trim(),
                source.EnglishLastName?.Trim(),
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

            return new UserAccessPreviewResponse
            {
                NId = normalizedNId,
                EmployeeId = source.EId?.Trim() ?? string.Empty,
                FullName = fullName,
                Division = source.Division?.Trim() ?? string.Empty,
                Department = source.Department?.Trim() ?? string.Empty,
                Section = source.Section?.Trim() ?? string.Empty,
                Position = source.Position?.Trim() ?? string.Empty,
                CostCenter = source.CostCenter?.Trim() ?? string.Empty,
                Email = source.Email?.Trim() ?? string.Empty,
            };
        }

        private static string NormalizeNId(string nId)
        {
            return nId.Trim().ToUpperInvariant();
        }

        private static AdminAccessLevel ParseAccessLevel(string? value)
        {
            if (Enum.TryParse<AdminAccessLevel>(value, true, out var level))
            {
                return level;
            }

            return AdminAccessLevel.User;
        }
    }

    public sealed class RegisterUserAccessRequest
    {
        public string NId { get; set; } = string.Empty;
        public string AccessLevel { get; set; } = nameof(AdminAccessLevel.User);
    }

    public sealed class UpdateUserAccessLevelRequest
    {
        public string AccessLevel { get; set; } = nameof(AdminAccessLevel.User);
        public bool IsActive { get; set; } = true;
    }

    public sealed class UserAccessPreviewResponse
    {
        public string NId { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string CostCenter { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
