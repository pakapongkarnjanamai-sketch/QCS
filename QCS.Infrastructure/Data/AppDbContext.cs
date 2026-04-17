using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using QCS.Application.Services;
using QCS.Domain.Models;
using QCS.Infrastructure.Services;

namespace QCS.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        private readonly IDateTime _dateTime;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ICurrentUserService _currentUserService;

        public AppDbContext(
            DbContextOptions<AppDbContext> options,
            IDateTime dateTime,
            IHttpContextAccessor httpContextAccessor,
            ICurrentUserService currentUserService)
            : base(options)
        {
            _dateTime = dateTime;
            _httpContextAccessor = httpContextAccessor;
            _currentUserService = currentUserService;
        }

        public DbSet<ApprovalStep> ApprovalSteps { get; set; }
        public DbSet<Request> Requests { get; set; }
        public DbSet<Quotation> Quotations { get; set; }
        public DbSet<AttachmentFile> AttachmentFiles { get; set; }
        //public DbSet<Role> Roles { get; set; }
        //public DbSet<UserRole> UserRoles { get; set; }
        //public DbSet<Department> Departments { get; set; }
        //public DbSet<UserDepartment> UserDepartments { get; set; }
        //public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Request>(entity =>
            {
                entity.Property(r => r.Code)
                    .HasMaxLength(50);

                entity.HasIndex(r => r.Code)
                    .IsUnique()
                    .HasDatabaseName("IX_Requests_Code");
            });
        }

        public override int SaveChanges()
        {
            SetAuditFields();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SetAuditFields();
            return await base.SaveChangesAsync(cancellationToken);
        }

        // ✅ แยก Logic ออกมาเป็น Private Method เพื่อลด Code Duplication
        private void SetAuditFields()
        {
            var entries = ChangeTracker.Entries<BaseEntity>();
            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = _dateTime.Now;
                    entry.Entity.CreatedBy = _currentUserService.UserId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = _dateTime.Now;
                    entry.Entity.UpdatedBy = _currentUserService.UserId;

                    // 🛡️ ป้องกันไม่ให้ CreatedAt และ CreatedBy ถูกแก้ไขโดยไม่ตั้งใจตอน Update
                    entry.Property(x => x.CreatedAt).IsModified = false;
                    entry.Property(x => x.CreatedBy).IsModified = false;
                }
            }
        }
    }
}