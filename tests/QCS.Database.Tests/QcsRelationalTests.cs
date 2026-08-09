using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QCS.Application.Services;
using QCS.Domain.Enum;
using QCS.Domain.Models;
using QCS.Infrastructure.Data;

namespace QCS.Database.Tests;

public class QcsRelationalTests : IClassFixture<LocalDbTestFixture>
{
    private readonly LocalDbTestFixture _fixture;

    public QcsRelationalTests(LocalDbTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Concurrent_renewal_creation_for_same_predecessor_raises_unique_index_violation()
    {
        using var db1 = _fixture.CreateDbContext();
        using var db2 = _fixture.CreateDbContext();

        var pred = new Request
        {
            Code = "QC-PRED-RACE",
            Title = "Race Predecessor",
            VendorCode = "V1",
            VendorName = "Vendor 1",
            Status = (int)RequestStatus.Completed,
            ValidUntil = DateTime.Now.AddDays(10),
            CreatedBy = "USER1",
            IsActive = true
        };
        db1.Requests.Add(pred);
        await db1.SaveChangesAsync();

        var successor1 = new Request
        {
            Code = "QC-SUCC-1",
            Title = "Successor 1",
            Intent = RequestIntent.Renewal,
            RenewedFromRequestId = pred.Id,
            VendorCode = "V1",
            VendorName = "Vendor 1",
            Status = (int)RequestStatus.Draft,
            CreatedBy = "USER1",
            IsActive = true
        };
        db1.Requests.Add(successor1);
        await db1.SaveChangesAsync();

        var successor2 = new Request
        {
            Code = "QC-SUCC-2",
            Title = "Successor 2",
            Intent = RequestIntent.Renewal,
            RenewedFromRequestId = pred.Id,
            VendorCode = "V1",
            VendorName = "Vendor 1",
            Status = (int)RequestStatus.Draft,
            CreatedBy = "USER2",
            IsActive = true
        };
        db2.Requests.Add(successor2);

        var ex = await Should.ThrowAsync<DbUpdateException>(() => db2.SaveChangesAsync());
        RequestService.IsPredecessorUniqueConflict(ex).ShouldBeTrue();
    }

    [Fact]
    public async Task Multiple_requests_with_null_predecessor_are_permitted()
    {
        using var db = _fixture.CreateDbContext();

        var req1 = new Request
        {
            Code = "QC-NULL-PRED-1",
            Title = "New Request 1",
            Intent = RequestIntent.New,
            RenewedFromRequestId = null,
            VendorCode = "V1",
            VendorName = "Vendor 1",
            Status = (int)RequestStatus.Draft,
            CreatedBy = "USER1",
            IsActive = true
        };
        var req2 = new Request
        {
            Code = "QC-NULL-PRED-2",
            Title = "New Request 2",
            Intent = RequestIntent.New,
            RenewedFromRequestId = null,
            VendorCode = "V1",
            VendorName = "Vendor 1",
            Status = (int)RequestStatus.Draft,
            CreatedBy = "USER1",
            IsActive = true
        };

        db.Requests.AddRange(req1, req2);
        await Should.NotThrowAsync(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Migration_preflight_throws_if_duplicate_predecessors_exist()
    {
        var dbName = $"QCS_Preflight_{Guid.NewGuid():N}";
        var connectionString = $"Server=(localdb)\\mssqllocaldb;Database={dbName};Trusted_Connection=True;MultipleActiveResultSets=true;Connect Timeout=15;";
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connectionString);
        var factory = new AppDbContextDesignTimeFactory();

        try
        {
            using (var db = factory.CreateDbContext(Array.Empty<string>()))
            {
                var migrator = db.GetService<IMigrator>();
                await migrator.MigrateAsync("20260807010554_AddRequestIntentAndRenewalSource");

                var pred = new Request
                {
                    Code = "QC-PRED-DUP",
                    Title = "Predecessor",
                    VendorCode = "V1",
                    VendorName = "Vendor 1",
                    Status = (int)RequestStatus.Completed,
                    CreatedBy = "USER1",
                    IsActive = true
                };
                db.Requests.Add(pred);
                await db.SaveChangesAsync();

                var child1 = new Request
                {
                    Code = "QC-CHILD-1",
                    Title = "Child 1",
                    Intent = RequestIntent.Renewal,
                    RenewedFromRequestId = pred.Id,
                    VendorCode = "V1",
                    VendorName = "Vendor 1",
                    Status = (int)RequestStatus.Draft,
                    CreatedBy = "USER1",
                    IsActive = true
                };
                var child2 = new Request
                {
                    Code = "QC-CHILD-2",
                    Title = "Child 2",
                    Intent = RequestIntent.Renewal,
                    RenewedFromRequestId = pred.Id,
                    VendorCode = "V1",
                    VendorName = "Vendor 1",
                    Status = (int)RequestStatus.Draft,
                    CreatedBy = "USER1",
                    IsActive = true
                };
                db.Requests.AddRange(child1, child2);
                await db.SaveChangesAsync();
            }

            using (var db = factory.CreateDbContext(Array.Empty<string>()))
            {
                var migrator = db.GetService<IMigrator>();
                var ex = await Should.ThrowAsync<Exception>(() => migrator.MigrateAsync());
                ex.Message.ShouldContain("IX_Requests_RenewedFromRequestId");
            }
        }
        finally
        {
            using var db = factory.CreateDbContext(Array.Empty<string>());
            await db.Database.EnsureDeletedAsync();
        }
    }
}
