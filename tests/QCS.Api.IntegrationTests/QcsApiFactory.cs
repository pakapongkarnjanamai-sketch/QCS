using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QCS.Application.Abstractions;
using QCS.API.Services;
using QCS.Infrastructure.Data;

namespace QCS.Api.IntegrationTests
{
    public class QcsApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"qcs-tests-{Guid.NewGuid()}";

        public FakeApprovalService ApprovalService { get; } = new();
        public FakeQrsSourcingService QrsSourcingService { get; } = new();
        public FakeDateTime DateTime { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Server=fake;Database=fake;Trusted_Connection=True;");

            builder.ConfigureAppConfiguration((context, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=fake;Database=fake;Trusted_Connection=True;",
                    ["DomainSettings:DomainPrefix"] = "NIKONOA\\",
                    ["ExternalServices:VendorApi"] = "https://vendor.invalid/api/",
                    ["ExternalServices:EmployeeLookupDepartmentApi"] = "https://lookup.invalid/GetDepartment",
                    ["ExternalServices:EmployeeLookupFullApi"] = "https://lookup.invalid/GetFull",
                    ["ExternalServices:WorkflowApi"] = "https://workflow.invalid/",
                    ["ExternalServices:Approval:DocumentBaseUrl"] = "https://approval.invalid/Document",
                    ["ExternalServices:Approval:WorkflowBaseUrl"] = "https://approval.invalid/Workflow",
                    ["ExternalServices:Approval:SourceSystem"] = "QCS",
                    ["ExternalServices:Approval:DocumentTypeCode"] = "QC",
                    ["ExternalServices:Approval:RequestUrlTemplate"] = "https://approval.invalid/QCS/User/requests/{id}",
                    ["ExternalServices:Approval:ForwardedUserSecret"] = "integration-test-secret",
                    ["Integration:ApiKeys:0"] = "test-api-key"
                });
            });

            builder.ConfigureServices(services =>
            {
                var descriptors = services
                    .Where(d =>
                        d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                        d.ServiceType == typeof(DbContextOptions) ||
                        (d.ServiceType.IsGenericType &&
                         d.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>)))
                    .ToArray();

                foreach (var descriptor in descriptors)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_databaseName);

                    // The in-memory store has no transactions, and EF raises TransactionIgnoredWarning
                    // as an error by default. Suppressing it here — in the test project, where the
                    // limitation lives — lets production code call BeginTransaction() unconditionally
                    // instead of branching on the provider.
                    //
                    // LIMITATION, deliberate and accepted: these tests therefore exercise
                    // transactional paths such as RequestService.DeleteAsync WITHOUT a real
                    // transaction, so they can prove the happy path but never prove a rollback.
                    // Green tests here are not rollback coverage. Anything that depends on rollback
                    // needs a relational provider.
                    options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                });

                services.RemoveAll<IApprovalService>();
                services.AddSingleton<IApprovalService>(ApprovalService);
                services.RemoveAll<IDateTime>();
                services.AddSingleton<IDateTime>(DateTime);
                services.RemoveAll<IQrsSourcingService>();
                services.AddSingleton<IQrsSourcingService>(QrsSourcingService);
                services.RemoveAll<IEmployeeDirectory>();
                services.AddSingleton<IEmployeeDirectory, FakeEmployeeDirectory>();
                services.RemoveAll<IEmployeeLookupService>();
                services.AddSingleton<IEmployeeLookupService, FakeEmployeeLookupService>();

                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.SchemeMap.Remove(NegotiateDefaults.AuthenticationScheme, out _);

                    var negotiate = options.Schemes
                        .FirstOrDefault(s => s.Name == NegotiateDefaults.AuthenticationScheme);

                    if (negotiate is not null && options.Schemes is ICollection<AuthenticationSchemeBuilder> schemes)
                    {
                        schemes.Remove(negotiate);
                    }

                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                });
            });
        }

        public void SeedDatabase(Action<AppDbContext> seed)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            seed(db);
            db.SaveChanges();
        }
    }

    public sealed class FakeDateTime : IDateTime
    {
        public DateTime Now { get; set; } = DateTime.Now;
        public CultureInfo CultureInfo => CultureInfo.InvariantCulture;
        public DateTime UnixTime => DateTime.UnixEpoch;
    }

    public class FakeQrsSourcingService : IQrsSourcingService
    {
        private readonly Dictionary<string, QrsSourcingDetailDto> _details = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Exception> _failures = new(StringComparer.OrdinalIgnoreCase);

        public void SetDetail(string code, QrsSourcingDetailDto detail)
        {
            _details[code] = detail;
        }

        public void SetFailure(string code, Exception exception)
        {
            _failures[code] = exception;
        }

        public Task<QrsSourcingPagedResultDto> GetRequestsAsync(string? search, int page, int pageSize, string? intent, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new QrsSourcingPagedResultDto());
        }

        public Task<QrsSourcingDetailDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            if (_failures.TryGetValue(code, out var failure))
            {
                throw failure;
            }

            if (_details.TryGetValue(code, out var detail))
            {
                return Task.FromResult<QrsSourcingDetailDto?>(detail);
            }
            if (!string.IsNullOrEmpty(code) && code.StartsWith("QRS-", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<QrsSourcingDetailDto?>(new QrsSourcingDetailDto
                {
                    Code = code,
                    Title = $"Mock {code}",
                    Intent = 1,
                    PreviousQcCode = "QC-20260806-579"
                });
            }
            return Task.FromResult<QrsSourcingDetailDto?>(null);
        }
    }
}
