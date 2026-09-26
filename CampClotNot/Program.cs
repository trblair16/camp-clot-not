using CampClotNot.Data;
using CampClotNot.Hubs;
using CampClotNot.Repositories;
using CampClotNot.Services;
using CampClotNot.Services.Email;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.ResponseCompression;
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
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("HubGuestAccess", policy => policy.RequireAssertion(ctx =>
            ctx.User.IsInRole("Admin") || ctx.User.IsInRole("Staff") ||
            ctx.User.IsInRole("Volunteer") || ctx.User.IsInRole("MedicalStaff") ||
            ctx.User.HasClaim(c => c.Type == GuestClaimTypes.EventId)));
    });
    builder.Services.AddMemoryCache();

    // Repositories
    builder.Services.AddScoped<IGroupRepository, GroupRepository>();
    builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();

    // Services
    builder.Services.AddScoped<ThemeService>();   // resolved per circuit via the active event's Theme
    builder.Services.AddScoped<ActiveEventService>();
    builder.Services.AddScoped<CapabilityService>();
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
    builder.Services.AddScoped<DocumentService>();
    builder.Services.AddScoped<BowserEventService>();
    builder.Services.AddScoped<GuestAccessService>();
    builder.Services.AddScoped<AttendanceService>();
    builder.Services.AddScoped<EventStaffService>();
    builder.Services.AddScoped<RegistrationService>();
    builder.Services.AddScoped<ScheduleImportService>();
    builder.Services.AddScoped<PasswordResetService>();
    builder.Services.AddSingleton<ForgotPasswordQueue>();
    builder.Services.AddHostedService<ForgotPasswordWorker>();
    builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>(c =>
    {
        c.BaseAddress = new Uri("https://api.resend.com/");
        c.Timeout = TimeSpan.FromSeconds(15);
    });
    builder.Services.AddScoped<ThemeAdminService>();
    builder.Services.AddScoped<EventSetupService>();
    builder.Services.AddScoped<AuthService>();
    builder.Services.AddSingleton<PushNotificationService>();
    builder.Services.AddScoped<SeedService>();

    builder.Services.AddDataProtection()
        .PersistKeysToDbContext<AppDbContext>();

    builder.Services.AddResponseCompression(opts =>
    {
        opts.EnableForHttps = true;
        opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/octet-stream"]);
    });

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddRazorPages();
    builder.Services.AddServerSideBlazor()
        .AddHubOptions(o => o.MaximumReceiveMessageSize = 11 * 1024 * 1024);
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

    app.UseResponseCompression();

    app.Use(async (context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            var reqPath = context.Request.Path.Value ?? "";
            if (reqPath.StartsWith("/_framework/") || reqPath.StartsWith("/_content/"))
                context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
            return Task.CompletedTask;
        });
        await next();
    });

    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = ctx =>
        {
            var path = ctx.File.Name;
            if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Context.Response.Headers["Cache-Control"] = "no-cache";
            }
            else if (path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
                  || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                  || path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
                  || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                  || path.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
            }
        }
    });
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
            var today       = CampTime.Today;
            if (activeEvent is not null && today >= activeEvent.EffDate && today <= activeEvent.ExpDate)
                expiresUtc = new DateTimeOffset(activeEvent.ExpDate.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            else
                expiresUtc = DateTimeOffset.UtcNow.AddDays(7);
        }

        var result    = await auth.LoginAsync(ctx, email, password, rememberMe, expiresUtc);
        var returnUrl = LocalReturnUrl(form["returnUrl"]);
        return result switch
        {
            LoginResult.MustChangePassword => Results.Redirect("/change-password" + (returnUrl is null ? "" : $"?returnUrl={Uri.EscapeDataString(returnUrl)}")),
            LoginResult.Success            => Results.Redirect(returnUrl ?? "/dashboard"),
            _                              => Results.Redirect("/login?error=true" + (returnUrl is null ? "" : $"&returnUrl={Uri.EscapeDataString(returnUrl)}"))
        };
    });

    app.MapPost("/account/change-password", async (HttpContext ctx, AuthService auth) =>
    {
        if (ctx.User.Identity?.IsAuthenticated != true)
            return Results.Redirect("/login");

        var form      = await ctx.Request.ReadFormAsync();
        var newPw     = form["newPassword"].ToString();
        var confirm   = form["confirmPassword"].ToString();
        var returnUrl = LocalReturnUrl(form["returnUrl"]);
        var keep      = returnUrl is null ? "" : $"&returnUrl={Uri.EscapeDataString(returnUrl)}";

        if (string.IsNullOrWhiteSpace(newPw) || newPw.Length < 8)
            return Results.Redirect("/change-password?error=tooshort" + keep);

        if (newPw != confirm)
            return Results.Redirect("/change-password?error=mismatch" + keep);

        var userIdStr = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Results.Redirect("/login");

        await auth.ChangePasswordAsync(userId, newPw, ctx);
        return Results.Redirect(returnUrl ?? "/dashboard");
    }).RequireAuthorization();

    // Forgot password — always the same response; the lookup and email happen in the background
    // (ForgotPasswordWorker) so neither the page nor its timing reveals whether an account exists.
    app.MapPost("/account/forgot-password", async (HttpContext ctx, ForgotPasswordQueue queue, IConfiguration config) =>
    {
        var form  = await ctx.Request.ReadFormAsync();
        var email = form["email"].ToString();
        if (!string.IsNullOrWhiteSpace(email))
            queue.Enqueue(new ForgotPasswordRequest(email, PublicBaseUrl.From(ctx.Request, config)));
        return Results.Redirect("/forgot-password?sent=true");
    }).AllowAnonymous();

    app.MapPost("/account/reset-password", async (HttpContext ctx, PasswordResetService resets, AuthService auth) =>
    {
        var form    = await ctx.Request.ReadFormAsync();
        var token   = form["token"].ToString();
        var newPw   = form["newPassword"].ToString();
        var confirm = form["confirmPassword"].ToString();
        var back    = $"/reset-password?token={Uri.EscapeDataString(token)}";

        if (newPw != confirm)
            return Results.Redirect(back + "&error=mismatch");

        var (result, userId) = await resets.RedeemAsync(token, newPw);
        switch (result)
        {
            case RedeemResult.TooShort: return Results.Redirect(back + "&error=tooshort");
            case RedeemResult.Invalid:  return Results.Redirect("/reset-password?error=invalid");
        }

        await auth.SignInAsync(ctx, userId!.Value);
        return Results.Redirect("/dashboard");
    }).AllowAnonymous();

    app.MapGet("/logout", async (HttpContext ctx) =>
    {
        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        ctx.Response.Redirect("/login");
    });

    // Guest join flow — cookie sign-in requires a real HTTP response, not a Blazor SignalR circuit,
    // same reason /account/login is a native form POST rather than a Blazor button click.
    // The QR code links to the bare /join page (not a code-carrying deep link) — scanning is a
    // shortcut to the entry form, not a substitute for typing the code.
    app.MapPost("/account/join", async (HttpContext ctx, GuestAccessService guestSvc) =>
    {
        var form      = await ctx.Request.ReadFormAsync();
        var code      = form["code"].ToString();
        var firstName = form["firstName"].ToString();
        var lastName  = form["lastName"].ToString();

        var ev = await guestSvc.ValidateCodeAsync(code);
        // Keep the return URL across a failed attempt (e.g. a check-in QR scan → join → typo).
        var back = LocalReturnUrl(form["returnUrl"]) is { } r ? $"&returnUrl={Uri.EscapeDataString(r)}" : "";
        if (ev is null) return Results.Redirect("/join?error=true" + back);

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            return Results.Redirect("/join?error=name" + back);

        var guest = await guestSvc.GetOrCreateGuestAsync(firstName, lastName);
        await guestSvc.RecordVisitAsync(guest.GuestAttendeeId, ev.EventId);
        await guestSvc.SignInGuestAsync(ctx, ev, guest);
        return Results.Redirect(LocalReturnUrl(form["returnUrl"]) ?? "/hub/schedule");
    }).AllowAnonymous();

    // Serve sponsor logos stored as bytea in the database
    app.MapGet("/sponsors/logo/{id:guid}", async (Guid id, SponsorService svc) =>
    {
        var s = await svc.GetByIdAsync(id);
        if (s?.LogoData is null) return Results.NotFound();
        return Results.File(s.LogoData, s.LogoContentType ?? "image/jpeg");
    });

    // Staff directory photos, by person (UserId). Only served for people shown in some event's
    // Hub directory, since the route is anonymous (the Hub page is also used by guests).
    app.MapGet("/staff-photo/{id:guid}", async (Guid id, IDbContextFactory<AppDbContext> factory) =>
    {
        using var db = factory.CreateDbContext();
        var photo = await db.Users.AsNoTracking()
            .Where(u => u.UserId == id && u.PhotoData != null && db.EventStaff.Any(s => s.UserId == id && s.ShowInDirectory))
            .Select(u => new { u.PhotoData, u.PhotoContentType })
            .FirstOrDefaultAsync();
        return photo is null ? Results.NotFound() : Results.File(photo.PhotoData!, photo.PhotoContentType ?? "image/jpeg");
    }).AllowAnonymous();

    // Any person's photo, for the Team page's editor (Admins only).
    app.MapGet("/admin/person-photo/{id:guid}", async (Guid id, IDbContextFactory<AppDbContext> factory) =>
    {
        using var db = factory.CreateDbContext();
        var photo = await db.Users.AsNoTracking().Where(u => u.UserId == id && u.PhotoData != null)
            .Select(u => new { u.PhotoData, u.PhotoContentType }).FirstOrDefaultAsync();
        return photo is null ? Results.NotFound() : Results.File(photo.PhotoData!, photo.PhotoContentType ?? "image/jpeg");
    }).RequireAuthorization(policy => policy.RequireRole("Admin"));

    app.MapGet("/location-image/{id:guid}", async (Guid id, IDbContextFactory<AppDbContext> factory) =>
    {
        using var db = factory.CreateDbContext();
        var loc = await db.Locations.FindAsync(id);
        if (loc?.ImageData is null) return Results.NotFound();
        return Results.File(loc.ImageData, loc.ImageContentType ?? "image/jpeg");
    }).AllowAnonymous();

    // Theme images uploaded on /admin/theme. Anonymous because the login page and guest nav
    // show the logo. URLs carry ?v=<UpdatedAt ticks>, so a day of caching never serves a stale image.
    app.MapGet("/theme-logo/{id:guid}", async (Guid id, HttpContext ctx, IDbContextFactory<AppDbContext> factory) =>
    {
        using var db = factory.CreateDbContext();
        var theme = await db.Themes.FindAsync(id);
        if (theme?.LogoData is null) return Results.NotFound();
        ctx.Response.Headers.CacheControl = "public, max-age=86400";
        return Results.File(theme.LogoData, theme.LogoContentType ?? "image/png");
    }).AllowAnonymous();

    app.MapGet("/theme-banner/{id:guid}", async (Guid id, HttpContext ctx, IDbContextFactory<AppDbContext> factory) =>
    {
        using var db = factory.CreateDbContext();
        var theme = await db.Themes.FindAsync(id);
        if (theme?.BannerData is null) return Results.NotFound();
        ctx.Response.Headers.CacheControl = "public, max-age=86400";
        return Results.File(theme.BannerData, theme.BannerContentType ?? "image/png");
    }).AllowAnonymous();

    app.MapGet("/admin/events/{id:guid}/guest-qr", async (Guid id, HttpRequest req, IDbContextFactory<AppDbContext> factory, GuestAccessService guestSvc) =>
    {
        using var db = factory.CreateDbContext();
        var ev = await db.Events.FindAsync(id);
        if (ev?.GuestCode is null) return Results.NotFound();

        // Links to the bare /join entry form, not a code-carrying deep link — scanning still
        // requires typing the code shown on the flyer, by design.
        var joinUrl = $"{req.Scheme}://{req.Host}/join";
        var png = guestSvc.GenerateJoinQrPng(joinUrl);
        return Results.File(png, "image/png");
    }).RequireAuthorization(policy => policy.RequireRole("Admin"));

    // Check-in QR for a tracked item. Creates the item's code on first use. The URL points at
    // /checkin/{code} (a random code, not the item id, so QR-only items can't be reached by id).
    app.MapGet("/admin/attendance/item/{id:guid}/qr.png", async (Guid id, HttpRequest req, IConfiguration config,
        AttendanceService attendance, GuestAccessService guestSvc) =>
    {
        var code = await attendance.EnsureCheckInCodeAsync(id);
        if (code is null) return Results.NotFound();
        var png = guestSvc.GenerateJoinQrPng($"{PublicBaseUrl.From(req, config)}/checkin/{code}");
        return Results.File(png, "image/png");
    }).RequireAuthorization(policy => policy.RequireRole("Admin"));

    // Schedule spreadsheet template for the active event (dropdowns for its days, types, locations).
    app.MapGet("/admin/schedule/template.xlsx", async (ActiveEventService activeEvents, ScheduleImportService imports) =>
    {
        var ev = await activeEvents.GetActiveEventAsync();
        var file = ev is null ? null : await imports.BuildTemplateAsync(ev.EventId);
        return file is { } f
            ? Results.File(f.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", f.FileName)
            : Results.NotFound();
    }).RequireAuthorization(policy => policy.RequireRole("Admin"));

    // Attendance CSV downloads — file responses need a real HTTP endpoint, not a Blazor circuit.
    app.MapGet("/admin/attendance/item/{id:guid}/csv", async (Guid id, AttendanceService svc) =>
    {
        var csv = await svc.BuildItemCsvAsync(id);
        return csv is { } f ? Results.File(f.Content, "text/csv; charset=utf-8", f.FileName) : Results.NotFound();
    }).RequireAuthorization(policy => policy.RequireRole("Admin"));

    app.MapGet("/admin/attendance/event/{id:guid}/csv", async (Guid id, AttendanceService svc) =>
    {
        var csv = await svc.BuildEventCsvAsync(id);
        return csv is { } f ? Results.File(f.Content, "text/csv; charset=utf-8", f.FileName) : Results.NotFound();
    }).RequireAuthorization(policy => policy.RequireRole("Admin"));

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

    app.MapGet("/hub/documents/{id:guid}/pdf", async (Guid id, HttpContext ctx, DocumentService svc) =>
    {
        var doc = await svc.GetByIdAsync(id);
        if (doc is null) return Results.NotFound();
        if (!string.IsNullOrEmpty(doc.VisibleRoles))
        {
            var allowed = doc.VisibleRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var userRoles = ctx.User.Claims
                .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                .Select(c => c.Value);
            if (!ctx.User.IsInRole("Admin") && !userRoles.Any(r => allowed.Contains(r, StringComparer.OrdinalIgnoreCase)))
                return Results.Forbid();
        }
        return Results.File(doc.Data, doc.ContentType, doc.OriginalFileName ?? $"{doc.Title}.pdf");
    }).RequireAuthorization();

    app.MapGet("/api/vapid-public-key", (IConfiguration config) =>
        Results.Ok(new { key = config["Vapid:PublicKey"] }));

    app.MapPost("/api/push/subscribe", async (HttpContext ctx, PushNotificationService pushSvc, GuestAccessService guestSvc) =>
    {
        var form = await ctx.Request.ReadFromJsonAsync<PushSubscribeRequest>();
        if (form is null) return Results.BadRequest();
        Guid? userId = null;
        if (Guid.TryParse(ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid))
            userId = uid;
        var guestAttendeeId = guestSvc.GetGuestAttendeeId(ctx.User);
        // A subscription with no owner would never be targeted (legacy guest cookie without a GuestAttendeeId).
        if (userId is null && guestAttendeeId is null) return Results.Unauthorized();
        await pushSvc.SubscribeAsync(form.Endpoint, form.P256dh, form.Auth, userId, guestAttendeeId);
        return Results.Ok();
    });

    app.MapPost("/api/push/unsubscribe", async (HttpContext ctx, PushNotificationService pushSvc) =>
    {
        var form = await ctx.Request.ReadFromJsonAsync<PushUnsubscribeRequest>();
        if (form is null) return Results.BadRequest();
        await pushSvc.UnsubscribeAsync(form.Endpoint);
        return Results.Ok();
    });

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

// Only same-site paths are allowed as a post-sign-in redirect ("/checkin/ABC"), never "//evil.com"
// or "/\evil.com", which browsers treat as another host.
static string? LocalReturnUrl(string? url) =>
    !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\")
        ? url
        : null;

// Railway injects a postgres:// URI — convert to Npgsql connection string format
static string ConvertPostgresUri(string uri)
{
    var u = new Uri(uri);
    var userInfo = u.UserInfo.Split(':');
    return $"Host={u.Host};Port={u.Port};Database={u.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
}

record PushSubscribeRequest(string Endpoint, string P256dh, string Auth);
record PushUnsubscribeRequest(string Endpoint);
