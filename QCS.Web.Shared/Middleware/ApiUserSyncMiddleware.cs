
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using QCS.Web.Shared.Services;
using System.Security.Claims;

namespace QCS.Web.Shared.Middleware
{
    public class ApiUserSyncMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiUserSyncMiddleware> _logger;
        private readonly IMemoryCache _cache;

        public ApiUserSyncMiddleware(RequestDelegate next, ILogger<ApiUserSyncMiddleware> logger, IMemoryCache cache)
        {
            _next = next;
            _logger = logger;
            _cache = cache;
        }

        public async Task InvokeAsync(HttpContext context, IApiUserService apiUserService)
        {
            // Skip สำหรับ static files เท่านั้น
            if (ShouldSkipMiddleware(context))
            {
                await _next(context);
                return;
            }

            if (context.User.Identity?.IsAuthenticated == true)
            {
                var windowsIdentity = context.User.Identity.Name;
                if (!string.IsNullOrEmpty(windowsIdentity) &&
                    windowsIdentity.StartsWith("NIKONOA\\", StringComparison.OrdinalIgnoreCase))
                {
                    var cacheKey = $"user_data_{windowsIdentity}";

                    // ตรวจสอบว่า User มี Role claims หรือไม่
                    var hasRoleClaims = context.User.Claims.Any(c => c.Type == ClaimTypes.Role);

                    // ถ้าไม่มี Role claims หรือ cache หมดอายุ ให้ sync ใหม่
                    if (!hasRoleClaims || !_cache.TryGetValue(cacheKey, out var cachedUserData))
                    {
                        _logger.LogInformation("Syncing user data for: {WindowsIdentity}, HasRoles: {HasRoles}",
                            windowsIdentity, hasRoleClaims);

                        try
                        {
                            var userResponse = await apiUserService.GetOrCreateUserAsync(windowsIdentity);
                            if (userResponse.Success && userResponse.Data != null)
                            {
                                var user = userResponse.Data;

                                // สร้าง Claims ใหม่ทั้งหมด
                                var claims = new List<Claim>
                                {
                                    new Claim(ClaimTypes.Name, windowsIdentity),
                                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                                    new Claim("UserId", user.Id.ToString()),
                                    new Claim("FullName", user.FullName ?? ""),
                                    new Claim("Email", user.Email ?? "")
                                };

                                // เพิ่ม Role Claims
                                foreach (var role in user.Roles)
                                {
                                    claims.Add(new Claim(ClaimTypes.Role, role.Name));
                                    _logger.LogInformation("Added role claim: {RoleName} for user: {WindowsIdentity}",
                                        role.Name, windowsIdentity);
                                }

                                // สร้าง ClaimsIdentity และ ClaimsPrincipal ใหม่
                                var claimsIdentity = new ClaimsIdentity(claims, context.User.Identity.AuthenticationType);
                                var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

                                // แทนที่ User ใน HttpContext
                                context.User = claimsPrincipal;

                                // Cache user data for 10 minutes
                                _cache.Set(cacheKey, user, TimeSpan.FromMinutes(10));

                                _logger.LogInformation("User {WindowsIdentity} synced successfully with {RoleCount} roles: {Roles}",
                                    windowsIdentity, user.Roles.Count, string.Join(", ", user.Roles.Select(r => r.Name)));
                            }
                            else
                            {
                                _logger.LogWarning("Failed to sync user data for: {WindowsIdentity}", windowsIdentity);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error syncing Windows user via API: {WindowsIdentity}", windowsIdentity);
                        }
                    }
                    else
                    {
                        // ถ้ามี cache และมี role claims แล้ว ให้ใช้ข้อมูลจาก cache
                        if (cachedUserData != null)
                        {
                            var user = cachedUserData as dynamic;

                            // ตรวจสอบว่า current user มี claims ครบหรือไม่
                            var currentRoles = context.User.Claims
                                .Where(c => c.Type == ClaimTypes.Role)
                                .Select(c => c.Value)
                                .ToList();

                            _logger.LogInformation("Using cached data for: {WindowsIdentity}, Current roles: {CurrentRoles}",
                                windowsIdentity, string.Join(", ", currentRoles));
                        }
                    }
                }
            }

            await _next(context);
        }

        private static bool ShouldSkipMiddleware(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant();

            // Skip เฉพาะ static files
            if (path != null && (
                path.StartsWith("/_framework/") ||
                path.StartsWith("/css/") ||
                path.StartsWith("/js/") ||
                path.StartsWith("/lib/") ||
                path.StartsWith("/images/") ||
                path.StartsWith("/favicon.ico") ||
                path.EndsWith(".css") ||
                path.EndsWith(".js") ||
                path.EndsWith(".png") ||
                path.EndsWith(".jpg") ||
                path.EndsWith(".jpeg") ||
                path.EndsWith(".gif") ||
                path.EndsWith(".svg") ||
                path.EndsWith(".ico") ||
                path.EndsWith(".woff") ||
                path.EndsWith(".woff2") ||
                path.EndsWith(".ttf") ||
                path.EndsWith(".eot")))
            {
                return true;
            }

            return false;
        }
    }
}
