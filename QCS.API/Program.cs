using QCS.API.Extensions;
using QCS.API.Middleware;
using QCS.Application;
using QCS.Application.Hubs;
using QCS.Infrastructure;
using Microsoft.Extensions.Options;
using QCS.API.Authentication;
using QCS.API.Integration;

var builder = WebApplication.CreateBuilder(args);

// Composition root — one call per layer.
builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Global exception handler — translates unhandled exceptions to ProblemDetails.
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

var integrationKeys = app.Services.GetRequiredService<IOptions<ApiKeyOptions>>().Value;
if (integrationKeys.ApiKeys.Count == 0)
{
    app.Logger.LogWarning(
        "No integration API keys are configured (Integration:ApiKeys is empty). Every request to /api/Integration will be rejected with 401.");
}

var qrsIntegration = app.Services.GetRequiredService<IOptions<QrsIntegrationOptions>>().Value;
if (string.IsNullOrWhiteSpace(qrsIntegration.BaseUrl) || string.IsNullOrWhiteSpace(qrsIntegration.ApiKey))
{
    app.Logger.LogWarning(
        "QRS integration is not configured (ExternalServices:Qrs:BaseUrl or ApiKey is empty). QRS sourcing lookup will be unavailable.");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("v1/swagger.json", "QCS API V1"));

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapHub<NotificationHub>("/notificationHub");
app.UseCors("AllowAll");

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
