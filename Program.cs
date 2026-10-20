using LGMS.Data;
using LGMS.Models;
using LGMS.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Load optional local secrets (gitignored)
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

var secretsFile = Path.Combine(builder.Environment.ContentRootPath, "secrets.txt");
if (File.Exists(secretsFile))
{
    foreach (var line in File.ReadAllLines(secretsFile))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#')) continue;
        var parts = trimmed.Split('=', 2);
        if (parts.Length == 2)
        {
            var key = parts[0].Trim();
            var val = parts[1].Trim();
            if (key == "DEEPSEEK_API_KEY" && string.IsNullOrEmpty(builder.Configuration["DeepSeek:ApiKey"]))
                builder.Configuration["DeepSeek:ApiKey"] = val;
            if (key == "GEMINI_API_KEY" && string.IsNullOrEmpty(builder.Configuration["Gemini:ApiKey"]))
                builder.Configuration["Gemini:ApiKey"] = val;
        }
    }
}

// Add services to the container.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(120);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation();

// Register MS SQL Server EF Core DbContext
builder.Services.AddDbContext<LgmsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Lgms")
        ?? "Server=.;Database=LGMS_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"));

// Configure AI Options
builder.Services.Configure<AIOptions>(builder.Configuration.GetSection(AIOptions.SectionName));
builder.Services.Configure<DeepSeekOptions>(builder.Configuration.GetSection(DeepSeekOptions.SectionName));
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));

// Register Process, Git, AI Providers and GitHub CLI services
builder.Services.AddSingleton<GitAccountOperationLock>();
builder.Services.AddSingleton<ProcessRunner>();
builder.Services.AddSingleton<IProcessService, ProcessService>();
builder.Services.AddSingleton<IRepositoryScanner, RepositoryScanner>();
builder.Services.AddScoped<IGitService, GitService>();
builder.Services.AddScoped<DeepSeekCommitService>();
builder.Services.AddScoped<IDeepSeekCommitService>(sp => sp.GetRequiredService<DeepSeekCommitService>());
builder.Services.AddScoped<IAICommitService>(sp => sp.GetRequiredService<DeepSeekCommitService>());
builder.Services.AddScoped<IAICommitService, GeminiCommitService>();
builder.Services.AddScoped<IAICommitManager, AICommitManager>();
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
