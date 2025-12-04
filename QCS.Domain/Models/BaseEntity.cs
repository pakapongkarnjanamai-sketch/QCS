using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QCS.Domain.Models
{
    public abstract class BaseEntity
    {
        [Key]
        public int Id { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        [StringLength(100)]
        public string? CreatedBy { get; set; }
        [StringLength(100)]
        public string? UpdatedBy { get; set; }
    }
}
