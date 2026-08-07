using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using QCS.Application.Abstractions;
using QCS.Infrastructure.Services;

namespace QCS.Infrastructure.Data
{
    /// <summary>
    /// Lets `dotnet ef` construct <see cref="AppDbContext"/> without starting QCS.API's web host.
    ///
    /// Without this, EF tooling falls back to building the API's IHost, which drags in the whole
    /// web pipeline - and fails outright when QCS.API/wwwroot does not exist, with an error
    /// ("Unable to create a 'DbContext'") that says nothing about the missing directory. Migration
    /// tooling has no business depending on a web root, so it no longer does.
    ///
    /// Design time never opens the connection - `migrations add` and `migrations script` only need
    /// a provider and a syntactically valid connection string - so the local fallback below is not
    /// a credential and is deliberately pointed at LocalDB. Override it with the same environment
    /// variable the plans' verification steps already use:
    ///
    ///   $env:ConnectionStrings__DefaultConnection='Server=...;Database=...;Trusted_Connection=True'
    /// </summary>
    public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        private const string DesignTimeFallbackConnection =
            @"Server=(localdb)\MSSQLLocalDB;Database=QCS_DesignTime;Trusted_Connection=True;TrustServerCertificate=True";

        public AppDbContext CreateDbContext(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = DesignTimeFallbackConnection;
            }

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            // The three services below exist for auditing on SaveChanges. Model building - the only
            // thing the tooling actually does - never touches them. The real DateTimeService is used
            // rather than a stub so there is still one definition of "now"; the other two
            // deliberately have no HttpContext and no user, because design time has neither.
            return new AppDbContext(
                options,
                new DateTimeService(),
                new DesignTimeHttpContextAccessor(),
                new DesignTimeCurrentUser());
        }

        private sealed class DesignTimeHttpContextAccessor : IHttpContextAccessor
        {
            public HttpContext? HttpContext { get; set; }
        }

        private sealed class DesignTimeCurrentUser : ICurrentUserService
        {
            public string UserId => string.Empty;

            public string FullName => string.Empty;

            public bool IsAuthenticated => false;
        }
    }
}
