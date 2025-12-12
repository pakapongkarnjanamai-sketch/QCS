using Microsoft.AspNetCore.Authentication.Negotiate;
using QCS.Web.Shared.Middleware;
using QCS.Web.Shared.Services;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages().AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = null);

// เพิ่ม Session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// Windows Authentication Configuration
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

// IIS Integration for Windows Authentication
builder.Services.Configure<IISOptions>(options =>
{
    options.AutomaticAuthentication = true;
    options.AuthenticationDisplayName = "Windows";
});

// Authorization with Role-based policies
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;

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
            context.User.Identity?.Name?.StartsWith("NIKONOA\\", StringComparison.OrdinalIgnoreCase) == true));
});

// Services
builder.Services.AddHttpContextAccessor();
//builder.Services.AddScoped<IApiUserService, ApiUserService>();

// HTTP Client for API calls
builder.Services.AddHttpClient("DocTrackerAPI", client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("ApiSettings:BaseUrl") ?? "https://localhost:7127");
    client.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
{
    UseDefaultCredentials = true
});

// Add memory cache
builder.Services.AddMemoryCache();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Static files ก่อน

app.UseRouting();
app.UseSession(); // เพิ่ม Session

app.UseAuthentication();
app.UseAuthorization();

// Middleware หลัง authentication/authorization
//app.UseMiddleware<ApiUserSyncMiddleware>();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
