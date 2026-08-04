using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QCS.Infrastructure.Data;

namespace QCS.Api.IntegrationTests
{
    public class QcsApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"qcs-tests-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=fake;Database=fake;Trusted_Connection=True;",
                    ["DomainSettings:DomainPrefix"] = "NIKONOA\\",
                    ["ExternalServices:VendorApi"] = "https://vendor.invalid/api/",
                    ["ExternalServices:EmployeeLookupDepartmentApi"] = "https://lookup.invalid/GetDepartment",
                    ["ExternalServices:EmployeeLookupFullApi"] = "https://lookup.invalid/GetFull",
                    ["ExternalServices:WorkflowApi"] = "https://workflow.invalid/"
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
                });

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
