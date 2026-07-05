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
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", p => p.RequireRole("Admin"));
});

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

app.MapPost("/client-api/auth/change-password", async (HttpContext ctx, UserRepository users, ChangePwRequest req) =>
{
    if (ctx.User?.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    var user = await users.GetByUsernameAsync(ctx.User.Identity!.Name ?? "");
    if (user == null) return Results.Unauthorized();
    if (user.PasswordHash != FeuerwehrListen.Services.AuthenticationService.HashPassword(req.OldPassword ?? ""))
        return Results.Json(new { ok = false });
    user.PasswordHash = FeuerwehrListen.Services.AuthenticationService.HashPassword(req.NewPassword ?? "");
    await users.UpdateAsync(user);
    return Results.Json(new { ok = true });
}).RequireAuthorization().DisableAntiforgery();

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

// ================== Listen-Verwaltung (angemeldete Nutzer) ==================
var listMgmt = app.MapGroup("/client-api").RequireAuthorization().DisableAntiforgery();

listMgmt.MapGet("/closed-lists", async (AttendanceListRepository att, OperationListRepository op, FireSafetyWatchRepository fsw) =>
    Results.Json(new
    {
        operations = (await op.GetClosedAsync()).OrderByDescending(l => l.ClosedAt).Select(l => new { id = l.Id, title = $"{NextcloudService.StripLeadingYear(l.OperationNumber, l.AlertTime.Year)} - {l.Keyword}", sub = l.Address ?? "", closedAt = l.ClosedAt, href = $"/operation/{l.Id}" }),
        attendance = (await att.GetClosedAsync()).OrderByDescending(l => l.ClosedAt).Select(l => new { id = l.Id, title = l.Title, sub = l.Unit, closedAt = l.ClosedAt, href = $"/attendance/{l.Id}" }),
        watches = (await fsw.GetClosedAsync()).OrderByDescending(l => l.ClosedAt).Select(l => new { id = l.Id, title = l.Name, sub = l.Location, closedAt = l.ClosedAt, href = $"/firesafetywatches/{l.Id}" })
    }));

listMgmt.MapGet("/archived-lists", async (AttendanceListRepository att, OperationListRepository op, FireSafetyWatchRepository fsw) =>
    Results.Json(new
    {
        operations = (await op.GetArchivedAsync()).OrderByDescending(l => l.ClosedAt).Select(l => new { id = l.Id, title = $"{NextcloudService.StripLeadingYear(l.OperationNumber, l.AlertTime.Year)} - {l.Keyword}", sub = l.Address ?? "", closedAt = l.ClosedAt, href = $"/operation/{l.Id}" }),
        attendance = (await att.GetArchivedAsync()).OrderByDescending(l => l.ClosedAt).Select(l => new { id = l.Id, title = l.Title, sub = l.Unit, closedAt = l.ClosedAt, href = $"/attendance/{l.Id}" }),
        watches = (await fsw.GetArchivedAsync()).OrderByDescending(l => l.ClosedAt).Select(l => new { id = l.Id, title = l.Name, sub = l.Location, closedAt = l.ClosedAt, href = $"/firesafetywatches/{l.Id}" })
    }));

listMgmt.MapPost("/list/{type}/{id:int}/archive", async (string type, int id, AttendanceListRepository att, OperationListRepository op, FireSafetyWatchRepository fsw) =>
{
    switch (type)
    {
        case "attendance": var a = await att.GetByIdAsync(id); if (a != null) { a.IsArchived = true; await att.UpdateAsync(a); } break;
        case "operation": var o = await op.GetByIdAsync(id); if (o != null) { o.IsArchived = true; await op.UpdateAsync(o); } break;
        case "firesafety": var w = await fsw.GetByIdAsync(id); if (w != null) { w.IsArchived = true; await fsw.UpdateAsync(w); } break;
        default: return Results.BadRequest();
    }
    return Results.Ok();
});

listMgmt.MapDelete("/list/{type}/{id:int}", async (string type, int id, AttendanceListRepository att, OperationListRepository op, FireSafetyWatchRepository fsw) =>
{
    switch (type)
    {
        case "attendance": await att.DeleteAsync(id); break;
        case "operation": await op.DeleteAsync(id); break;
        case "firesafety": await fsw.DeleteAsync(id); break;
        default: return Results.BadRequest();
    }
    return Results.Ok();
});

// ================== ADMIN-CRUD (Cookie-Auth, Rolle Admin) ==================
var admin = app.MapGroup("/client-api/admin").RequireAuthorization("Admin").DisableAntiforgery();

// --- Fahrzeuge ---
admin.MapGet("/vehicles", async (VehicleRepository repo) =>
    Results.Json((await repo.GetAllAsync()).Select(v => new { id = v.Id, name = v.Name, callSign = v.CallSign, type = v.Type.ToString(), isActive = v.IsActive, createdAt = v.CreatedAt })));
admin.MapPost("/vehicles", async (VehicleRepository repo, VehicleReq r) =>
{
    var id = await repo.CreateAsync(new Vehicle { Name = r.Name.Trim(), CallSign = r.CallSign.Trim(), Type = Enum.TryParse<VehicleType>(r.Type, out var t) ? t : VehicleType.Sonstige, IsActive = r.IsActive, CreatedAt = DateTime.Now });
    return Results.Json(new { id });
});
admin.MapPut("/vehicles/{id:int}", async (int id, VehicleRepository repo, VehicleReq r) =>
{
    var v = await repo.GetByIdAsync(id); if (v == null) return Results.NotFound();
    v.Name = r.Name.Trim(); v.CallSign = r.CallSign.Trim(); v.Type = Enum.TryParse<VehicleType>(r.Type, out var t) ? t : v.Type; v.IsActive = r.IsActive;
    await repo.UpdateAsync(v); return Results.Ok();
});
admin.MapDelete("/vehicles/{id:int}", async (int id, VehicleRepository repo) => { await repo.DeleteAsync(id); return Results.Ok(); });

// --- Stichwoerter ---
admin.MapGet("/keywords", async (KeywordRepository repo) =>
    Results.Json((await repo.GetAllAsync()).Select(k => new { id = k.Id, name = k.Name, description = k.Description })));
admin.MapPost("/keywords", async (KeywordRepository repo, KeywordReq r) =>
{
    var id = await repo.CreateAsync(new Keyword { Name = r.Name.Trim(), Description = r.Description, IsActive = true, CreatedAt = DateTime.Now });
    return Results.Json(new { id });
});
admin.MapPut("/keywords/{id:int}", async (int id, KeywordRepository repo, KeywordReq r) =>
{
    var k = await repo.GetByIdAsync(id); if (k == null) return Results.NotFound();
    k.Name = r.Name.Trim(); k.Description = r.Description; await repo.UpdateAsync(k); return Results.Ok();
});
admin.MapDelete("/keywords/{id:int}", async (int id, KeywordRepository repo) => { await repo.DeleteAsync(id); return Results.Ok(); });
admin.MapGet("/keywords/{id:int}/requirements", async (int id, PersonalRequirementRepository repo) =>
    Results.Json((await repo.GetByKeywordIdAsync(id)).Select(r => new { functionDefId = r.FunctionDefId, minimumCount = r.MinimumCount, isRequired = r.IsRequired })));
admin.MapPost("/keywords/{id:int}/requirements", async (int id, PersonalRequirementRepository repo, List<ReqItem> body) =>
{
    await repo.DeleteByKeywordIdAsync(id);
    foreach (var r in body.Where(x => x.MinimumCount > 0 || x.IsRequired))
        await repo.CreateAsync(new PersonalRequirement { KeywordId = id, FunctionDefId = r.FunctionDefId, MinimumCount = r.MinimumCount, IsRequired = r.IsRequired, CreatedAt = DateTime.Now });
    return Results.Ok();
});

// --- Funktionen ---
admin.MapGet("/functions", async (OperationFunctionRepository repo) =>
    Results.Json((await repo.GetAllAsync()).Select(f => new { id = f.Id, name = f.Name, isDefault = f.IsDefault })));
admin.MapPost("/functions", async (OperationFunctionRepository repo, FunctionReq r) =>
{
    var id = await repo.CreateAsync(new OperationFunctionDef { Name = r.Name.Trim(), IsDefault = r.IsDefault });
    return Results.Json(new { id });
});
admin.MapPut("/functions/{id:int}", async (int id, OperationFunctionRepository repo, FunctionReq r) =>
{
    await repo.UpdateAsync(new OperationFunctionDef { Id = id, Name = r.Name.Trim(), IsDefault = r.IsDefault }); return Results.Ok();
});
admin.MapDelete("/functions/{id:int}", async (int id, OperationFunctionRepository repo) => { await repo.DeleteAsync(id); return Results.Ok(); });

// --- API-Keys ---
admin.MapGet("/apikeys", async (ApiKeyRepository repo) =>
    Results.Json((await repo.GetAllAsync()).Select(k => new { id = k.Id, key = k.Key, description = k.Description, isActive = k.IsActive, createdAt = k.CreatedAt })));
admin.MapPost("/apikeys", async (ApiKeyRepository repo, ApiKeyReq r) =>
{
    var key = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace("=", "").Replace("+", "").Replace("/", "") + Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace("=", "").Replace("+", "").Replace("/", "");
    var id = await repo.CreateAsync(new ApiKey { Key = key, Description = r.Description?.Trim() ?? "", IsActive = true, CreatedAt = DateTime.Now });
    return Results.Json(new { id, key });
});
admin.MapPost("/apikeys/{id:int}/toggle", async (int id, ApiKeyRepository repo) =>
{
    var k = await repo.GetByIdAsync(id); if (k == null) return Results.NotFound();
    k.IsActive = !k.IsActive; await repo.UpdateAsync(k); return Results.Json(new { isActive = k.IsActive });
});
admin.MapDelete("/apikeys/{id:int}", async (int id, ApiKeyRepository repo) => { await repo.DeleteAsync(id); return Results.Ok(); });

// --- Mitglieder ---
admin.MapGet("/members", async (MemberRepository repo) =>
{
    var members = await repo.GetAllAsync();
    var units = await repo.GetUnitsForMembersAsync(members.Select(m => m.Id));
    return Results.Json(members.OrderBy(m => m.LastName).ThenBy(m => m.FirstName).Select(m => new
    {
        id = m.Id, memberNumber = m.MemberNumber, firstName = m.FirstName, lastName = m.LastName, isActive = m.IsActive,
        units = units.TryGetValue(m.Id, out var u) && u.Count > 0 ? u : (m.UnitNumber.HasValue ? new List<int> { m.UnitNumber.Value } : new List<int>())
    }));
});
admin.MapPost("/members", async (MemberRepository repo, MemberReq r) =>
{
    var units = (r.Units ?? new()).Where(u => u >= 1 && u <= 9).OrderBy(u => u).ToList();
    int? primary = units.Count > 0 ? units.First() : null;
    var id = await repo.CreateAsync(new Member { MemberNumber = r.MemberNumber.Trim(), FirstName = r.FirstName.Trim(), LastName = r.LastName.Trim(), IsActive = r.IsActive, UnitNumber = primary, CreatedAt = DateTime.Now });
    await repo.SetUnitsForMemberAsync(id, units, primary);
    return Results.Json(new { id });
});
admin.MapPut("/members/{id:int}", async (int id, MemberRepository repo, MemberReq r) =>
{
    var m = await repo.GetByIdAsync(id); if (m == null) return Results.NotFound();
    var units = (r.Units ?? new()).Where(u => u >= 1 && u <= 9).OrderBy(u => u).ToList();
    int? primary = units.Count > 0 ? units.First() : null;
    m.MemberNumber = r.MemberNumber.Trim(); m.FirstName = r.FirstName.Trim(); m.LastName = r.LastName.Trim(); m.IsActive = r.IsActive; m.UnitNumber = primary;
    await repo.UpdateAsync(m);
    await repo.SetUnitsForMemberAsync(id, units, primary);
    return Results.Ok();
});
admin.MapDelete("/members/{id:int}", async (int id, MemberRepository repo) => { await repo.DeleteAsync(id); return Results.Ok(); });
admin.MapPost("/members/import-csv", async (MemberRepository repo, CsvReq r) =>
{
    var lines = (r.Csv ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
    if (lines.Length < 2) return Results.Json(new { imported = 0, skipped = 0, errors = new[] { "Keine Datenzeilen." } });
    var existing = (await repo.GetAllAsync()).Select(m => m.MemberNumber.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
    int imported = 0, skipped = 0; var errors = new List<string>();
    for (var i = 1; i < lines.Length; i++)
    {
        var parts = lines[i].Trim().Split(';');
        if (parts.Length < 3) { errors.Add($"Zeile {i + 1}: zu wenige Spalten."); continue; }
        var num = parts[0].Trim(); var fn = parts[1].Trim(); var ln = parts[2].Trim();
        if (num == "" || fn == "" || ln == "") { errors.Add($"Zeile {i + 1}: Pflichtfeld leer."); continue; }
        if (existing.Contains(num)) { skipped++; continue; }
        var active = parts.Length < 4 || parts[3].Trim().ToLowerInvariant() is "ja" or "true" or "1" or "";
        var allUnits = new List<int>();
        if (parts.Length >= 5)
            foreach (var t in parts[4].Split(new[] { ',', ' ', '|', '/' }, StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(t.Trim(), out var pu) && pu >= 1 && pu <= 9 && !allUnits.Contains(pu)) allUnits.Add(pu);
        int? primary = allUnits.Count > 0 ? allUnits.First() : null;
        var newId = await repo.CreateAsync(new Member { MemberNumber = num, FirstName = fn, LastName = ln, IsActive = active, UnitNumber = primary, CreatedAt = DateTime.Now });
        if (allUnits.Count > 0) await repo.SetUnitsForMemberAsync(newId, allUnits, primary);
        existing.Add(num); imported++;
    }
    return Results.Json(new { imported, skipped, errors });
});

// --- Benutzer ---
admin.MapGet("/users", async (UserRepository repo) =>
    Results.Json((await repo.GetAllAsync()).Select(u => new { id = u.Id, username = u.Username, firstName = u.FirstName, lastName = u.LastName, role = u.Role.ToString(), hasQr = !string.IsNullOrEmpty(u.QrAuthCode), hasPin = !string.IsNullOrEmpty(u.AdminPin) })));
admin.MapPost("/users", async (UserRepository repo, UserReq r) =>
{
    var id = await repo.CreateAsync(new User
    {
        Username = r.Username.Trim(),
        FirstName = r.FirstName?.Trim() ?? "", LastName = r.LastName?.Trim() ?? "",
        Role = Enum.TryParse<UserRole>(r.Role, out var role) ? role : UserRole.User,
        PasswordHash = FeuerwehrListen.Services.AuthenticationService.HashPassword(r.Password ?? ""),
        QrAuthCode = string.IsNullOrWhiteSpace(r.QrAuthCode) ? null : r.QrAuthCode.Trim(),
        AdminPin = string.IsNullOrWhiteSpace(r.AdminPin) ? null : r.AdminPin.Trim()
    });
    return Results.Json(new { id });
});
admin.MapPut("/users/{id:int}", async (int id, UserRepository repo, UserReq r) =>
{
    var u = await repo.GetByIdAsync(id); if (u == null) return Results.NotFound();
    u.Username = r.Username.Trim(); u.FirstName = r.FirstName?.Trim() ?? ""; u.LastName = r.LastName?.Trim() ?? "";
    u.Role = Enum.TryParse<UserRole>(r.Role, out var role) ? role : u.Role;
    if (!string.IsNullOrWhiteSpace(r.Password)) u.PasswordHash = FeuerwehrListen.Services.AuthenticationService.HashPassword(r.Password);
    u.QrAuthCode = string.IsNullOrWhiteSpace(r.QrAuthCode) ? null : r.QrAuthCode.Trim();
    u.AdminPin = string.IsNullOrWhiteSpace(r.AdminPin) ? null : r.AdminPin.Trim();
    await repo.UpdateAsync(u); return Results.Ok();
});
admin.MapDelete("/users/{id:int}", async (int id, UserRepository repo) => { await repo.DeleteAsync(id); return Results.Ok(); });

// --- Statistik ---
admin.MapGet("/statistics", async (StatisticsService stats) =>
{
    var list = await stats.GetListStatisticsAsync();
    var top = await stats.GetTopParticipantsAsync(null, 10);
    var members = await stats.GetMemberStatisticsAsync();
    return Results.Json(new
    {
        list = new { list.TotalLists, list.OpenLists, list.ClosedLists, list.ArchivedLists, list.AverageParticipants, list.TotalParticipants },
        top = top.Select(t => new { t.MemberName, t.MemberNumber, t.ParticipationCount, t.Percentage }),
        members = members.Select(m => new { m.MemberName, m.MemberNumber, m.TotalAttendance, m.TotalOperations, m.AttendancePercentage })
    });
});

// --- Einstellungen (Key-Value) ---
admin.MapGet("/settings", async (SettingsService settings) =>
    Results.Json(await settings.GetAllSettingsAsync()));
admin.MapPost("/settings", async (SettingsService settings, IWebHostEnvironment env, Dictionary<string, string> body) =>
{
    foreach (var kv in body)
        await settings.UpdateSettingAsync(kv.Key, kv.Value ?? "");
    // manifest.json App-Name aktualisieren
    if (body.TryGetValue(SettingKeys.BrandingAppName, out var appName))
    {
        try
        {
            var path = Path.Combine(env.WebRootPath, "manifest.json");
            if (File.Exists(path))
            {
                var json = await File.ReadAllTextAsync(path);
                json = System.Text.RegularExpressions.Regex.Replace(json, "\"name\":\\s*\"[^\"]*\"",
                    $"\"name\": \"{(string.IsNullOrWhiteSpace(appName) ? "Feuerwehr Listen" : appName)}\"");
                await File.WriteAllTextAsync(path, json);
            }
        }
        catch { }
    }
    return Results.Ok();
});

// --- Geplante Listen ---
admin.MapGet("/scheduled", async (ScheduledListRepository repo) =>
    Results.Json((await repo.GetAllAsync()).OrderBy(s => s.ScheduledEventTime).Select(s => new
    {
        id = s.Id, type = s.Type.ToString(), title = s.Title, unit = s.Unit, unitNumber = s.UnitNumber,
        operationNumber = s.OperationNumber, keyword = s.Keyword,
        eventTime = s.ScheduledEventTime, minutesBefore = s.MinutesBeforeEvent,
        openTime = s.ScheduledEventTime.AddMinutes(-s.MinutesBeforeEvent), isProcessed = s.IsProcessed
    })));
admin.MapPost("/scheduled", async (ScheduledListRepository repo, ScheduledReq r) =>
{
    var isAtt = r.Type == "Attendance";
    var id = await repo.CreateAsync(new ScheduledList
    {
        Type = isAtt ? ScheduledListType.Attendance : ScheduledListType.Operation,
        Title = r.Title?.Trim() ?? "", Unit = r.Unit?.Trim() ?? "", Description = r.Description?.Trim() ?? "",
        OperationNumber = r.OperationNumber?.Trim() ?? "", Keyword = r.Keyword?.Trim() ?? "",
        UnitNumber = isAtt ? r.UnitNumber : null,
        ScheduledEventTime = r.EventTime, MinutesBeforeEvent = r.MinutesBefore, IsProcessed = false, CreatedAt = DateTime.Now
    });
    return Results.Json(new { id });
});
admin.MapDelete("/scheduled/{id:int}", async (int id, ScheduledListRepository repo) => { await repo.DeleteAsync(id); return Results.Ok(); });

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

// Einsatzliste: Detail + Fahrzeuge + Funktionen + Eintraege
app.MapGet("/client-api/operation/{id:int}", async (int id, OperationListRepository repo, OperationEntryRepository entryRepo, OperationEntryFunctionRepository efRepo, VehicleRepository vehicleRepo, OperationFunctionRepository funcRepo) =>
{
    var list = await repo.GetByIdAsync(id);
    if (list == null) return Results.NotFound();
    var entries = await entryRepo.GetByListIdAsync(id);
    var funcMap = await efRepo.GetFunctionsForEntriesAsync(entries.Select(e => e.Id).ToList());
    var vehicles = await vehicleRepo.GetActiveAsync();
    var funcs = await funcRepo.GetAllAsync();
    return Results.Json(new
    {
        id = list.Id,
        number = NextcloudService.StripLeadingYear(list.OperationNumber, list.AlertTime.Year),
        keyword = list.Keyword,
        address = list.Address,
        isOpen = list.Status == ListStatus.Open,
        entries = entries.Select(e => new
        {
            id = e.Id,
            name = e.NameOrId,
            vehicle = e.Vehicle,
            breathing = e.WithBreathingApparatus,
            functions = funcMap.TryGetValue(e.Id, out var fl) ? fl.Select(f => f.Name).ToArray() : Array.Empty<string>()
        }),
        vehicles = vehicles.Select(v => new { id = v.Id, name = v.Name }),
        functions = funcs.Select(f => new { id = f.Id, name = f.Name })
    });
});

// Mitglied fuer Einsatz-Eintrag aufloesen (danach waehlt der Client Fahrzeug + Funktionen)
app.MapPost("/client-api/operation/{id:int}/resolve", async (int id, MemberRepository memberRepo, OperationResolveRequest req) =>
{
    var input = (req.Code ?? "").Trim();
    if (input.Length == 0) return Results.Json(new { status = "notfound" });
    var o = input.LastIndexOf('('); var c = input.LastIndexOf(')');
    if (o >= 0 && c > o)
    {
        var inside = input.Substring(o + 1, c - o - 1).Trim();
        if (inside.Length > 0 && await memberRepo.GetByMemberNumberAsync(inside) != null) input = inside;
    }
    Member? member = null;
    if (int.TryParse(input, out _)) member = await memberRepo.GetByMemberNumberAsync(input);
    if (member == null)
    {
        var found = await memberRepo.SearchAsync(input);
        if (found.Count == 1) member = found[0];
        else if (found.Count > 1)
            return Results.Json(new { status = "choose-member", members = found.Select(m => new { id = m.Id, name = $"{m.FirstName} {m.LastName}", number = m.MemberNumber }) });
    }
    if (member == null) return Results.Json(new { status = "notfound" });
    return Results.Json(new { status = "found", member = new { id = member.Id, name = $"{member.FirstName} {member.LastName}", number = member.MemberNumber } });
}).DisableAntiforgery();

// Einsatz-Eintrag speichern (mit Fahrzeug + Funktionen)
app.MapPost("/client-api/operation/{id:int}/add", async (int id, OperationListRepository repo, OperationEntryRepository entryRepo, OperationEntryFunctionRepository efRepo, MemberRepository memberRepo, VehicleRepository vehicleRepo, OperationAddRequest req) =>
{
    var list = await repo.GetByIdAsync(id);
    if (list == null) return Results.NotFound();
    var member = await memberRepo.GetByIdAsync(req.MemberId);
    if (member == null) return Results.Json(new { status = "notfound" });

    var entries = await entryRepo.GetByListIdAsync(id);
    if (entries.Any(e => e.NameOrId.Contains($"({member.MemberNumber})")))
        return Results.Json(new { status = "duplicate", name = $"{member.FirstName} {member.LastName}" });

    var vehicleName = "Ohne Fahrzeug";
    if (!req.NoVehicle && req.VehicleId is int vid)
        vehicleName = (await vehicleRepo.GetByIdAsync(vid))?.Name ?? "Ohne Fahrzeug";

    var entryId = await entryRepo.CreateAsync(new OperationEntry
    {
        OperationListId = id,
        NameOrId = $"{member.FirstName} {member.LastName} ({member.MemberNumber})",
        Vehicle = vehicleName,
        Function = OperationFunction.Trupp,
        WithBreathingApparatus = req.BreathingApparatus,
        EnteredAt = DateTime.Now
    });
    if (req.FunctionIds is { Count: > 0 })
        await efRepo.SetFunctionsForEntryAsync(entryId, req.FunctionIds);

    return Results.Json(new { status = "added", name = $"{member.FirstName} {member.LastName}" });
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

public record VehicleReq(string Name, string CallSign, string Type, bool IsActive);
public record KeywordReq(string Name, string? Description);
public record FunctionReq(string Name, bool IsDefault);
public record ReqItem(int FunctionDefId, int MinimumCount, bool IsRequired);
public record MemberReq(string MemberNumber, string FirstName, string LastName, bool IsActive, List<int>? Units);
public record CsvReq(string? Csv);
public record ApiKeyReq(string? Description);
public record UserReq(string Username, string? FirstName, string? LastName, string Role, string? Password, string? QrAuthCode, string? AdminPin);
public record ScheduledReq(string Type, string? Title, string? Unit, string? Description, int? UnitNumber, string? OperationNumber, string? Keyword, DateTime EventTime, int MinutesBefore);
public record AuthLoginRequest(string? Username, string? Password);
public record AuthQrRequest(string? Code, string? Pin);
public record ChangePwRequest(string? OldPassword, string? NewPassword);
public record FeedbackRequest(int OperationId, string? Text);
public record CreateAttendanceRequest(string Title, string Unit, string? Description, int? UnitNumber);
public record CreateOperationRequest(string OperationNumber, string Keyword, DateTime AlertTime, string? Address);
public record AttendanceAddRequest(string? Code, int? MemberId, int? TargetListId);
public record OperationResolveRequest(string? Code);
public record OperationAddRequest(int MemberId, int? VehicleId, bool NoVehicle, bool BreathingApparatus, List<int>? FunctionIds);

public partial class Program { }