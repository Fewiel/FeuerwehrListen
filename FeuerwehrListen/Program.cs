using FeuerwehrListen.Components;
using FeuerwehrListen.Models;
using FeuerwehrListen.Data;
using FeuerwehrListen.Services;
using FeuerwehrListen.Repositories;
using FeuerwehrListen.Middleware;
using LinqToDB;
using LinqToDB.AspNet;
using LinqToDB.AspNet.Logging;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using PdfSharp.Fonts;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
{
    builder.Configuration.AddJsonFile("appsettings.Production.json", optional: false, reloadOnChange: true);
}

GlobalFontSettings.FontResolver = new FontResolver();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        // Getrennte Circuits laenger aufbewahren: Safari/iPad legt Seiten schnell schlafen.
        // Nach dem Aufwachen findet der Client seinen Circuit wieder (Reconnect), statt dass
        // Blazor.reconnect() fehlschlaegt und die Seite komplett neu laden muss.
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(6);
        options.DisconnectedCircuitMaxRetained = 100;
    })
    .AddHubOptions(options =>
    {
        // Standard sind 32 KB. Digital gezeichnete Unterschriften (PNG-DataURL vom Canvas)
        // kommen per JS-Interop zurück und können größer sein -> sonst bricht der Circuit
        // beim Speichern ab ("hängt fest / verliert die Verbindung").
        options.MaximumReceiveMessageSize = 5 * 1024 * 1024; // 5 MB

        // Neuere Safari-Versionen (iPadOS 17/18) drosseln JS-Timer aggressiv (Energiesparen,
        // Display dimmt, Tab kurz inaktiv). Der Keep-Alive-Ping des Clients kommt dann zu
        // spaet und der Server beendete den Circuit nach 30s (Standard) -> auf neuen iPads
        // "Verbindung unterbrochen" bei jedem Klick, Buttons/Login/Navigation tot.
        // 120s Toleranz ueberbrueckt die Drosselung; Server pingt weiter alle 15s.
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(120);
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        options.HandshakeTimeout = TimeSpan.FromSeconds(30);
    })
    // WASM-Fundament: erlaubt einzelnen Seiten den Render-Mode InteractiveWebAssembly
    // (Client laeuft im Browser, Daten ueber Fast-Endpoints - kein SignalR).
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddScoped<FeuerwehrListen.Services.AuthenticationService>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<FeuerwehrListen.Services.AuthenticationService>());
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore();

// Cookie-Auth fuer den WASM-Client (Admin-Bereich). Additiv - die bestehende
// serverseitige AuthenticationService/QR-Anmeldung bleibt unangetastet.
builder.Services.AddAuthentication("FwCookie")
    .AddCookie("FwCookie", options =>
    {
        options.Cookie.Name = "fw_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        // API statt Redirects: 401/403 zurueckgeben.
        options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Feuerwehr Listen API", 
        Version = "v1",
        Description = "REST API für externe Tools zur Verwaltung von Anwesenheits- und Einsatzlisten"
    });
    c.AddSecurityDefinition("ApiKey", new()
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Name = "X-API-Key",
        Description = "API Key für Authentifizierung"
    });
    c.AddSecurityRequirement(new()
    {
        {
            new()
            {
                Reference = new()
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<DownloadTokenService>();

var dbProvider = builder.Configuration["DatabaseSettings:Provider"];
var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING") 
                       ?? (dbProvider == "MySQL"
                           ? builder.Configuration["DatabaseSettings:MySQLConnection"]
                           : builder.Configuration["DatabaseSettings:SQLiteConnection"]);

builder.Services.AddLinqToDBContext<AppDbConnection>((provider, options) =>
{
    if (dbProvider == "MySQL")
    {
        return options.UseMySqlConnector(connectionString!);
    }
    else
    {
        return options.UseSQLite(connectionString!);
    }
});

builder.Services.AddFluentMigratorCore()
    .ConfigureRunner(rb =>
    {
        if (dbProvider == "MySQL")
        {
            rb.AddMySql5().WithGlobalConnectionString(connectionString);
        }
        else
        {
            rb.AddSQLite().WithGlobalConnectionString(connectionString);
        }
        rb.ScanIn(typeof(Program).Assembly).For.Migrations();
    })
    .AddLogging(lb => lb.AddFluentMigratorConsole());

builder.Services.AddScoped<AttendanceListRepository>();
builder.Services.AddScoped<OperationListRepository>();
builder.Services.AddScoped<AttendanceEntryRepository>();
builder.Services.AddScoped<OperationEntryRepository>();
builder.Services.AddScoped<MemberRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<ApiKeyRepository>();
builder.Services.AddScoped<ScheduledListRepository>();
builder.Services.AddScoped<VehicleRepository>();
builder.Services.AddScoped<OperationFunctionRepository>();
builder.Services.AddScoped<OperationEntryFunctionRepository>();
builder.Services.AddScoped<KeywordRepository>();
builder.Services.AddScoped<PersonalRequirementRepository>();
builder.Services.AddScoped<FireSafetyWatchRepository>();
builder.Services.AddScoped<FireSafetyWatchRequirementRepository>();
builder.Services.AddScoped<FireSafetyWatchEntryRepository>();
builder.Services.AddScoped<SettingsRepository>();
builder.Services.AddScoped<DefectRepository>();
builder.Services.AddScoped<OperationReportRepository>();
builder.Services.AddScoped<StatisticsService>();
builder.Services.AddScoped<PdfExportService>();
builder.Services.AddScoped<GeocodingService>();
builder.Services.AddScoped<PersonalRequirementsService>();
builder.Services.AddScoped<UnitAssignmentService>();
builder.Services.AddScoped<EmailSenderService>();
builder.Services.AddScoped<FeedbackService>();
builder.Services.AddScoped<ListNotificationService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<NextcloudService>();

builder.Services.AddSingleton<SidebarService>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddHostedService<ScheduledListBackgroundService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
    runner.MigrateUp();
}

// Pre-warm settings cache after migrations
var settingsService = app.Services.GetRequiredService<SettingsService>();
await settingsService.InitializeAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => 
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Feuerwehr Listen API v1");
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<ApiKeyAuthMiddleware>();

app.UseAntiforgery();

app.MapControllers();
app.MapGet("/token", (HttpContext ctx, DownloadTokenService tokenSvc, string path) =>
{
    var token = tokenSvc.CreateToken(path);
    return Results.Text(token, "text/plain");
});

// --- Client-API (Proof-of-Concept) ---------------------------------------------------
// Zustandslose JSON-Endpoints fuer einen circuit-freien Client (kein SignalR).
// BEWUSST getrennt von der externen /api (X-API-Key) - die bleibt voellig unveraendert;
// "/client-api" matcht die ApiKey-Middleware (StartsWithSegments "/api") nicht.
app.MapGet("/client-api/open-lists", async (
    AttendanceListRepository attRepo,
    OperationListRepository opRepo,
    FireSafetyWatchRepository fswRepo,
    DefectRepository defectRepo,
    SettingsService settings) =>
{
    var operations = (await opRepo.GetOpenAsync())
        .OrderByDescending(x => x.AlertTime)
        .Select(o => new
        {
            id = o.Id,
            title = string.IsNullOrWhiteSpace(o.Keyword) ? o.OperationNumber : o.Keyword,
            sub = o.Address ?? "",
            time = o.AlertTime,
            href = $"/operation/{o.Id}"
        });

    var attendance = (await attRepo.GetOpenAsync())
        .OrderByDescending(x => x.CreatedAt)
        .Select(a => new
        {
            id = a.Id,
            title = a.Title,
            sub = a.UnitNumber.HasValue ? settings.GetUnitLabel(a.UnitNumber.Value) : a.Unit,
            time = a.CreatedAt,
            href = $"/attendance/{a.Id}"
        });

    var watches = (await fswRepo.GetAllAsync())
        .Where(w => !w.IsArchived && w.Status == FeuerwehrListen.Models.ListStatus.Open && w.EventDateTime >= DateTime.Now)
        .OrderBy(w => w.EventDateTime)
        .Select(w => new
        {
            id = w.Id,
            title = w.Name,
            sub = w.Location ?? "",
            time = w.EventDateTime,
            href = $"/firesafetywatches/{w.Id}"
        });

    var openDefects = settings.IsModuleVisible(SettingKeys.VisibilityDefects)
        ? await defectRepo.GetCountAsync(DefectStatus.Open) + await defectRepo.GetCountAsync(DefectStatus.InProgress)
        : 0;

    return Results.Json(new
    {
        serverTime = DateTime.Now,
        operations,
        attendance,
        watches,
        openDefects
    });
});

// Layout-/Navigations-Kontext fuer den WASM-Client (Modul-Sichtbarkeit + Branding).
app.MapGet("/client-api/app-context", (SettingsService settings) =>
{
    string? branding(string key)
    {
        var v = settings.GetSetting(key);
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }
    return Results.Json(new
    {
        appName = branding(SettingKeys.BrandingAppName) ?? "Feuerwehr Listen",
        logoUrl = branding(SettingKeys.BrandingLogoUrl),
        modules = new
        {
            attendance = settings.IsModuleVisible(SettingKeys.VisibilityAttendance),
            operations = settings.IsModuleVisible(SettingKeys.VisibilityOperations),
            fireSafety = settings.IsModuleVisible(SettingKeys.VisibilityFireSafetyWatch),
            defects = settings.IsModuleVisible(SettingKeys.VisibilityDefects)
        },
        unitLabels = Enumerable.Range(1, 9).Select(i => new { number = i, label = settings.GetUnitLabel(i) })
    });
});

// --- Auth (Cookie) fuer den WASM-Admin-Bereich ---
app.MapPost("/client-api/auth/login", async (HttpContext ctx, UserRepository users, AuthLoginRequest req) =>
{
    var user = await users.GetByUsernameAsync(req.Username ?? "");
    if (user == null || user.PasswordHash != FeuerwehrListen.Services.AuthenticationService.HashPassword(req.Password ?? ""))
        return Results.Json(new { ok = false });
    await SignInUser(ctx, user);
    return Results.Json(new { ok = true });
}).DisableAntiforgery();

app.MapPost("/client-api/auth/qr", async (HttpContext ctx, UserRepository users, AuthQrRequest req) =>
{
    var user = await users.GetByQrAuthCodeAsync((req.Code ?? "").Trim());
    if (user == null) return Results.Json(new { status = "invalid" });
    if (user.Role == UserRole.Admin && !string.IsNullOrWhiteSpace(user.AdminPin))
    {
        if (string.IsNullOrWhiteSpace(req.Pin)) return Results.Json(new { status = "pin" });
        if (user.AdminPin != req.Pin.Trim()) return Results.Json(new { status = "badpin" });
    }
    await SignInUser(ctx, user);
    return Results.Json(new { status = "ok" });
}).DisableAntiforgery();

app.MapPost("/client-api/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync("FwCookie");
    return Results.Ok();
}).DisableAntiforgery();

app.MapGet("/client-api/auth/me", (HttpContext ctx) =>
{
    if (ctx.User?.Identity?.IsAuthenticated != true) return Results.Json(new { authenticated = false });
    return Results.Json(new
    {
        authenticated = true,
        username = ctx.User.Identity!.Name,
        isAdmin = ctx.User.IsInRole("Admin"),
        firstName = ctx.User.FindFirst("FirstName")?.Value,
        lastName = ctx.User.FindFirst("LastName")?.Value
    });
});

// Anwesenheitsliste: Detail + Eintraege
app.MapGet("/client-api/attendance/{id:int}", async (int id, AttendanceListRepository repo, AttendanceEntryRepository entryRepo, SettingsService settings) =>
{
    var list = await repo.GetByIdAsync(id);
    if (list == null) return Results.NotFound();
    var entries = await entryRepo.GetByListIdAsync(id);
    return Results.Json(new
    {
        id = list.Id,
        title = list.Title,
        unit = list.Unit,
        unitNumber = list.UnitNumber,
        unitAlias = list.UnitNumber.HasValue ? settings.GetUnitAlias(list.UnitNumber.Value) : null,
        description = list.Description,
        createdAt = list.CreatedAt,
        isOpen = list.Status == ListStatus.Open,
        entries = entries.Select(e => new { id = e.Id, name = e.NameOrId, enteredAt = e.EnteredAt, isExcused = e.IsExcused })
    });
});

// Anwesenheit eintragen (Mitglieds-Lookup + Einheiten-Umleitung + Duplikatpruefung serverseitig)
app.MapPost("/client-api/attendance/{id:int}/add", async (int id, AttendanceListRepository repo, AttendanceEntryRepository entryRepo, MemberRepository memberRepo, UnitAssignmentService unitSvc, AttendanceAddRequest req) =>
{
    var list = await repo.GetByIdAsync(id);
    if (list == null) return Results.NotFound();

    Member? member = null;
    if (req.MemberId is int mid)
    {
        member = await memberRepo.GetByIdAsync(mid);
    }
    else if (!string.IsNullOrWhiteSpace(req.Code))
    {
        var input = req.Code.Trim();
        var o = input.LastIndexOf('('); var c = input.LastIndexOf(')');
        if (o >= 0 && c > o)
        {
            var inside = input.Substring(o + 1, c - o - 1).Trim();
            if (inside.Length > 0 && await memberRepo.GetByMemberNumberAsync(inside) != null) input = inside;
        }
        if (int.TryParse(input, out _)) member = await memberRepo.GetByMemberNumberAsync(input);
        if (member == null)
        {
            var found = await memberRepo.SearchAsync(input);
            if (found.Count == 1) member = found[0];
            else if (found.Count > 1)
                return Results.Json(new { status = "choose-member", members = found.Select(m => new { id = m.Id, name = $"{m.FirstName} {m.LastName}", number = m.MemberNumber }) });
        }
    }
    if (member == null) return Results.Json(new { status = "notfound" });

    var name = $"{member.FirstName} {member.LastName}";
    int targetListId = req.TargetListId ?? id;

    if (req.TargetListId == null && list.Status == ListStatus.Open && list.UnitNumber.HasValue)
    {
        var units = await unitSvc.ResolveAllUnitNumbersAsync(member);
        if (!units.Contains(list.UnitNumber.Value) && units.Count > 0)
        {
            var avail = new List<AttendanceList>();
            foreach (var u in units)
                foreach (var l in await repo.GetAllOpenByUnitNumberAsync(u))
                    if (!avail.Any(a => a.Id == l.Id)) avail.Add(l);
            foreach (var l in await repo.GetOpenWithoutUnitAsync())
                if (!avail.Any(a => a.Id == l.Id)) avail.Add(l);

            if (avail.Count == 0) return Results.Json(new { status = "nomatch", units = string.Join(", ", units.Select(u => $"Einheit {u}")) });
            if (avail.Count == 1) targetListId = avail[0].Id;
            else return Results.Json(new { status = "choose-list", memberId = member.Id, name, lists = avail.Select(l => new { id = l.Id, title = l.Title }) });
        }
    }

    var targetEntries = await entryRepo.GetByListIdAsync(targetListId);
    if (targetEntries.Any(e => e.NameOrId.Contains($"({member.MemberNumber})")))
        return Results.Json(new { status = "duplicate", name });

    await entryRepo.CreateAsync(new AttendanceEntry
    {
        AttendanceListId = targetListId,
        NameOrId = $"{name} ({member.MemberNumber})",
        EnteredAt = DateTime.Now,
        IsExcused = false
    });
    return Results.Json(new { status = targetListId == id ? "added" : "redirected", name, listId = targetListId });
}).DisableAntiforgery();

// Brandsicherheitswachen-Liste
app.MapGet("/client-api/firesafetywatches", async (FireSafetyWatchRepository repo) =>
    Results.Json((await repo.GetAllWithStatusAsync())
        .Where(w => !w.IsArchived)
        .OrderBy(w => w.EventDateTime)
        .Select(w => new { id = w.Id, name = w.Name, location = w.Location, time = w.EventDateTime, required = w.TotalRequired, assigned = w.TotalAssigned })));

// Stichwoerter (fuer die Einsatzliste-Anlage)
app.MapGet("/client-api/keywords", async (KeywordRepository kw) =>
    Results.Json((await kw.GetAllAsync()).Select(k => new { name = k.Name, description = k.Description })));

// Anwesenheitsliste anlegen
app.MapPost("/client-api/attendance/create", async (AttendanceListRepository repo, CreateAttendanceRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Unit)) return Results.BadRequest();
    var id = await repo.CreateAsync(new AttendanceList
    {
        Title = req.Title.Trim(),
        Unit = req.Unit.Trim(),
        Description = req.Description ?? "",
        UnitNumber = req.UnitNumber,
        CreatedAt = DateTime.Now,
        Status = ListStatus.Open
    });
    return Results.Json(new { id });
}).DisableAntiforgery();

// Einsatzliste anlegen (Stichwort anlegen falls neu)
app.MapPost("/client-api/operation/create", async (OperationListRepository repo, KeywordRepository kw, CreateOperationRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.OperationNumber) || string.IsNullOrWhiteSpace(req.Keyword)) return Results.BadRequest();
    var name = req.Keyword.Trim();
    var all = await kw.GetAllAsync();
    var existing = all.FirstOrDefault(k => k.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    int keywordId;
    if (existing != null) { keywordId = existing.Id; name = existing.Name; }
    else keywordId = await kw.CreateAsync(new Keyword { Name = name, IsActive = true, CreatedAt = DateTime.Now });

    var id = await repo.CreateAsync(new OperationList
    {
        OperationNumber = req.OperationNumber.Trim(),
        Keyword = name,
        KeywordId = keywordId,
        AlertTime = req.AlertTime,
        CreatedAt = DateTime.Now,
        Status = ListStatus.Open,
        Address = string.IsNullOrWhiteSpace(req.Address) ? null : req.Address!.Trim()
    });
    return Results.Json(new { id });
}).DisableAntiforgery();

// Einsatz-Auswahl/-Suche fuer das Feedback (letzte 5 bzw. Suche nach Adresse/Stichwort/Nummer).
app.MapGet("/client-api/operations/recent", async (OperationListRepository opRepo, string? q) =>
{
    var ops = await opRepo.GetForFeedbackAsync();
    IEnumerable<OperationList> sel = ops;
    if (!string.IsNullOrWhiteSpace(q))
    {
        var s = q.Trim();
        sel = ops.Where(o => (o.Address ?? "").Contains(s, StringComparison.OrdinalIgnoreCase)
                          || (o.Keyword ?? "").Contains(s, StringComparison.OrdinalIgnoreCase)
                          || (o.OperationNumber ?? "").Contains(s, StringComparison.OrdinalIgnoreCase));
    }
    else sel = ops.Take(5);

    return Results.Json(sel.Take(25).Select(o => new
    {
        id = o.Id,
        keyword = o.Keyword,
        address = o.Address ?? "",
        number = NextcloudService.StripLeadingYear(o.OperationNumber, o.AlertTime.Year),
        time = o.AlertTime
    }));
});

// Feedback absenden (wie bisher: an in den Settings hinterlegte Empfaenger).
app.MapPost("/client-api/feedback", async (OperationListRepository opRepo, FeedbackService feedback, FeedbackRequest req) =>
{
    var op = await opRepo.GetByIdAsync(req.OperationId);
    if (op == null) return Results.NotFound();
    var result = await feedback.SendFeedbackAsync(op, req.Text ?? "");
    return Results.Json(new { result = result.ToString() });
}).DisableAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(FeuerwehrListen.Client._Imports).Assembly);

app.Run();

static async Task SignInUser(HttpContext ctx, User user)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.Name, user.Username),
        new(ClaimTypes.Role, user.Role.ToString()),
        new("FirstName", user.FirstName ?? ""),
        new("LastName", user.LastName ?? "")
    };
    var identity = new ClaimsIdentity(claims, "FwCookie", ClaimTypes.Name, ClaimTypes.Role);
    await ctx.SignInAsync("FwCookie", new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = true });
}

public record AuthLoginRequest(string? Username, string? Password);
public record AuthQrRequest(string? Code, string? Pin);
public record FeedbackRequest(int OperationId, string? Text);
public record CreateAttendanceRequest(string Title, string Unit, string? Description, int? UnitNumber);
public record CreateOperationRequest(string OperationNumber, string Keyword, DateTime AlertTime, string? Address);
public record AttendanceAddRequest(string? Code, int? MemberId, int? TargetListId);

public partial class Program { }