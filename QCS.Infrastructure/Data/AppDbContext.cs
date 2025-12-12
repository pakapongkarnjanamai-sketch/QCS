using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;


using QCS.Domain.Models;
using QCS.Infrastructure.Services;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace QCS.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        private readonly IDateTime _dateTime;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AppDbContext(DbContextOptions<AppDbContext> options, IDateTime dateTime, IHttpContextAccessor httpContextAccessor)
            : base(options)
        {
            _dateTime = dateTime;
            _httpContextAccessor = httpContextAccessor;
        }
        public DbSet<ApprovalStep> ApprovalSteps { get; set; }
        public DbSet<PurchaseRequest> PurchaseRequests { get; set; }
        public DbSet<Quotation> Quotations { get; set; }
        public DbSet<AttachmentFile> AttachmentFiles { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<UserDepartment> UserDepartments { get; set; }

        public DbSet<User> Users { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

        }
        public override int SaveChanges()
        {
            var entries = ChangeTracker.Entries<BaseEntity>();
            foreach (var entry in entries)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = _dateTime.Now;
                        entry.Entity.CreatedBy = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
                        break;
                    case EntityState.Modified:
                        entry.Entity.UpdatedAt = _dateTime.Now;
                        entry.Entity.UpdatedBy = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
                        break;
                }
            }
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var entries = ChangeTracker.Entries<BaseEntity>();
            foreach (var entry in entries)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = _dateTime.Now;
                        entry.Entity.CreatedBy = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
                        break;
                    case EntityState.Modified:
                        entry.Entity.UpdatedAt = _dateTime.Now;
                        entry.Entity.UpdatedBy = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
                        break;
                }
            }
            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
