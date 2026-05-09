using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authentication;
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

            services.AddApiAuthentication();
            services.AddApiAuthorization(configuration);
            services.AddApiCors(configuration);

            return services;
        }

        private static IServiceCollection AddApiAuthentication(this IServiceCollection services)
        {
            services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
                .AddNegotiate();

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
            });

            return services;
        }

        private static IServiceCollection AddApiCors(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var allowedOrigins = configuration.GetSection("CorsOrigins").Get<string[]>()
                ?? throw new InvalidOperationException("CorsOrigins configuration is required.");

            services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                });
            });

            return services;
        }
    }
}
