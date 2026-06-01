using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QCS.API.Services;
using QCS.Domain.Models;
using QCS.Infrastructure.Data;

namespace QCS.API.Security
{
    public sealed class AdminAccessClaimsTransformation : IClaimsTransformation
    {
        private const string RootNId = "N4734";
        private readonly AppDbContext _context;
        private readonly IEmployeeLookupService _employeeLookupService;
        private readonly IMemoryCache _cache;

        public AdminAccessClaimsTransformation(
            AppDbContext context,
            IEmployeeLookupService employeeLookupService,
            IMemoryCache cache)
        {
            _context = context;
            _employeeLookupService = employeeLookupService;
            _cache = cache;
        }

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal.Identity?.IsAuthenticated != true)
            {
                return principal;
            }

            if (principal.HasClaim(c => c.Type == "qcs.admin.access.loaded"))
            {
                return principal;
            }

            var normalizedNId = ExtractNId(principal.Identity.Name);
            if (string.IsNullOrWhiteSpace(normalizedNId))
            {
                return principal;
            }

            AdminAccessLevel accessLevel;
            var hasAccess = false;

            if (string.Equals(normalizedNId, RootNId, StringComparison.OrdinalIgnoreCase))
            {
                hasAccess = true;
                accessLevel = AdminAccessLevel.SuperAdmin;
            }
            else
            {
                var user = await _context.AdminUserAccesses
                    .AsNoTracking()
                    .Where(x => x.NId == normalizedNId && x.IsActive)
                    .Select(x => new { x.AccessLevel })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    accessLevel = AdminAccessLevel.User;
                }
                else
                {
                    hasAccess = true;
                    accessLevel = user.AccessLevel;
                }
            }

            var identity = new ClaimsIdentity();
            identity.AddClaim(new Claim("qcs.admin.access.loaded", "true"));
            identity.AddClaim(new Claim("qcs.nid", normalizedNId));
            identity.AddClaim(new Claim("qcs.admin.access", hasAccess ? "true" : "false"));

            if (hasAccess)
            {
                foreach (var role in ExpandRoles(accessLevel))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                }
            }

            var fullName = await ResolveFullNameAsync(normalizedNId);
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                identity.AddClaim(new Claim("FullName", fullName));
            }

            principal.AddIdentity(identity);
            return principal;
        }

        private async Task<string?> ResolveFullNameAsync(string nId)
        {
            var cacheKey = $"emp:fullname:{nId}";
            if (_cache.TryGetValue(cacheKey, out string? cached))
                return cached;

            string? fullName = null;
            try
            {
                var employee = await _employeeLookupService.GetEmployeeByNIdAsync(nId);
                if (employee != null)
                    fullName = $"{employee.EnglishFirstName} {employee.EnglishLastName}".Trim();
            }
            catch
            {
                // Employee directory unavailable — fall back to NID
            }

            _cache.Set(cacheKey, fullName, TimeSpan.FromMinutes(30));
            return fullName;
        }

        private static string ExtractNId(string? identityName)
        {
            if (string.IsNullOrWhiteSpace(identityName))
            {
                return string.Empty;
            }

            var parts = identityName.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            return (parts.Length > 0 ? parts[^1] : identityName).Trim().ToUpperInvariant();
        }

        private static IEnumerable<string> ExpandRoles(AdminAccessLevel level)
        {
            yield return "User";

            if (level >= AdminAccessLevel.Manager)
            {
                yield return "Manager";
            }

            if (level >= AdminAccessLevel.Admin)
            {
                yield return "Admin";
            }

            if (level >= AdminAccessLevel.SuperAdmin)
            {
                yield return "SuperAdmin";
            }
        }
    }
}
