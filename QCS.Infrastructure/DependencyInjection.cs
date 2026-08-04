using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QCS.Application.Abstractions;
using QCS.Application.Services;
using QCS.Infrastructure.Data;
using QCS.Infrastructure.Services;

namespace QCS.Infrastructure
{
    /// <summary>
    /// Composition root for the Infrastructure layer.
    /// Registers EF Core, repositories, unit of work, and the concrete adapters
    /// for the abstractions defined in the Application layer.
    /// </summary>
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' is not configured. Set it in the server-side appsettings.json or user secrets.");
            }

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connectionString));

            services.AddTransient<IDateTime, DateTimeService>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddSingleton<IPdfPageCounter, PdfPigPageCounter>();

            // WorkflowService talks to an external HTTP endpoint, so it lives here.
            services.AddHttpClient<WorkflowService>();

            var vendorApiBaseUrl = configuration["ExternalServices:VendorApi"]
                ?? throw new InvalidOperationException("ExternalServices:VendorApi configuration is required.");

            services.AddHttpClient("VendorApi", client =>
            {
                client.BaseAddress = new Uri(vendorApiBaseUrl);
            });

            return services;
        }
    }
}
