using Microsoft.Extensions.DependencyInjection;
using QCS.Application.Abstractions;
using QCS.Application.Services;

namespace QCS.Application
{
    /// <summary>
    /// Composition root for the Application layer.
    /// Registers application services that depend only on the Application/Domain layers.
    /// </summary>
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IRequestService, RequestService>();
            services.AddHttpClient<IQuotationService, QuotationService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(35);
            });
            services.AddScoped<IFileService, FileService>();
            services.AddScoped<IPaperSavedService, PaperSavedService>();

            return services;
        }
    }
}
