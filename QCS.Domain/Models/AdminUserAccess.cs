using System.ComponentModel.DataAnnotations;

namespace QCS.Domain.Models
{
    public enum AdminAccessLevel
    {
        User = 10,
        Manager = 20,
        Admin = 30,
        SuperAdmin = 40,
    }

    public class AdminUserAccess : BaseEntity
    {
        [Required]
        [StringLength(50)]
        public string NId { get; set; } = string.Empty;

        [StringLength(50)]
        public string EmployeeId { get; set; } = string.Empty;

        [StringLength(100)]
        public string EnglishFirstName { get; set; } = string.Empty;

        [StringLength(100)]
        public string EnglishLastName { get; set; } = string.Empty;

        [StringLength(200)]
        public string Division { get; set; } = string.Empty;

        [StringLength(200)]
        public string Department { get; set; } = string.Empty;

        [StringLength(200)]
        public string Section { get; set; } = string.Empty;

        [StringLength(200)]
        public string Position { get; set; } = string.Empty;

        [StringLength(100)]
        public string CostCenter { get; set; } = string.Empty;

        [EmailAddress]
        [StringLength(200)]
        public string Email { get; set; } = string.Empty;

        public AdminAccessLevel AccessLevel { get; set; } = AdminAccessLevel.User;

        public DateTime LastSyncedAt { get; set; }
    }
}
