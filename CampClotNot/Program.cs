using CampClotNot.Data;
using CampClotNot.Hubs;
using CampClotNot.Repositories;
using CampClotNot.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Railway injects PORT — only override URLs in production; dev uses launchSettings.json
    var railwayPort = Environment.GetEnvironmentVariable("PORT");
    if (railwayPort is not null)
        builder.WebHost.UseUrls($"http://0.0.0.0:{railwayPort}");

    builder.Host.UseSerilog((ctx, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console());

    // Database — production reads DATABASE_URL from Railway environment
    var connStr = Environment.GetEnvironmentVariable("DATABASE_URL")
        ?? builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("No database connection string found.");

    // Railway injects DATABASE_URL as a postgres:// URI; EF Core needs a standard connection string
    if (connStr.StartsWith("postgres://") || connStr.StartsWith("postgresql://"))
        connStr = ConvertPostgresUri(connStr);

    // DbContextFactory — required for Blazor Server to avoid concurrent-command errors on the same circuit
    builder.Services.AddDbContextFactory<AppDbContext>(opt =>
        opt.UseNpgsql(connStr));

    // Cookie auth — 24-hour sessions per spec §7.2
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(opt =>
        {
            opt.LoginPath = "/login";
            opt.ExpireTimeSpan = TimeSpan.FromHours(24);
            opt.SlidingExpiration = true;
        });
    builder.Services.AddAuthorization();

    // Repositories
    builder.Services.AddScoped<IGroupRepository, GroupRepository>();
    builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();

    // Services
    builder.Services.AddScoped<GroupService>();
    builder.Services.AddScoped<TransactionService>();
    builder.Services.AddScoped<AuthService>();
    builder.Services.AddScoped<SeedService>();

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddRazorPages();
    builder.Services.AddServerSideBlazor();
    builder.Services.AddSignalR();
    builder.Services.AddMudServices();

    var app = builder.Build();

    // Seed reference data and dev admin user on every startup (idempotent)
    using (var scope = app.Services.CreateScope())
        await scope.ServiceProvider.GetRequiredService<SeedService>().SeedAsync();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        // Do NOT add UseHttpsRedirection here — Railway terminates TLS externally
    }
    else
    {
        app.UseDeveloperExceptionPage();
        app.UseHttpsRedirection();
    }

    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    // Health endpoint for Railway and ops monitoring
    app.MapGet("/health", async (IDbContextFactory<AppDbContext> factory) =>
    {
        try
        {
            using var db = factory.CreateDbContext();
            await db.Database.CanConnectAsync();
            return Results.Ok(new { status = "ok", db = "connected" });
        }
        catch (Exception ex)
        {
            return Results.Problem($"DB unreachable: {ex.Message}");
        }
    });

    // Login/logout endpoints — cookie auth requires a real HTTP response, not a Blazor SignalR circuit
    app.MapPost("/account/login", async (HttpContext ctx, AuthService auth) =>
    {
        var form = await ctx.Request.ReadFormAsync();
        var email    = form["email"].ToString();
        var password = form["password"].ToString();
        var ok = await auth.LoginAsync(ctx, email, password);
        return ok ? Results.Redirect("/") : Results.Redirect("/login?error=true");
    });

    app.MapGet("/logout", async (HttpContext ctx) =>
    {
        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        ctx.Response.Redirect("/login");
    });

    app.MapHub<CampHub>("/camphub");
    app.MapBlazorHub();
    app.MapFallbackToPage("/_Host");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    Log.CloseAndFlush();
}

// Railway injects a postgres:// URI — convert to Npgsql connection string format
static string ConvertPostgresUri(string uri)
{
    var u = new Uri(uri);
    var userInfo = u.UserInfo.Split(':');
    return $"Host={u.Host};Port={u.Port};Database={u.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
}
