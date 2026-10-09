using LGMS.Data;
using LGMS.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(120);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation();

// Register MS SQL Server EF Core DbContext
builder.Services.AddDbContext<LgmsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Lgms")
        ?? "Server=.;Database=LGMS_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"));

// Register Process, Git and GitHub CLI services
builder.Services.AddSingleton<GitAccountOperationLock>();
builder.Services.AddSingleton<ProcessRunner>();
builder.Services.AddSingleton<IProcessService, ProcessService>();
builder.Services.AddSingleton<IRepositoryScanner, RepositoryScanner>();
builder.Services.AddScoped<IGitService, GitService>();
builder.Services.AddScoped<GitHubCliService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Auto-migrate or ensure database exists on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<LgmsDbContext>();
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Could not automatically migrate MS SQL database on startup.");
    }
}

app.Run();
