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
            opt.AccessDeniedPath = "/";
            opt.ExpireTimeSpan = TimeSpan.FromHours(24);
            opt.SlidingExpiration = true;
        });
    builder.Services.AddAuthorization();

    // Repositories
    builder.Services.AddScoped<IGroupRepository, GroupRepository>();
    builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();

    // Services
    builder.Services.AddSingleton<ThemeService>();   // one active theme per app instance
    builder.Services.AddScoped<GroupService>();
    builder.Services.AddScoped<TransactionService>();
    builder.Services.AddScoped<BoardService>();
    builder.Services.AddScoped<MiniGameService>();
    builder.Services.AddScoped<LocationService>();
    builder.Services.AddScoped<InfoPageService>();
    builder.Services.AddScoped<StaffDirectoryService>();
    builder.Services.AddScoped<AnnouncementService>();
    builder.Services.AddScoped<ScheduleService>();
    builder.Services.AddScoped<ScheduleItemTypeService>();
    builder.Services.AddScoped<IncidentReportService>();
    builder.Services.AddScoped<SponsorService>();
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
    app.MapPost("/account/login", async (HttpContext ctx, AuthService auth, IDbContextFactory<AppDbContext> factory) =>
    {
        var form       = await ctx.Request.ReadFormAsync();
        var email      = form["email"].ToString();
        var password   = form["password"].ToString();
        var rememberMe = form["rememberMe"].ToString() is "on" or "true";

        DateTimeOffset? expiresUtc = null;
        if (rememberMe)
        {
            using var db    = factory.CreateDbContext();
            var activeEvent = await db.Events.FirstOrDefaultAsync(e => e.IsActive);
            var today       = DateOnly.FromDateTime(DateTime.UtcNow);
            if (activeEvent is not null && today >= activeEvent.EffDate && today <= activeEvent.ExpDate)
                expiresUtc = new DateTimeOffset(activeEvent.ExpDate.AddDays(1).ToDateTime(TimeOnly.Midnight), TimeSpan.Zero);
            else
                expiresUtc = DateTimeOffset.UtcNow.AddDays(7);
        }

        var result = await auth.LoginAsync(ctx, email, password, rememberMe, expiresUtc);
        return result switch
        {
            LoginResult.MustChangePassword => Results.Redirect("/change-password"),
            LoginResult.Success            => Results.Redirect("/dashboard"),
            _                              => Results.Redirect("/login?error=true")
        };
    });

    app.MapPost("/account/change-password", async (HttpContext ctx, AuthService auth) =>
    {
        if (ctx.User.Identity?.IsAuthenticated != true)
            return Results.Redirect("/login");

        var form    = await ctx.Request.ReadFormAsync();
        var newPw   = form["newPassword"].ToString();
        var confirm = form["confirmPassword"].ToString();

        if (string.IsNullOrWhiteSpace(newPw) || newPw.Length < 8)
            return Results.Redirect("/change-password?error=tooshort");

        if (newPw != confirm)
            return Results.Redirect("/change-password?error=mismatch");

        var userIdStr = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Results.Redirect("/login");

        await auth.ChangePasswordAsync(userId, newPw, ctx);
        return Results.Redirect("/dashboard");
    }).RequireAuthorization();

    app.MapGet("/logout", async (HttpContext ctx) =>
    {
        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        ctx.Response.Redirect("/login");
    });

    // Serve sponsor logos stored as bytea in the database
    app.MapGet("/sponsors/logo/{id:guid}", async (Guid id, SponsorService svc) =>
    {
        var s = await svc.GetByIdAsync(id);
        if (s?.LogoData is null) return Results.NotFound();
        return Results.File(s.LogoData, s.LogoContentType ?? "image/jpeg");
    });

    app.MapGet("/staff-photo/{id:guid}", async (Guid id, IDbContextFactory<AppDbContext> factory) =>
    {
        using var db = factory.CreateDbContext();
        var member = await db.StaffMembers.FindAsync(id);
        if (member?.PhotoData is null) return Results.NotFound();
        return Results.File(member.PhotoData, member.PhotoContentType ?? "image/jpeg");
    }).AllowAnonymous();

    app.MapGet("/location-image/{id:guid}", async (Guid id, IDbContextFactory<AppDbContext> factory) =>
    {
        using var db = factory.CreateDbContext();
        var loc = await db.Locations.FindAsync(id);
        if (loc?.ImageData is null) return Results.NotFound();
        return Results.File(loc.ImageData, loc.ImageContentType ?? "image/jpeg");
    }).AllowAnonymous();

    app.MapGet("/hub/info/{slug}/pdf", async (string slug, HttpContext ctx, IDbContextFactory<AppDbContext> factory) =>
    {
        using var db = factory.CreateDbContext();
        var page = await db.InfoPages.FirstOrDefaultAsync(p => p.Slug == slug);
        if (page?.PdfData is null) return Results.NotFound();
        if (!string.IsNullOrEmpty(page.PdfVisibleRoles))
        {
            var allowed = page.PdfVisibleRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var userRoles = ctx.User.Claims
                .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                .Select(c => c.Value);
            if (!ctx.User.IsInRole("Admin") && !userRoles.Any(r => allowed.Contains(r, StringComparer.OrdinalIgnoreCase)))
                return Results.Forbid();
        }
        return Results.File(page.PdfData, page.PdfContentType ?? "application/pdf");
    }).RequireAuthorization();

    app.MapHub<LiveHub>("/livehub");
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
