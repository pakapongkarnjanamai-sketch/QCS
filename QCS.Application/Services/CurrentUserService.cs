using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace QCS.Application.Services
{
    public interface ICurrentUserService
    {
        // คืนค่า User Id เช่น "4734" (ตัด Domain ออกแล้ว)
        string UserId { get; }

        // คืนค่าชื่อเต็มจาก AD เช่น "DOMAIN\4734" (เผื่อต้องใช้)
        string FullName { get; }

        // เช็คว่า User Login อยู่จริงไหม
        bool IsAuthenticated { get; }
    }
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

                // กรณีไม่มี User Login หรือเป็น System ให้ return "SYSTEM" หรือค่าว่างตาม Business Rule
                if (user?.Identity?.IsAuthenticated != true)
                    return "SYSTEM";

                var fullName = user.Identity.Name; // ex: "NIKONOA\4734"
                if (string.IsNullOrEmpty(fullName))
                    return "SYSTEM";

                // Logic ตัด Domain: ถ้ามี Backslash ให้เอาข้างหลัง, ถ้าไม่มีให้เอาทั้งหมด
                var parts = fullName.Split('\\');
                return parts.Length > 1 ? parts[1] : parts[0];
            }
        }

        public string FullName => _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "SYSTEM";

        public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    }
}