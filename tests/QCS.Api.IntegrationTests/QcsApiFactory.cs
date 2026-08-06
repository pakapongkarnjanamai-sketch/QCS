using System;
using System.Collections.Generic;
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
                    ["ExternalServices:Approval:ForwardedUserSecret"] = "integration-test-secret"
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
}
