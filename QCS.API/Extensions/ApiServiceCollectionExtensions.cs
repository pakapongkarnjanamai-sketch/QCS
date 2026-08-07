using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using QCS.API.Authentication;
using QCS.API.Security;
using QCS.API.Services;

namespace QCS.API.Extensions
{
    /// <summary>
    /// Composition root for the API layer (web concerns: MVC, auth, CORS, Swagger, SignalR).
    /// </summary>
    public static class ApiServiceCollectionExtensions
    {
        public static IServiceCollection AddApiServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddSignalR();
            services.AddMemoryCache();
            services.AddHttpContextAccessor();
            services.AddScoped<IEmployeeLookupService, EmployeeLookupService>();
            services.AddScoped<IClaimsTransformation, AdminAccessClaimsTransformation>();

            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.DictionaryKeyPolicy = null;
                });

            services.AddOpenApi();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new() { Title = "QCS API", Version = "v1" });
            });

            services.AddProblemDetails();

            services.AddApiAuthentication(configuration);
            services.AddApiAuthorization(configuration);
            services.AddApiCors(configuration);

            return services;
        }

        private static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ApiKeyOptions>(configuration.GetSection(ApiKeyOptions.SectionName));

            services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
                .AddNegotiate()
                .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                    ApiKeyAuthenticationHandler.SchemeName,
                    configureOptions: null);

            services.Configure<IISOptions>(options =>
            {
                options.AutomaticAuthentication = true;
                options.AuthenticationDisplayName = "Windows";
            });

            return services;
        }

        private static IServiceCollection AddApiAuthorization(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var domainPrefix = configuration["DomainSettings:DomainPrefix"]
                ?? throw new InvalidOperationException("DomainSettings:DomainPrefix configuration is required.");

            services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        context.User.Identity?.Name?.StartsWith(domainPrefix, StringComparison.OrdinalIgnoreCase) == true)
                    .Build();

                options.AddPolicy("AdminOnly", policy =>
                    policy.RequireRole("Admin", "SuperAdmin"));

                options.AddPolicy("SuperAdminOnly", policy =>
                    policy.RequireRole("SuperAdmin"));

                options.AddPolicy("ManagerOrAbove", policy =>
                    policy.RequireRole("Manager", "Admin", "SuperAdmin"));

                options.AddPolicy("UserOrAbove", policy =>
                    policy.RequireRole("User", "Manager", "Admin", "SuperAdmin"));

                options.AddPolicy("DomainUser", policy =>
                    policy.RequireAssertion(context =>
                        context.User.Identity?.Name?.StartsWith(domainPrefix, StringComparison.OrdinalIgnoreCase) == true));

                options.AddPolicy("IntegrationClient", policy =>
                    policy.AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName)
                        .RequireAuthenticatedUser());
            });

            return services;
        }

        private static IServiceCollection AddApiCors(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var allowedOrigins = configuration.GetSection("CorsOrigins").Get<string[]>()
                ?? throw new InvalidOperationException("CorsOrigins configuration is required.");
            var normalizedOrigins = BuildAllowedOrigins(allowedOrigins);

            services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.WithOrigins(normalizedOrigins)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                });
            });

            return services;
        }

        private static string[] BuildAllowedOrigins(IEnumerable<string> origins)
        {
            var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawOrigin in origins)
            {
                if (string.IsNullOrWhiteSpace(rawOrigin))
                {
                    continue;
                }

                var candidate = rawOrigin.Trim().TrimEnd('/');
                if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    normalized.Add(candidate);
                    continue;
                }

                var portPart = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
                normalized.Add($"{uri.Scheme}://{uri.Host}{portPart}");

                if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                {
                    normalized.Add($"{uri.Scheme}://127.0.0.1{portPart}");
                }
                else if (uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
                {
                    normalized.Add($"{uri.Scheme}://localhost{portPart}");
                }
            }

            return normalized.ToArray();
        }
    }
}
