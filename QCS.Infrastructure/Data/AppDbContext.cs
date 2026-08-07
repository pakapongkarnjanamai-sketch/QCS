using Microsoft.AspNetCore.Http;
using QCS.Application.Abstractions;
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
        public DbSet<AdminUserAccess> AdminUserAccesses { get; set; }
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

                entity.Property(r => r.SourceSystem)
                    .HasMaxLength(20);

                entity.Property(r => r.SourceCode)
                    .HasMaxLength(50);

                entity.HasIndex(r => r.Code)
                    .IsUnique()
                    .HasDatabaseName("IX_Requests_Code");

                entity.HasIndex(r => new { r.SourceSystem, r.SourceCode })
                    .HasDatabaseName("IX_Requests_Source");

                entity.Property(r => r.ApprovalDocumentNumber)
                    .HasMaxLength(50);

                entity.Property(r => r.CurrentStepName)
                    .HasMaxLength(200);

                entity.HasIndex(r => r.ApprovalDocumentId)
                    .IsUnique()
                    .HasFilter("[ApprovalDocumentId] IS NOT NULL")
                    .HasDatabaseName("IX_Requests_ApprovalDocumentId");

                entity.HasOne(r => r.RenewedFromRequest)
                    .WithMany()
                    .HasForeignKey(r => r.RenewedFromRequestId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(r => r.RenewedFromRequestId)
                    .IsUnique()
                    .HasFilter("[RenewedFromRequestId] IS NOT NULL")
                    .HasDatabaseName("IX_Requests_RenewedFromRequestId");
            });

            modelBuilder.Entity<Quotation>(entity =>
            {
                entity.HasOne(quotation => quotation.SourceQuotation)
                    .WithMany()
                    .HasForeignKey(quotation => quotation.SourceQuotationId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AdminUserAccess>(entity =>
            {
                entity.Property(x => x.NId)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.HasIndex(x => x.NId)
                    .IsUnique()
                    .HasDatabaseName("IX_AdminUserAccesses_NId");
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

                    // Stamped only when the caller has not already set it, so an explicitly
                    // assigned owner survives — this is what lets tests seed rows for a specific
                    // user. Safe because no DTO or model-binding path exposes CreatedBy: the
                    // request DTOs do not carry it and RequestService never assigns it from input.
                    // ⚠️ The day a DTO does expose CreatedBy, this becomes a spoofing hole and
                    // must go back to an unconditional assignment.
                    if (string.IsNullOrEmpty(entry.Entity.CreatedBy))
                    {
                        entry.Entity.CreatedBy = _currentUserService.UserId;
                    }
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