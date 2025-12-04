using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QCS.Domain.Models
{
    public class User : BaseEntity
    {
        [Required]
        public string NID { get; set; } = string.Empty;
        public string EmployeeID { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public DateTime LastLogin { get; set; }

        // Navigation properties
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public virtual ICollection<UserDepartment> UserDepartments { get; set; } = new List<UserDepartment>();
    }

    public class Role : BaseEntity
    {
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Description { get; set; }

        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }

    public class UserRole : BaseEntity
    {
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; } = null!;

        public int RoleId { get; set; }

        [ForeignKey("RoleId")]
        public virtual Role? Role { get; set; } = null!;


    }

    public class UserDepartment : BaseEntity
    {
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; } = null!;

        public int DepartmentId { get; set; }

        [ForeignKey("DepartmentId")]
        public virtual Department? Department { get; set; } = null!;

    }
    public class Department : BaseEntity
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        // Navigation properties
        public virtual ICollection<UserDepartment> UserDepartments { get; set; } = new List<UserDepartment>();
        //public virtual ICollection<DocumentDepartment> DocumentDepartments { get; set; } = new List<DocumentDepartment>();
    }
}
