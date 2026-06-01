using QCS.API.Extensions;
using QCS.API.Middleware;
using QCS.Application;
using QCS.Application.Hubs;
using QCS.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Composition root — one call per layer.
builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Global exception handler — translates unhandled exceptions to ProblemDetails.
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

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
