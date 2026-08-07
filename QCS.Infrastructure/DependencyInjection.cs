using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QCS.Application.Abstractions;
using QCS.Application.Services;
using QCS.Infrastructure.Data;
using QCS.Infrastructure.Services;
using QCS.Infrastructure.Approval;
using Microsoft.Extensions.Options;

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
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseDefaultCredentials = true
            });

            var employeeLookupFullApi = configuration["ExternalServices:EmployeeLookupFullApi"]
                ?? throw new InvalidOperationException("ExternalServices:EmployeeLookupFullApi configuration is required.");

            services.AddHttpClient<IEmployeeDirectory, EmployeeDirectoryAdapter>(client =>
            {
                client.BaseAddress = new Uri(employeeLookupFullApi);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseDefaultCredentials = true
            });

            services.AddOptions<ApprovalServiceOptions>()
                .Bind(configuration.GetSection(ApprovalServiceOptions.SectionName))
                .Validate(
                    options => Uri.TryCreate(options.DocumentBaseUrl, UriKind.Absolute, out _),
                    $"{ApprovalServiceOptions.SectionName}:DocumentBaseUrl must be an absolute URL.")
                .Validate(
                    options => Uri.TryCreate(options.WorkflowBaseUrl, UriKind.Absolute, out _),
                    $"{ApprovalServiceOptions.SectionName}:WorkflowBaseUrl must be an absolute URL.")
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.SourceSystem),
                    $"{ApprovalServiceOptions.SectionName}:SourceSystem is required.")
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.DocumentTypeCode),
                    $"{ApprovalServiceOptions.SectionName}:DocumentTypeCode is required.")
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.ForwardedUserSecret),
                    $"{ApprovalServiceOptions.SectionName}:ForwardedUserSecret is required.")
                .Validate(
                    options => options.RequestUrlTemplate.Contains("{id}", StringComparison.OrdinalIgnoreCase)
                        && Uri.TryCreate(
                            options.RequestUrlTemplate.Replace("{id}", "1", StringComparison.OrdinalIgnoreCase),
                            UriKind.Absolute,
                            out _),
                    $"{ApprovalServiceOptions.SectionName}:RequestUrlTemplate must be an absolute URL containing '{{id}}'.")
                .Validate(
                    options => options.TimeoutSeconds is > 0 and <= 300,
                    $"{ApprovalServiceOptions.SectionName}:TimeoutSeconds must be between 1 and 300.")
                .ValidateOnStart();

            services.AddHttpClient<IApprovalService, ApprovalServiceClient>((provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<ApprovalServiceOptions>>().Value;
                client.BaseAddress = new Uri(options.DocumentBaseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseDefaultCredentials = true
            });

            services.AddHttpClient(ApprovalServiceClient.WorkflowHttpClientName, (provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<ApprovalServiceOptions>>().Value;
                client.BaseAddress = new Uri(options.WorkflowBaseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseDefaultCredentials = true
            });

            services.AddScoped<IApprovalRequestFactory, ApprovalRequestFactory>();

            services.Configure<QCS.Infrastructure.Integration.QrsIntegrationOptions>(configuration.GetSection(QCS.Infrastructure.Integration.QrsIntegrationOptions.SectionName));
            services.AddHttpClient<IQrsSourcingService, QCS.Infrastructure.Integration.QrsSourcingService>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptionsMonitor<QCS.Infrastructure.Integration.QrsIntegrationOptions>>().CurrentValue;
                client.BaseAddress = Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseAddress)
                    ? new Uri(baseAddress.ToString().TrimEnd('/') + "/")
                    : null;
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });

            return services;
        }
    }
}
