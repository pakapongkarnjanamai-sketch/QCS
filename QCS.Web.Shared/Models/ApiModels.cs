namespace QCS.Web.Shared.Models
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public string? ErrorCode { get; set; }
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string NID { get; set; } = string.Empty;
        public string EmployeeID { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public DateTime LastLogin { get; set; }
        public List<RoleDto>? Roles { get; set; } = new();
        public string FullName => $"{FirstName} {LastName}".Trim();
    }

    public class RoleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }

    public class DocumentDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public class CreateUserRequest
    {
        public string WindowsIdentity { get; set; } = string.Empty;
    }

    public class AssignRoleRequest
    {
        public int UserId { get; set; }
        public int RoleId { get; set; }
    }
}
