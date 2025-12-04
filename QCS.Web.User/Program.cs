using QCS.Web.User.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages().AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = null);
// ลงทะเบียน HttpClient สำหรับ ApiClientService
builder.Services.AddHttpClient<IApiClientService, ApiClientService>(client =>
{
    // ใส่ URL ของ QCS.API ที่รันอยู่จริง (ดูจาก launchSettings.json ของโปรเจกต์ API)
    client.BaseAddress = new Uri("https://localhost:7127/api/");

    // ตั้งค่า Timeout หรือ Header อื่นๆ ได้ที่นี่
    client.Timeout = TimeSpan.FromMinutes(5);
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
