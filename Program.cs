using Ask2Ask.Data;
using Ask2Ask.Services;
using Ask2Ask.Middleware;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

// Load API configuration
builder.Configuration.AddJsonFile("appsettings.Api.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddRazorPages();

// Add memory cache for rate limiting
builder.Services.AddMemoryCache();

// Add database context
builder.Services.AddDbContext<TrackingDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("TrackingDb") 
        ?? "Data Source=TrackingData/tracking.db"));

// Add tracking service
builder.Services.AddScoped<TrackingService>();

// Add API authentication service
builder.Services.AddScoped<ApiAuthenticationService>();

// Add ZKP authentication service
builder.Services.AddScoped<ZkpAuthenticationService>();

// Add ASN ping timing service
builder.Services.AddScoped<AsnPingTimingService>();

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Add API authentication middleware (before authorization)
app.UseMiddleware<ApiAuthenticationMiddleware>();

app.UseAuthorization();

// Ensure root path redirects to Index page (must be before MapRazorPages)
app.MapGet("/", () => Results.Redirect("/Index", permanent: false));

app.MapRazorPages();

app.Run();
