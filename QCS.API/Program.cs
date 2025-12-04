using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.EntityFrameworkCore;
using QCS.Application.Services;
using QCS.Infrastructure.Data;
using QCS.Infrastructure.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = null);
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "QCS API", Version = "v1" });
});

builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<IISOptions>(options =>
{
    options.AutomaticAuthentication = true;
    options.AuthenticationDisplayName = "Windows";
});

builder.Services.AddAuthorization(options =>
{
    // Default policy - require authentication
    options.FallbackPolicy = options.DefaultPolicy;

    // Role-based policies
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin", "SuperAdmin"));

    options.AddPolicy("SuperAdminOnly", policy =>
        policy.RequireRole("SuperAdmin"));

    options.AddPolicy("ManagerOrAbove", policy =>
        policy.RequireRole("Manager", "Admin", "SuperAdmin"));

    options.AddPolicy("UserOrAbove", policy =>
        policy.RequireRole("User", "Manager", "Admin", "SuperAdmin"));

    // Domain-based policies
    options.AddPolicy("DomainUser", policy =>
        policy.RequireAssertion(context =>
            context.User.Identity?.Name?.StartsWith("NIKONOA\\", StringComparison.OrdinalIgnoreCase) == true));
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<IDateTime, DateTimeService>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
// register all services and policies first
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("https://localhost:7154", "https://localhost:7105")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "QCS API V1");
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();

app.MapControllers();

app.Run();
