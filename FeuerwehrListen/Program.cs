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
    // Server-Komponenten bleiben registriert: liefern u. a. ProtectedLocalStorage, das die
    // bestehende serverseitige AuthenticationService/QR-Anmeldung benoetigt. Interaktive
    // Server-Circuits werden aber nicht mehr erzeugt (die App rendert global WebAssembly).
    .AddInteractiveServerComponents(options =>
    {
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(6);
        options.DisconnectedCircuitMaxRetained = 100;
    })
    .AddHubOptions(options =>
    {
        // Digital gezeichnete Unterschriften (PNG-DataURL vom Canvas) koennen groesser sein
        // als die Standard-32 KB.
        options.MaximumReceiveMessageSize = 5 * 1024 * 1024; // 5 MB
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(120);
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        options.HandshakeTimeout = TimeSpan.FromSeconds(30);
    })
    // WASM-Fundament: Client laeuft im Browser, Daten ueber Fast-Endpoints - kein SignalR.
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
// Einmal-Login-Tickets fuer den Cookie-SignIn per Browser-GET (Server-Modus, s. AuthTicketService).
builder.Services.AddSingleton<AuthTicketService>();

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
builder.Services.AddScoped<RoomRepository>();
builder.Services.AddScoped<CalendarRepository>();
builder.Services.AddScoped<CalendarService>();
builder.Services.AddScoped<StatisticsService>();
builder.Services.AddScoped<PdfExportService>();
// GeocodingService ueber IHttpClientFactory (typed client) - kein new HttpClient() je Scope.
// OSM/Nominatim verlangt einen aussagekraeftigen User-Agent.
builder.Services.AddHttpClient<GeocodingService>(client =>
{
    client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
    client.DefaultRequestHeaders.UserAgent.Clear();
    client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("FeuerwehrListen", "1.0"));
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.AddScoped<PersonalRequirementsService>();
builder.Services.AddScoped<UnitAssignmentService>();
builder.Services.AddScoped<EmailSenderService>();
builder.Services.AddScoped<FeedbackService>();
builder.Services.AddScoped<ListNotificationService>();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
// Server-seitiger HttpClient fuer server-gerenderte Client-Komponenten (Alt-Geraete /
// Server-Modus): BaseAddress = App selbst, Auth-Cookie wird weitergereicht. In WASM
// laeuft der Fetch im Browser und nutzt DIESE Registrierung nicht (nur beim Prerender/
// InteractiveServer).
builder.Services.AddSingleton<FeuerwehrListen.Services.AppBaseUrlProvider>();
builder.Services.AddSingleton<FeuerwehrListen.Services.AppUrlService>();
builder.Services.AddSingleton<FeuerwehrListen.Services.NetworkAccessService>();
builder.Services.AddSingleton<FeuerwehrListen.Services.InternalAuthSecret>();
builder.Services.AddScoped<HttpClient>(sp =>
{
    var accessor = sp.GetRequiredService<IHttpContextAccessor>();
    var baseProvider = sp.GetRequiredService<FeuerwehrListen.Services.AppBaseUrlProvider>();
    var authState = sp.GetRequiredService<AuthenticationStateProvider>();
    var secret = sp.GetRequiredService<FeuerwehrListen.Services.InternalAuthSecret>();
    var ctx = accessor.HttpContext;
    // BaseAddress bestimmen. Reihenfolge ist wichtig:
    // 1. HttpContext (Prerender und normale Requests) - der echte Host des Nutzers.
    // 2. NavigationManager: im Blazor-Server-Circuit gibt es keinen HttpContext, wohl
    //    aber die tatsaechlich aufgerufene Adresse des Nutzers.
    // 3. Erst zuletzt der prozessweite Provider.
    //
    // Punkt 2 ist nicht bloss Kosmetik: der Provider haelt den Host des ALLERERSTEN
    // Requests nach dem Start. Ohne ihn trugen alle Self-Calls im Server-Modus
    // (Alt-Geraete) diesen fremden Host - und damit griff dessen Host-Profil fuer
    // jeden Server-Modus-Nutzer, unabhaengig davon, welche Adresse er aufgerufen hat.
    string? baseUrl = null;
    if (ctx != null)
    {
        baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    }
    else
    {
        try
        {
            var nav = sp.GetService<Microsoft.AspNetCore.Components.NavigationManager>();
            if (nav != null && !string.IsNullOrEmpty(nav.BaseUri)) baseUrl = nav.BaseUri.TrimEnd('/');
        }
        catch { /* ausserhalb eines Circuits nicht verfuegbar */ }
    }
    if (string.IsNullOrEmpty(baseUrl)) baseUrl = baseProvider.BaseUrl;

    // Echten Nutzer-Host merken. Er wandert als X-Fw-Host mit dem Self-Call mit, damit
    // Host-Profile auch im Server-Modus fuer den richtigen Host ausgewertet werden -
    // unabhaengig davon, welche Zieladresse der Self-Call technisch verwendet.
    string? originHost = ctx?.Request.Host.Value;
    if (string.IsNullOrEmpty(originHost) && !string.IsNullOrEmpty(baseUrl)
        && Uri.TryCreate(baseUrl, UriKind.Absolute, out var ouri))
        originHost = ouri.IsDefaultPort ? ouri.Host : $"{ouri.Host}:{ouri.Port}";

    // Ist eine interne Adresse hinterlegt, laufen Self-Calls DARUEBER statt ueber die
    // oeffentliche Adresse. Wichtig hinter einem Reverse-Proxy mit Passwortschutz:
    // sonst ruft der Server sich selbst von aussen auf und scheitert an dessen Anmeldung.
    // Der echte Nutzer-Host bleibt in originHost erhalten.
    var internalBase = sp.GetRequiredService<SettingsService>().GetSetting(SettingKeys.AppInternalBaseUrl);
    if (!string.IsNullOrWhiteSpace(internalBase)) baseUrl = internalBase.Trim().TrimEnd('/');
    // Eigenen Host fuer die Zertifikatspruefung merken (s. u.).
    string? ownHost = null;
    if (!string.IsNullOrEmpty(baseUrl) && Uri.TryCreate(baseUrl, UriKind.Absolute, out var buri)) ownHost = buri.Host;
    var handler = new FeuerwehrListen.Services.SelfCookieHandler(accessor, authState, secret, baseProvider, originHost, baseUrl)
    {
        InnerHandler = new HttpClientHandler
        {
            // Selbstsignierte/beliebige Zertifikate NUR fuer den eigenen Host akzeptieren
            // (Self-Calls). Fremd-Hosts (z. B. der 3D-Tag-Helfer) werden regulaer geprueft.
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
            {
                var targetHost = message.RequestUri?.Host;
                if (ownHost != null && string.Equals(targetHost, ownHost, StringComparison.OrdinalIgnoreCase))
                    return true;
                return errors == System.Net.Security.SslPolicyErrors.None;
            }
        }
    };
    var client = new HttpClient(handler);
    if (!string.IsNullOrEmpty(baseUrl)) client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    return client;
});
builder.Services.AddScoped<NextcloudService>();

builder.Services.AddSingleton<SidebarService>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddScoped<TagRenderService>();
// AppContextService wird von Client-Komponenten (NavMenu, Home, …) injiziert. Beim
// Server-Rendering (iOS-12-/Server-Modus + Prerender) rendert der SERVER diese Komponenten,
// daher MUSS der Service auch in der Server-DI registriert sein - sonst wirft NavMenu beim
// Render "no registered service of type AppContextService" -> Circuit stirbt -> weisse Seite.
// Nutzt den server-seitigen scoped HttpClient (SelfCookieHandler).
builder.Services.AddScoped<FeuerwehrListen.Client.Services.AppContextService>();
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
// Basis-URL (scheme://host) fuer server-seitige Self-Calls waehrend eines Blazor-Server-
// Circuits (dort gibt es keinen HttpContext). Bevorzugt einen konfigurierten Wert
// (AppSettings:BaseUrl) - schuetzt vor Host-Header-Poisoning; nur als Fallback der Host-Header.
var configuredBaseUrl = app.Configuration["AppSettings:BaseUrl"];
app.Use(async (ctx, next) =>
{
    var p = ctx.RequestServices.GetRequiredService<FeuerwehrListen.Services.AppBaseUrlProvider>();
    if (string.IsNullOrEmpty(p.BaseUrl))
        p.BaseUrl = !string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? configuredBaseUrl.TrimEnd('/')
            : $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    await next();
});

app.UseStaticFiles();

app.UseAuthentication();

// Interne Identity fuer Server-Modus-Self-Calls: kein Auth-Cookie, aber gueltiger
// prozess-geheimer Header -> Identity aus X-Fw-User/Role setzen. Nur der server-eigene
// SelfCookieHandler kennt das Geheimnis; von aussen nicht faelschbar.
app.Use(async (ctx, next) =>
{
    if (!(ctx.User.Identity?.IsAuthenticated ?? false)
        && ctx.Request.Headers.TryGetValue("X-Fw-Internal", out var sec)
        && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(sec.ToString()),
                System.Text.Encoding.UTF8.GetBytes(ctx.RequestServices.GetRequiredService<FeuerwehrListen.Services.InternalAuthSecret>().Value)))
    {
        // NUR mit Benutzernamen eine Identity bauen. Ein ClaimsIdentity mit gesetztem
        // authenticationType gilt sonst auch ohne Namen als angemeldet - und weil das
        // interne Kennzeichen bei JEDEM Self-Call mitgeht (auch fuer Gaeste), waeren
        // damit Netz-Schranke und Host-Profile ausgehebelt.
        var internalUser = ctx.Request.Headers["X-Fw-User"].ToString();
        if (!string.IsNullOrWhiteSpace(internalUser))
        {
            var claims = new List<System.Security.Claims.Claim>
            {
                new(System.Security.Claims.ClaimTypes.Name, internalUser)
            };
            foreach (var r in ctx.Request.Headers["X-Fw-Role"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries))
                claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, r.Trim()));
            ctx.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(claims, "FwInternal"));
        }
    }
    await next();
});

app.UseAuthorization();

// Zugriffsschranke: Host-Profile (z. B. wachen.example.de zeigt nur Wachen) und die
// Netz-Schranke (ausserhalb der lokalen Netze nur freigegebene Module fuer Gaeste).
// Bewusst NACH der Authentifizierung, damit angemeldete Benutzer erkannt werden.
// /client-api/approve bleibt immer offen - der Link geht per Mail nach draussen.
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path;

    // Freigabe-Host: Seitenaufrufe ausserhalb des Freigabe-Wegs auf eine
    // Hinweisseite umleiten. Ohne das laedt zwar keine Daten mehr, die
    // Anwendungshuelle mit Navigation waere aber weiterhin sichtbar.
    // Nur echte Seitennavigationen umleiten (Accept: text/html) - Skripte,
    // Stylesheets und das Blazor-Framework muessen weiterhin ausgeliefert werden.
    if (!path.StartsWithSegments("/client-api"))
    {
        var accessPage = ctx.RequestServices.GetRequiredService<FeuerwehrListen.Services.NetworkAccessService>();
        if (accessPage.IsApproveOnlyHost(ctx)
            && !path.StartsWithSegments("/approve")
            && !path.StartsWithSegments("/nur-freigabe")
            && !path.StartsWithSegments("/_framework")
            && !path.StartsWithSegments("/_blazor")
            && !path.StartsWithSegments("/_content")
            && ctx.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.Redirect("/nur-freigabe");
            return;
        }
    }

    if (path.StartsWithSegments("/client-api"))
    {
        var access = ctx.RequestServices.GetRequiredService<FeuerwehrListen.Services.NetworkAccessService>();

        // Freigabe-Host: alles ausser dem Freigabe-Vorgang sperren. Bewusst als
        // Positivliste - eine reine Modulsperre wuerde Mitgliedersuche, Anmeldung
        // und den QR-Pruefer offen lassen, die nicht modulgebunden sind.
        if (access.IsApproveOnlyHost(ctx)
            && !FeuerwehrListen.Services.NetworkAccessService.IsApproveOnlyAllowedPath(path))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = "approve_only_host",
                message = "Ueber diese Adresse ist nur die Freigabe erreichbar."
            });
            return;
        }

        var module = FeuerwehrListen.Services.NetworkAccessService.ModuleForPath(path);
        if (module != null)
        {
            if (!access.GetAllowedModules(ctx).Contains(module))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsJsonAsync(new
                {
                    error = "module_blocked",
                    module,
                    message = "Dieser Bereich ist von hier aus nicht verfuegbar. Bitte im Feuerwehrnetz oeffnen oder anmelden."
                });
                return;
            }
        }
    }
    await next();
});

app.UseMiddleware<ApiKeyAuthMiddleware>();

app.UseAntiforgery();

app.MapControllers();
// Download-Token nur fuer angemeldete Nutzer und nur fuer Export-Pfade ausstellen.
// Frueher anonym + beliebiger Pfad -> jeder konnte Export-PDFs (inkl. Mitgliederliste)
// per Token abrufen. Interaktive Nutzer nutzen zusaetzlich die neuen Inline-PDF-Endpoints
// (/client-api/export/*), dieser Token-Weg bleibt fuer den externen /api/export-Zugriff.
app.MapGet("/token", (HttpContext ctx, DownloadTokenService tokenSvc, string path) =>
{
    if (string.IsNullOrEmpty(path) || !path.StartsWith("/api/export/", StringComparison.Ordinal))
        return Results.BadRequest("Ungueltiger Pfad");
    var token = tokenSvc.CreateToken(path);
    return Results.Text(token, "text/plain");
}).RequireAuthorization();

// --- Client-API (Proof-of-Concept) ---------------------------------------------------
// Zustandslose JSON-Endpoints fuer einen circuit-freien Client (kein SignalR).
// BEWUSST getrennt von der externen /api (X-API-Key) - die bleibt voellig unveraendert;
// "/client-api" matcht die ApiKey-Middleware (StartsWithSegments "/api") nicht.
app.MapGet("/client-api/open-lists", async (
    HttpContext http,
    AttendanceListRepository attRepo,
    OperationListRepository opRepo,
    FireSafetyWatchRepository fswRepo,
    FireSafetyWatchRequirementRepository fswReqRepo,
    DefectRepository defectRepo,
    CalendarRepository calRepo,
    VehicleRepository vehicleRepo,
    RoomRepository roomRepo,
    NetworkAccessService access,
    SettingsService settings) =>
{
    // Nach den erlaubten Modulen filtern - sonst wuerden von aussen Titel und Zeiten
    // gesperrter Listen auf der Startseite auftauchen.
    var allowed = access.GetAllowedModules(http);

    var operations = allowed.Contains(NetworkAccessService.ModuleOperations)
        ? (await opRepo.GetOpenAsync())
            .OrderByDescending(x => x.AlertTime)
            .Select(o => new ListItemDto(
                o.Id,
                string.IsNullOrWhiteSpace(o.Keyword) ? o.OperationNumber : o.Keyword,
                o.Address ?? "",
                o.AlertTime,
                $"/operation/{o.Id}"))
            .ToList()
        : new List<ListItemDto>();

    var attendance = allowed.Contains(NetworkAccessService.ModuleAttendance)
        ? (await attRepo.GetOpenAsync())
            .OrderByDescending(x => x.CreatedAt)
            .Select(a => new ListItemDto(
                a.Id,
                a.Title,
                a.UnitNumber.HasValue ? settings.GetUnitLabel(a.UnitNumber.Value) : a.Unit,
                a.CreatedAt,
                $"/attendance/{a.Id}"))
            .ToList()
        : new List<ListItemDto>();

    var watchList = allowed.Contains(NetworkAccessService.ModuleFireSafety)
        ? (await fswRepo.GetAllAsync())
            .Where(w => !w.IsArchived && w.Status == FeuerwehrListen.Models.ListStatus.Open && w.EventDateTime >= DateTime.Now)
            .OrderBy(w => w.EventDateTime)
            .ToList()
        : new List<FeuerwehrListen.Models.FireSafetyWatch>();

    // Fahrzeuge je Wache gebuendelt nachladen (eine Abfrage statt einer je Wache).
    var watchVehicles = await fswReqRepo.GetVehicleNamesForWatchesAsync(watchList.Select(w => w.Id).ToList());

    var watches = watchList
        .Select(w => new ListItemDto(
            w.Id,
            w.Name,
            w.Location ?? "",
            w.EventDateTime,
            $"/firesafetywatches/{w.Id}",
            watchVehicles.TryGetValue(w.Id, out var wv) ? wv : new List<string>()))
        .ToList();

    // Anstehende Kalendertermine (Dienste, Veranstaltungen, Buchungen) inkl. Fahrzeugen
    // und Raeumen - bisher tauchten sie in der Uebersicht gar nicht auf.
    var calendarItems = new List<ListItemDto>();
    if (allowed.Contains(NetworkAccessService.ModuleCalendar))
    {
        // Nur was HEUTE stattfindet. Die Uebersicht ist die Tagesansicht der Wache -
        // die Vorausschau gehoert in den Kalender. Mehrtaegige Termine erscheinen an
        // jedem Tag, an dem sie laufen.
        var today = DateTime.Now.Date;
        var tomorrow = today.AddDays(1);
        var events = (await calRepo.GetEventsInRangeAsync(today, tomorrow))
            .Where(e => e.Status != CalendarEventStatus.Abgelehnt)
            .OrderBy(e => e.StartTime)
            .Take(20)
            .ToList();

        if (events.Count > 0)
        {
            var res = await calRepo.GetResourcesForEventsAsync(events.Select(e => e.Id).ToList());
            var vehicleNames = (await vehicleRepo.GetAllAsync()).ToDictionary(v => v.Id, v => v.Name);
            var roomNames = (await roomRepo.GetAllAsync()).ToDictionary(r => r.Id, r => r.Name);

            foreach (var e in events)
            {
                var names = res.Where(r => r.CalendarEventId == e.Id)
                    .Select(r => r.ResourceKind == CalendarResourceKind.Vehicle
                        ? (vehicleNames.TryGetValue(r.ResourceId, out var vn) ? vn : null)
                        : (roomNames.TryGetValue(r.ResourceId, out var rn) ? rn : null))
                    .Where(n => n != null).Select(n => n!).Distinct().OrderBy(n => n).ToList();

                calendarItems.Add(new ListItemDto(
                    e.Id,
                    e.Title,
                    string.IsNullOrWhiteSpace(e.Location) ? TypeLabel(e.Type) : $"{TypeLabel(e.Type)} · {e.Location}",
                    e.StartTime,
                    "/kalender",
                    names));
            }
        }
    }

    static string TypeLabel(CalendarEventType t) => t switch
    {
        CalendarEventType.Dienst => "Dienst",
        CalendarEventType.Fahrzeugbuchung => "Fahrzeugbuchung",
        CalendarEventType.Raumbuchung => "Raumbuchung",
        _ => "Veranstaltung"
    };

    var openDefects = allowed.Contains(NetworkAccessService.ModuleDefects)
        ? await defectRepo.GetCountAsync(DefectStatus.Open) + await defectRepo.GetCountAsync(DefectStatus.InProgress)
        : 0;

    return Results.Json(new
    {
        serverTime = DateTime.Now,
        operations,
        attendance,
        watches,
        calendar = calendarItems,
        openDefects
    });
});

// Layout-/Navigations-Kontext fuer den WASM-Client (Modul-Sichtbarkeit + Branding).
app.MapGet("/client-api/app-context", (HttpContext http, SettingsService settings, NetworkAccessService access) =>
{
    string? branding(string key)
    {
        var v = settings.GetSetting(key);
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }
    // Die effektiv erlaubten Module melden (Host-Profil + Netz-Schranke), damit die
    // Oberflaeche nichts anbietet, was der Server anschliessend mit 403 ablehnt.
    var allowed = access.GetAllowedModules(http);
    return Results.Json(new
    {
        appName = branding(SettingKeys.BrandingAppName) ?? "Feuerwehr Listen",
        logoUrl = branding(SettingKeys.BrandingLogoUrl),
        modules = new
        {
            attendance = allowed.Contains(NetworkAccessService.ModuleAttendance),
            operations = allowed.Contains(NetworkAccessService.ModuleOperations),
            fireSafety = allowed.Contains(NetworkAccessService.ModuleFireSafety),
            defects = allowed.Contains(NetworkAccessService.ModuleDefects),
            calendar = allowed.Contains(NetworkAccessService.ModuleCalendar)
        },
        unitLabels = Enumerable.Range(1, 9).Select(i => new { number = i, label = settings.GetUnitLabel(i) })
    });
});

// --- Auth (Cookie) fuer den WASM-Admin-Bereich ---
// WICHTIG: Login/QR setzen NICHT mehr direkt den Cookie, sondern geben ein Einmal-Ticket
// zurueck. Der eigentliche Cookie-SignIn passiert in einer echten Browser-GET-Navigation
// (/client-api/auth/complete). Grund: Im Server-Modus (SignalR) laeuft dieser POST
// server-intern (SelfHttpClient) -> das Set-Cookie erreicht den Browser sonst nie.
app.MapPost("/client-api/auth/login", async (UserRepository users, AuthTicketService tickets, AuthLoginRequest req) =>
{
    var user = await users.GetByUsernameAsync(req.Username ?? "");
    if (user == null || !FeuerwehrListen.Services.AuthenticationService.VerifyPassword(user.PasswordHash, req.Password))
        return Results.Json(new { ok = false });
    return Results.Json(new { ok = true, ticket = tickets.CreateTicket(user.Id) });
}).DisableAntiforgery();

app.MapPost("/client-api/auth/qr", async (UserRepository users, AuthTicketService tickets, AuthQrRequest req) =>
{
    var user = await users.GetByQrAuthCodeAsync((req.Code ?? "").Trim());
    if (user == null) return Results.Json(new { status = "invalid" });
    if (user.Role == UserRole.Admin && !string.IsNullOrWhiteSpace(user.AdminPin))
    {
        if (string.IsNullOrWhiteSpace(req.Pin)) return Results.Json(new { status = "pin" });
        if (user.AdminPin != req.Pin.Trim()) return Results.Json(new { status = "badpin" });
    }
    return Results.Json(new { status = "ok", ticket = tickets.CreateTicket(user.Id) });
}).DisableAntiforgery();

// Login-ABSCHLUSS: echte Top-Level-Browser-GET -> hier wird der Cookie gesetzt, sodass
// das Set-Cookie im Server-Modus tatsaechlich im Browser ankommt. Kein RequireAuthorization
// (das ist ja der Anmeldevorgang selbst). Ticket ist einmalig und 60s gueltig.
app.MapGet("/client-api/auth/complete", async (HttpContext ctx, UserRepository users, AuthTicketService tickets, string? ticket, string? returnUrl) =>
{
    // Nach dem Cookie-SignIn zurueck zur Ausgangsseite (returnUrl) - so bleibt der QR-Direktlogin
    // auf einer Detailseite dort, statt auf / zu springen. Nur lokale Pfade (Open-Redirect-Schutz).
    var back = (!string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith("/") && !returnUrl.StartsWith("//")) ? returnUrl : "/";
    if (!tickets.TryConsume(ticket, out var userId)) return Results.Redirect("/login");
    var user = await users.GetByIdAsync(userId);
    if (user == null) return Results.Redirect("/login");
    await SignInUser(ctx, user);
    return Results.Redirect(back);
}).DisableAntiforgery();

app.MapPost("/client-api/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync("FwCookie");
    return Results.Ok();
}).DisableAntiforgery();

// Logout-ABSCHLUSS als echte Browser-GET: nur so wird der Cookie-Loesch-Header im Browser
// wirksam (analog complete, damit auch der Server-Modus den Cookie tatsaechlich entfernt).
app.MapGet("/client-api/auth/logout-redirect", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync("FwCookie");
    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapPost("/client-api/auth/change-password", async (HttpContext ctx, UserRepository users, ChangePwRequest req) =>
{
    if (ctx.User?.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    var user = await users.GetByUsernameAsync(ctx.User.Identity!.Name ?? "");
    if (user == null) return Results.Unauthorized();
    if (!FeuerwehrListen.Services.AuthenticationService.VerifyPassword(user.PasswordHash, req.OldPassword))
        return Results.Json(new { ok = false });
    user.PasswordHash = FeuerwehrListen.Services.AuthenticationService.HashPassword(req.NewPassword ?? "");
    await users.UpdateAsync(user);
    return Results.Json(new { ok = true });
}).RequireAuthorization().DisableAntiforgery();

// B14: Gescannten Code als BENUTZER-Ausweis identifizieren OHNE Login (kein Cookie, keine
// Anmeldung). Dient nur dazu, dem Client zu sagen, ob ein QR-Code zu einem User gehoert - damit
// er entscheiden kann, ob er das QuickActions-Menue (z. B. "Liste schliessen") anbietet. Die
// eigentlichen Admin-Aktionen laufen weiter ueber die normalen RequireAuthorization-Endpoints,
// d. h. QuickActions funktionieren nur, wenn der Nutzer ohnehin per Cookie angemeldet ist.
// Sicherheit: Bestaetigt lediglich Zugehoerigkeit + Rolle, verleiht KEINE Rechte.
app.MapGet("/client-api/auth/qr-identify", async (UserRepository users, string? code) =>
{
    var c = (code ?? "").Trim();
    if (c.Length == 0) return Results.Json(new { isUser = false });
    var user = await users.GetByQrAuthCodeAsync(c);
    if (user == null) return Results.Json(new { isUser = false });
    return Results.Json(new { isUser = true, username = user.Username, role = user.Role.ToString() });
});

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

// ================== Inline-PDF fuer interaktive (per Cookie angemeldete) Nutzer ==========
// Inline-PDF fuer interaktive Nutzer, ohne Token-await - behebt iOS-WASM-Download.
// Grund: Die Client-Seiten mussten bisher erst `await Http.GetStringAsync("token?...")`
// und DANN oeffnen; der await zerstoert auf iOS die User-Aktivierung und die
// Content-Disposition:attachment-Antwort wird blockiert. Diese Endpoints brauchen kein
// Token (Cookie-Auth reicht) und liefern das PDF INLINE (Results.File ohne Dateiname ->
// Content-Disposition: inline), sodass die Seite es direkt in der Klick-Geste oeffnen kann.
// Der externe /api/export-Weg (Token + Content-Disposition:attachment) bleibt unveraendert.
// PDF-Downloads fuer Anwesenheit/Einsatz/Einsatzbericht bewusst OHNE Login (Kiosk-Wunsch).
// Mitglieder-/Statistik-PDF bleiben Admin (personenbezogene Gesamtlisten).
app.MapGet("/client-api/export/attendance/{id:int}/pdf", async (int id, PdfExportService pdf) =>
    Results.File(await pdf.ExportAttendanceListAsync(id), "application/pdf"));

app.MapGet("/client-api/export/operation/{id:int}/pdf", async (int id, PdfExportService pdf) =>
    Results.File(await pdf.ExportOperationListAsync(id), "application/pdf"));

app.MapGet("/client-api/export/operation-report/{id:int}/pdf", async (int id, PdfExportService pdf) =>
    Results.File(await pdf.ExportOperationReportAsync(id), "application/pdf"));

app.MapGet("/client-api/export/members/pdf", async (PdfExportService pdf) =>
    Results.File(await pdf.ExportMemberListAsync(), "application/pdf"))
    .RequireAuthorization("Admin");

app.MapGet("/client-api/export/statistics/pdf", async (PdfExportService pdf,
    int? listType, DateTime? from, DateTime? to, int? unit) =>
{
    var lt = listType ?? 3; // Default wie ExportController.ExportStatisticsPdf
    var filter = new StatsFilter
    {
        ListType = Enum.IsDefined(typeof(StatListType), lt) ? (StatListType)lt : StatListType.All,
        From = from,
        To = to,
        Unit = unit ?? 0
    };
    return Results.File(await pdf.ExportStatisticsReportAsync(filter), "application/pdf");
}).RequireAuthorization("Admin");

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

// B6 (Sicherheit): Endgueltiges Loeschen ist Admin-only (Original: Archive.razor zeigte den
// Loesch-Button nur bei AuthService.IsAdmin). Daher explizit RequireAuthorization("Admin").
// Archivieren dagegen war im Original (ClosedLists.razor unter UserAuthCheck) fuer jeden
// angemeldeten Nutzer erlaubt und bleibt deshalb bei RequireAuthorization() der Gruppe.
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
}).RequireAuthorization("Admin");

// ================== ADMIN-CRUD (Cookie-Auth, Rolle Admin) ==================
var admin = app.MapGroup("/client-api/admin").RequireAuthorization("Admin").DisableAntiforgery();

// --- Fahrzeuge ---
admin.MapGet("/vehicles", async (VehicleRepository repo) =>
    Results.Json((await repo.GetAllAsync()).Select(v => new { id = v.Id, name = v.Name, callSign = v.CallSign, type = v.Type.ToString(), isActive = v.IsActive, showInOperations = v.ShowInOperations, isBookable = v.IsBookable, requiresApproval = v.RequiresApproval, approverEmails = v.ApproverEmails, createdAt = v.CreatedAt })));
admin.MapPost("/vehicles", async (VehicleRepository repo, VehicleReq r) =>
{
    var id = await repo.CreateAsync(new Vehicle
    {
        Name = r.Name.Trim(),
        CallSign = r.CallSign.Trim(),
        Type = Enum.TryParse<VehicleType>(r.Type, out var t) ? t : VehicleType.Sonstige,
        IsActive = r.IsActive,
        ShowInOperations = r.ShowInOperations,
        IsBookable = r.IsBookable,
        RequiresApproval = r.RequiresApproval,
        ApproverEmails = string.IsNullOrWhiteSpace(r.ApproverEmails) ? null : r.ApproverEmails.Trim(),
        CreatedAt = DateTime.Now
    });
    return Results.Json(new { id });
});
admin.MapPut("/vehicles/{id:int}", async (int id, VehicleRepository repo, VehicleReq r) =>
{
    var v = await repo.GetByIdAsync(id); if (v == null) return Results.NotFound();
    v.Name = r.Name.Trim(); v.CallSign = r.CallSign.Trim(); v.Type = Enum.TryParse<VehicleType>(r.Type, out var t) ? t : v.Type; v.IsActive = r.IsActive;
    v.ShowInOperations = r.ShowInOperations; v.IsBookable = r.IsBookable; v.RequiresApproval = r.RequiresApproval;
    v.ApproverEmails = string.IsNullOrWhiteSpace(r.ApproverEmails) ? null : r.ApproverEmails.Trim();
    await repo.UpdateAsync(v); return Results.Ok();
});

// --- Zugriffsschutz-Diagnose ---
// Zeigt fuer DIESEN Aufruf, was die App tatsaechlich sieht. Gedacht fuer die
// Einrichtung hinter einem Reverse-Proxy: dort ist die haeufigste Fehlerquelle,
// dass die Proxy-Adresse nicht stimmt oder der Host-Header nicht durchgereicht wird.
admin.MapGet("/access-check", (HttpContext http, NetworkAccessService access, SettingsService settings) =>
{
    var remote = http.Connection.RemoteIpAddress;
    var clientIp = access.GetClientIp(http);
    var host = http.Request.Host.Value ?? "";
    var profile = access.GetHostProfile(host);
    var allowed = access.GetAllowedModules(http);

    return Results.Json(new
    {
        host,
        remoteIp = remote?.ToString(),
        forwardedFor = http.Request.Headers["X-Forwarded-For"].ToString(),
        forwardedProto = http.Request.Headers["X-Forwarded-Proto"].ToString(),
        // Wird X-Forwarded-For ueberhaupt beruecksichtigt? Nur wenn der direkte
        // Absender als Proxy hinterlegt ist.
        proxyRecognised = remote != null && clientIp != null && !clientIp.Equals(remote),
        clientIp = clientIp?.ToString(),
        isTrustedNetwork = access.IsTrustedNetwork(http),
        restrictExternal = (settings.GetSetting(SettingKeys.SecurityRestrictExternal) ?? "").Equals("true", StringComparison.OrdinalIgnoreCase),
        hostProfileFound = profile != null,
        hostProfileModules = profile?.OrderBy(x => x).ToArray() ?? Array.Empty<string>(),
        approveOnlyHost = access.IsApproveOnlyHost(http),
        allowedModules = allowed.OrderBy(x => x).ToArray(),
        authenticated = http.User.Identity?.IsAuthenticated ?? false,
        configuredTrustedProxies = settings.GetSetting(SettingKeys.SecurityTrustedProxies) ?? "",
        configuredTrustedNetworks = string.IsNullOrWhiteSpace(settings.GetSetting(SettingKeys.SecurityTrustedNetworks))
            ? NetworkAccessService.DefaultTrustedNetworks + "  (Standard)"
            : settings.GetSetting(SettingKeys.SecurityTrustedNetworks)!
    });
});

// --- Raeume ---
admin.MapGet("/rooms", async (RoomRepository repo) =>
    Results.Json((await repo.GetAllAsync()).Select(x => new { id = x.Id, name = x.Name, description = x.Description, capacity = x.Capacity, requiresApproval = x.RequiresApproval, approverEmails = x.ApproverEmails, isActive = x.IsActive, createdAt = x.CreatedAt })));
admin.MapPost("/rooms", async (RoomRepository repo, RoomReq r) =>
{
    if (string.IsNullOrWhiteSpace(r.Name)) return Results.BadRequest();
    var id = await repo.CreateAsync(new Room
    {
        Name = r.Name.Trim(),
        Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim(),
        Capacity = r.Capacity,
        RequiresApproval = r.RequiresApproval,
        ApproverEmails = string.IsNullOrWhiteSpace(r.ApproverEmails) ? null : r.ApproverEmails.Trim(),
        IsActive = r.IsActive,
        CreatedAt = DateTime.Now
    });
    return Results.Json(new { id });
});
admin.MapPut("/rooms/{id:int}", async (int id, RoomRepository repo, RoomReq r) =>
{
    if (string.IsNullOrWhiteSpace(r.Name)) return Results.BadRequest();
    var x = await repo.GetByIdAsync(id); if (x == null) return Results.NotFound();
    x.Name = r.Name.Trim();
    x.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
    x.Capacity = r.Capacity;
    x.RequiresApproval = r.RequiresApproval;
    x.ApproverEmails = string.IsNullOrWhiteSpace(r.ApproverEmails) ? null : r.ApproverEmails.Trim();
    x.IsActive = r.IsActive;
    await repo.UpdateAsync(x); return Results.Ok();
});
admin.MapDelete("/rooms/{id:int}", async (int id, RoomRepository repo) => { await repo.DeleteAsync(id); return Results.Ok(); });
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
// B1: StrengthPosition (Zaehlstelle) wird als int (1=Zugfuehrer, 2=Gruppenfuehrer, 3=Mannschaft)
// uebertragen. Der PUT laedt den bestehenden Datensatz und aktualisiert nur die Felder, statt
// ein neues OperationFunctionDef ohne StrengthPosition zu erzeugen (das setzte die Zaehlstelle
// still auf den Default Mannschaft zurueck = Datenverlust-Bug).
admin.MapGet("/functions", async (OperationFunctionRepository repo) =>
    Results.Json((await repo.GetAllAsync()).Select(f => new { id = f.Id, name = f.Name, isDefault = f.IsDefault, strengthPosition = (int)f.StrengthPosition })));
admin.MapPost("/functions", async (OperationFunctionRepository repo, FunctionReq r) =>
{
    var id = await repo.CreateAsync(new OperationFunctionDef
    {
        Name = r.Name.Trim(),
        IsDefault = r.IsDefault,
        StrengthPosition = Enum.IsDefined(typeof(StrengthPosition), r.StrengthPosition) ? (StrengthPosition)r.StrengthPosition : StrengthPosition.Mannschaft
    });
    return Results.Json(new { id });
});
admin.MapPut("/functions/{id:int}", async (int id, OperationFunctionRepository repo, FunctionReq r) =>
{
    var f = await repo.GetByIdAsync(id); if (f == null) return Results.NotFound();
    f.Name = r.Name.Trim();
    f.IsDefault = r.IsDefault;
    f.StrengthPosition = Enum.IsDefined(typeof(StrengthPosition), r.StrengthPosition) ? (StrengthPosition)r.StrengthPosition : f.StrengthPosition;
    await repo.UpdateAsync(f); return Results.Ok();
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
// Tag-Vorschau (SVG: Body blau + Inlay rot an SCAD-Positionen, echter QR).
// Das eigentliche STL-Rendering (OpenSCAD) laeuft NICHT hier, sondern client-seitig
// ueber einen lokalen Helfer (fwtag-helper) - die kleine VM soll nicht rendern.
admin.MapGet("/members/{id:int}/tag-preview", async (int id, MemberRepository repo, TagRenderService tags) =>
{
    var m = await repo.GetByIdAsync(id);
    if (m == null) return Results.NotFound();
    return Results.Content(tags.BuildPreviewSvg(m), "image/svg+xml");
});
// Name/Nummer fuer den lokalen Helfer (Client baut daraus die OpenSCAD-Parameter).
admin.MapGet("/members/{id:int}/tag-info", async (int id, MemberRepository repo) =>
{
    var m = await repo.GetByIdAsync(id);
    if (m == null) return Results.NotFound();
    return Results.Json(new { number = (m.MemberNumber ?? "").Trim(), name = TagRenderService.DisplayName(m) });
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
// B15: GET liefert die Email mit. POST/PUT uebernehmen die Email. Beim Anlegen kann der Client
// ein selbst generiertes Klartext-Passwort mitschicken (B17: einmalige Anzeige im Frontend);
// ist SendWelcomeMail gesetzt und eine Email vorhanden, werden die Zugangsdaten (Benutzername +
// Klartext-Passwort) per Mail versendet. Der Mail-Versand darf den Anlage-Vorgang nicht crashen.
admin.MapGet("/users", async (UserRepository repo) =>
    Results.Json((await repo.GetAllAsync()).Select(u => new { id = u.Id, username = u.Username, firstName = u.FirstName, lastName = u.LastName, email = u.Email, role = u.Role.ToString(), hasQr = !string.IsNullOrEmpty(u.QrAuthCode), hasPin = !string.IsNullOrEmpty(u.AdminPin), createdAt = u.CreatedAt })));
admin.MapPost("/users", async (UserRepository repo, EmailSenderService email, UserReq r) =>
{
    var user = new User
    {
        Username = r.Username.Trim(),
        FirstName = r.FirstName?.Trim() ?? "", LastName = r.LastName?.Trim() ?? "",
        Email = r.Email?.Trim() ?? "",
        Role = Enum.TryParse<UserRole>(r.Role, out var role) ? role : UserRole.User,
        PasswordHash = FeuerwehrListen.Services.AuthenticationService.HashPassword(r.Password ?? ""),
        QrAuthCode = string.IsNullOrWhiteSpace(r.QrAuthCode) ? null : r.QrAuthCode.Trim(),
        AdminPin = string.IsNullOrWhiteSpace(r.AdminPin) ? null : r.AdminPin.Trim(),
        CreatedAt = DateTime.Now
    };
    var id = await repo.CreateAsync(user);

    // Willkommens-Mail mit Zugangsdaten (nur wenn angefordert, Email + Klartext-Passwort vorhanden).
    if (r.SendWelcomeMail && !string.IsNullOrWhiteSpace(user.Email) && !string.IsNullOrWhiteSpace(r.Password))
    {
        try
        {
            var body = "Hallo " + user.FirstName + " " + user.LastName + ",\n\n" +
                "Ihr Konto bei Feuerwehr Listen wurde erfolgreich eingerichtet.\n\n" +
                "Ihre Zugangsdaten:\n" +
                "  Benutzername: " + user.Username + "\n" +
                "  Passwort:     " + r.Password + "\n\n" +
                "Bitte melden Sie sich an und ändern Sie Ihr Passwort unter \"Passwort ändern\" in der Navigation.\n\n" +
                "Mit freundlichen Grüßen\n" +
                "Feuerwehr Listen";
            await email.SendAsync(new[] { user.Email }, "Ihre Zugangsdaten - Feuerwehr Listen", body);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Willkommens-Mail an {Email} konnte nicht versendet werden", user.Email);
        }
    }
    return Results.Json(new { id });
});
admin.MapPut("/users/{id:int}", async (int id, UserRepository repo, UserReq r) =>
{
    var u = await repo.GetByIdAsync(id); if (u == null) return Results.NotFound();
    u.Username = r.Username.Trim(); u.FirstName = r.FirstName?.Trim() ?? ""; u.LastName = r.LastName?.Trim() ?? "";
    u.Email = r.Email?.Trim() ?? "";
    u.Role = Enum.TryParse<UserRole>(r.Role, out var role) ? role : u.Role;
    if (!string.IsNullOrWhiteSpace(r.Password)) u.PasswordHash = FeuerwehrListen.Services.AuthenticationService.HashPassword(r.Password);
    u.QrAuthCode = string.IsNullOrWhiteSpace(r.QrAuthCode) ? null : r.QrAuthCode.Trim();
    u.AdminPin = string.IsNullOrWhiteSpace(r.AdminPin) ? null : r.AdminPin.Trim();
    await repo.UpdateAsync(u); return Results.Ok();
});
admin.MapDelete("/users/{id:int}", async (int id, UserRepository repo) => { await repo.DeleteAsync(id); return Results.Ok(); });

// --- Statistik ---
// Vollstaendige Auswertung MIT Filter (Listentyp/Zeitraum/Einheit). Die Fahrzeug-,
// Funktions-, Atemschutz-, Stichwort-, Requirements- und Trend-Sektionen gelten nur fuer
// Einsatzlisten (filter.IncludeOperations); bei anderem ListType liefern die Service-
// Methoden leere Listen/Nullwerte - die UI blendet sie dann aus.
admin.MapGet("/statistics", async (StatisticsService stats, int? listType, DateTime? from, DateTime? to, int? unit) =>
{
    var lt = listType ?? 0;
    var filter = new StatsFilter
    {
        ListType = Enum.IsDefined(typeof(StatListType), lt) ? (StatListType)lt : StatListType.Operation,
        From = from,
        To = to,
        Unit = unit ?? 0
    };

    var list = await stats.GetListStatisticsAsync(filter);
    var top = await stats.GetTopParticipantsAsync(filter, 10);
    var members = await stats.GetMemberStatisticsAsync(filter);
    var vehicles = await stats.GetVehicleStatisticsAsync(filter);
    var functions = await stats.GetFunctionStatisticsAsync(filter);
    var breathing = await stats.GetBreathingApparatusStatisticsAsync(filter);
    var keywords = await stats.GetKeywordStatisticsAsync(filter);
    var requirements = await stats.GetPersonalRequirementsStatisticsAsync(filter);
    var typeTrends = await stats.GetParticipationTrendByTypeAsync(filter);
    var opComposition = await stats.GetOperationCompositionAsync(filter, 100);

    return Results.Json(new
    {
        includeOperations = filter.IncludeOperations,
        list = new
        {
            list.TotalLists, list.OpenLists, list.ClosedLists, list.ArchivedLists,
            list.AverageParticipants, list.TotalParticipants
        },
        top = top.Select(t => new { t.MemberName, t.MemberNumber, t.ParticipationCount, t.Percentage }),
        members = members.Select(m => new
        {
            m.MemberName, m.MemberNumber, m.TotalAttendance, m.TotalOperations,
            m.AttendancePercentage, m.LastParticipation
        }),
        vehicles = vehicles.Select(v => new { v.VehicleName, v.UsageCount, v.UsagePercentage, v.AverageCrew }),
        functions = functions.Select(f => new { f.FunctionName, f.Count, f.Percentage }),
        breathing = new { breathing.WithApparatus, breathing.WithoutApparatus, breathing.WithApparatusPercentage },
        keywords = keywords.Select(k => new
        {
            k.KeywordName, k.UsageCount, k.UsagePercentage, k.TotalOperations,
            k.OperationsWithRequirements, k.OperationsFulfillingRequirements, k.RequirementsFulfillmentRate
        }),
        requirements = new
        {
            requirements.TotalOperations,
            requirements.OperationsWithKeywords,
            requirements.OperationsWithRequirements,
            requirements.OperationsFulfillingRequirements,
            requirements.RequirementsFulfillmentRate,
            keywordSummaries = requirements.KeywordSummaries.Select(s => new
            {
                s.KeywordName, s.OperationsCount, s.RequirementsDefined,
                s.RequirementsFulfilled, s.FulfillmentRate
            })
        },
        typeTrends = typeTrends.Select(t => new
        {
            t.Einsatzart, t.OperationCount, t.AverageParticipants, t.Series, t.TrendPercent
        }),
        // Einsatz-Uebersicht: verschachtelte Dictionaries (Funktion->Anzahl) werden von
        // System.Text.Json direkt als JSON-Objekte (string->int) serialisiert.
        operationComposition = opComposition.Select(o => new
        {
            o.OperationNumber, o.Keyword, o.KeywordId, o.Address, o.TotalParticipants,
            o.FunctionCounts, o.NoVehicleFunctionCounts,
            o.WithVehicleTruppCount, o.WithoutVehicleTruppCount,
            o.HasPersonalRequirements, o.RequirementsFulfillmentRate, o.RequirementsFulfilled
        })
    });
});

// --- Einstellungen (Key-Value) ---
admin.MapGet("/settings", async (SettingsService settings) =>
    Results.Json(await settings.GetAllSettingsAsync()));
admin.MapPost("/settings", async (SettingsService settings, IWebHostEnvironment env, Dictionary<string, string> body) =>
{
    foreach (var kv in body)
        await settings.UpdateSettingAsync(kv.Key, kv.Value ?? "");
    // manifest.json App-Name aktualisieren. Per System.Text.Json (JsonNode) statt Regex -
    // sonst koennen Sonderzeichen im Namen ($0, Anfuehrungszeichen) die Datei korrumpieren.
    if (body.TryGetValue(SettingKeys.BrandingAppName, out var appName))
    {
        try
        {
            var path = Path.Combine(env.WebRootPath, "manifest.json");
            if (File.Exists(path))
            {
                var raw = await File.ReadAllTextAsync(path);
                var node = System.Text.Json.Nodes.JsonNode.Parse(raw);
                if (node is System.Text.Json.Nodes.JsonObject obj)
                {
                    obj["name"] = string.IsNullOrWhiteSpace(appName) ? "Feuerwehr Listen" : appName;
                    var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                    await File.WriteAllTextAsync(path, obj.ToJsonString(opts));
                }
            }
        }
        catch (Exception ex)
        {
            // Read-only Container o. ae.: tolerant fangen, aber loggen statt still schlucken.
            app.Logger.LogWarning(ex, "manifest.json konnte nicht aktualisiert werden");
        }
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
// B5: Geplante Liste sofort "Jetzt oeffnen" - erzeugt sofort die echte Anwesenheits-/Einsatzliste
// (gleiche Erzeugungslogik wie ScheduledListBackgroundService) und setzt IsProcessed=true.
// Rueckgabe: neue Listen-Id + Typ ("attendance"/"operation"), damit der Client dorthin navigieren kann.
admin.MapPost("/scheduled/{id:int}/process", async (int id, ScheduledListRepository repo, AttendanceListRepository att, OperationListRepository op) =>
{
    var s = await repo.GetByIdAsync(id);
    if (s == null) return Results.NotFound();
    if (s.IsProcessed) return Results.BadRequest(new { error = "Bereits verarbeitet." });

    int newId; string type;
    if (s.Type == ScheduledListType.Attendance)
    {
        newId = await att.CreateAsync(new AttendanceList
        {
            Title = s.Title,
            Unit = s.Unit,
            Description = s.Description,
            UnitNumber = s.UnitNumber,
            CreatedAt = DateTime.Now,
            Status = ListStatus.Open
        });
        type = "attendance";
    }
    else
    {
        newId = await op.CreateAsync(new OperationList
        {
            OperationNumber = s.OperationNumber,
            Keyword = s.Keyword,
            AlertTime = s.ScheduledEventTime,
            CreatedAt = DateTime.Now,
            Status = ListStatus.Open
        });
        type = "operation";
    }

    s.IsProcessed = true;
    await repo.UpdateAsync(s);
    return Results.Json(new { id = newId, type });
});

// B4: Nextcloud "Verbindung testen". Body optional (url/user/pass); ohne Body werden die
// gespeicherten Settings getestet (TestConnectionAsync faellt auf Config() zurueck).
admin.MapPost("/nextcloud/test", async (NextcloudService nc, NextcloudTestReq? req) =>
{
    var (ok, message) = await nc.TestConnectionAsync(req?.Url, req?.User, req?.Pass);
    return Results.Json(new { ok, message });
});

// B2/B3: Branding - Logo- und PWA-Icon-Upload (portiert aus Settings.razor).
// Logo: schreibt wwwroot/custom-logo.<ext>, setzt Setting BrandingLogoUrl, gibt { logoUrl } zurueck.
admin.MapPost("/branding/logo", async (HttpRequest http, IWebHostEnvironment env, SettingsService settings) =>
{
    var form = await http.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    if (file == null || file.Length == 0) return Results.BadRequest(new { error = "Keine Datei." });
    if (file.Length > 5 * 1024 * 1024) return Results.BadRequest(new { error = "Datei zu gross (max. 5 MB)." });
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (ext is not ".png" and not ".jpg" and not ".jpeg" and not ".svg")
        return Results.BadRequest(new { error = "Ungueltiger Dateityp (erlaubt: png, jpg, jpeg, svg)." });
    try
    {
        var fileName = $"custom-logo{ext}";
        var filePath = Path.Combine(env.WebRootPath, fileName);
        await using (var fs = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(fs);
        var logoUrl = $"/{fileName}";
        await settings.UpdateSettingAsync(SettingKeys.BrandingLogoUrl, logoUrl);
        return Results.Json(new { logoUrl });
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Logo-Upload fehlgeschlagen (evtl. read-only Container)");
        return Results.Problem("Logo konnte nicht gespeichert werden (Dateisystem schreibgeschuetzt?).");
    }
});
admin.MapPost("/branding/logo/remove", async (IWebHostEnvironment env, SettingsService settings) =>
{
    try
    {
        var current = settings.GetSetting(SettingKeys.BrandingLogoUrl);
        if (!string.IsNullOrWhiteSpace(current) && current.StartsWith("/custom-logo"))
        {
            var filePath = Path.Combine(env.WebRootPath, current.TrimStart('/'));
            if (File.Exists(filePath)) File.Delete(filePath);
        }
        await settings.UpdateSettingAsync(SettingKeys.BrandingLogoUrl, "");
        return Results.Ok();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Logo-Entfernen fehlgeschlagen");
        return Results.Problem("Logo konnte nicht entfernt werden.");
    }
});
// Icon: schreibt icon-192.png + icon-512.png (1:1 wie Original: dieselben Bytes) und regeneriert
// manifest.json. Der App-Name im manifest wird aus dem gespeicherten Setting uebernommen.
admin.MapPost("/branding/icon", async (HttpRequest http, IWebHostEnvironment env, SettingsService settings) =>
{
    var form = await http.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    if (file == null || file.Length == 0) return Results.BadRequest(new { error = "Keine Datei." });
    if (file.Length > 5 * 1024 * 1024) return Results.BadRequest(new { error = "Datei zu gross (max. 5 MB)." });
    if (!file.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "Nur PNG erlaubt." });
    try
    {
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();
        await File.WriteAllBytesAsync(Path.Combine(env.WebRootPath, "icon-192.png"), bytes);
        await File.WriteAllBytesAsync(Path.Combine(env.WebRootPath, "icon-512.png"), bytes);

        var manifestPath = Path.Combine(env.WebRootPath, "manifest.json");
        var appName = settings.GetSetting(SettingKeys.BrandingAppName);
        if (string.IsNullOrWhiteSpace(appName)) appName = "Feuerwehr Listen";
        var manifest = "{\n" +
            "  \"name\": \"" + appName + "\",\n" +
            "  \"short_name\": \"FW Listen\",\n" +
            "  \"description\": \"Feuerwehr Anwesenheits- und Einsatzlisten\",\n" +
            "  \"start_url\": \"/\",\n" +
            "  \"id\": \"/\",\n" +
            "  \"display\": \"standalone\",\n" +
            "  \"background_color\": \"#F5F6F8\",\n" +
            "  \"theme_color\": \"#E4002B\",\n" +
            "  \"orientation\": \"portrait\",\n" +
            "  \"icons\": [\n" +
            "    { \"src\": \"icon-192.png\", \"sizes\": \"192x192\", \"type\": \"image/png\", \"purpose\": \"any\" },\n" +
            "    { \"src\": \"icon-512.png\", \"sizes\": \"512x512\", \"type\": \"image/png\", \"purpose\": \"any\" }\n" +
            "  ]\n}";
        await File.WriteAllTextAsync(manifestPath, manifest);
        return Results.Json(new { ok = true });
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Icon-Upload fehlgeschlagen (evtl. read-only Container)");
        return Results.Problem("Icon konnte nicht gespeichert werden (Dateisystem schreibgeschuetzt?).");
    }
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
app.MapPost("/client-api/attendance/{id:int}/add", async (int id, HttpContext http, AttendanceListRepository repo, AttendanceEntryRepository entryRepo, MemberRepository memberRepo, UnitAssignmentService unitSvc, AttendanceAddRequest req) =>
{
    var list = await repo.GetByIdAsync(id);
    if (list == null) return Results.NotFound();
    // Geschlossene Listen: Nachtragen nur fuer angemeldete Benutzer
    if (list.Status != ListStatus.Open && !(http.User.Identity?.IsAuthenticated ?? false)) return Results.Forbid();

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
app.MapGet("/client-api/operation/{id:int}", async (int id, OperationListRepository repo, OperationEntryRepository entryRepo, OperationEntryFunctionRepository efRepo, VehicleRepository vehicleRepo, OperationFunctionRepository funcRepo, PersonalRequirementsService reqSvc) =>
{
    var list = await repo.GetByIdAsync(id);
    if (list == null) return Results.NotFound();
    var entries = await entryRepo.GetByListIdAsync(id);
    var funcMap = await efRepo.GetFunctionsForEntriesAsync(entries.Select(e => e.Id).ToList());
    // Nur einsatz-taugliche Fahrzeuge anbieten - kalender-only-Fahrzeuge bleiben aussen vor.
    var vehicles = await vehicleRepo.GetForOperationsAsync();
    var funcs = await funcRepo.GetAllAsync();

    // B10: Personal-Requirements-Status (nur wenn ein Stichwort mit definierten Requirements gesetzt
    // ist). ValidateRequirementsAsync liefert bei fehlendem Stichwort/keinen Requirements eine leere
    // MissingRequirements-Liste mit IsValid=true -> dann geben wir requirements=null zurueck.
    object? requirements = null;
    if (list.KeywordId.HasValue)
    {
        var validation = await reqSvc.ValidateRequirementsAsync(list.Id, list.KeywordId.Value);
        // Nur ausgeben, wenn tatsaechlich Requirements definiert sind (sonst sind sowohl IsValid als
        // auch die Missing-Liste leer/true und es gibt nichts anzuzeigen).
        if (validation.MissingRequirements.Count > 0 || !validation.IsValid)
        {
            requirements = new
            {
                isValid = validation.IsValid,
                missing = validation.MissingRequirements.Select(m => new
                {
                    functionName = m.FunctionName,
                    currentCount = m.CurrentCount,
                    requiredCount = m.RequiredCount,
                    isRequired = m.IsRequired
                })
            };
        }
    }

    return Results.Json(new
    {
        id = list.Id,
        number = NextcloudService.StripLeadingYear(list.OperationNumber, list.AlertTime.Year),
        keyword = list.Keyword,
        address = list.Address,
        alertTime = list.AlertTime,
        createdAt = list.CreatedAt,
        isOpen = list.Status == ListStatus.Open,
        entries = entries.Select(e => new
        {
            id = e.Id,
            name = e.NameOrId,
            enteredAt = e.EnteredAt,
            vehicle = e.Vehicle,
            breathing = e.WithBreathingApparatus,
            functions = funcMap.TryGetValue(e.Id, out var fl) ? fl.Select(f => f.Name).ToArray() : Array.Empty<string>()
        }),
        vehicles = vehicles.Select(v => new { id = v.Id, name = v.Name }),
        functions = funcs.Select(f => new { id = f.Id, name = f.Name }),
        requirements
    });
});

// Einsatzbericht: laden (angemeldet)
app.MapGet("/client-api/operation/{id:int}/report", async (int id, OperationListRepository opRepo, OperationReportRepository repo, OperationEntryRepository entryRepo, OperationEntryFunctionRepository efRepo, VehicleRepository vRepo, NextcloudService nc) =>
{
    var list = await opRepo.GetByIdAsync(id);
    if (list == null) return Results.NotFound();
    var report = await repo.GetOrCreateAsync(id);
    if (string.IsNullOrWhiteSpace(report.Strasse) && !string.IsNullOrWhiteSpace(list.Address)) report.Strasse = list.Address;
    var entries = await entryRepo.GetByListIdAsync(id);
    var funcMap = await efRepo.GetFunctionsForEntriesAsync(entries.Select(e => e.Id).ToList());
    var extForces = await repo.GetExternalForcesAsync(report.Id);
    var mittel = await repo.GetMittelAsync(report.Id);
    var vehicles = (await vRepo.GetForOperationsAsync()).Where(v => !StrengthCalc.IsNoVehicle(v.Name)).ToList();
    var strengths = await repo.GetVehicleStrengthsAsync(report.Id);
    var gesamt = StrengthCalc.CombinedTotal(entries, funcMap, strengths.Select(v => (v.VehicleName, v.Staerke)), extForces.Select(e => e.Staerke));
    return Results.Json(new
    {
        report,
        // B7: Einsatz-Kopfdaten fuer die Berichtsanzeige (Einsatznummer, Stichwort, Alarmzeit).
        operationNumber = NextcloudService.StripLeadingYear(list.OperationNumber, list.AlertTime.Year),
        keyword = list.Keyword,
        alertTime = list.AlertTime,
        externalForces = extForces.Select(f => new { id = f.Id, rufname = f.Rufname, staerke = f.Staerke }),
        mittel = mittel.Select(m => new { name = m.Name, anzahl = m.Anzahl, dauer = m.Dauer, isCustom = m.IsCustom }),
        vehicleStrengths = vehicles.Select(v => new { vehicleName = v.Name, staerke = strengths.FirstOrDefault(s => s.VehicleName == v.Name)?.Staerke }),
        entries = entries.Where(e => !StrengthCalc.IsNoVehicle(e.Vehicle)).Select(e => new { name = e.NameOrId, vehicle = e.Vehicle, functions = funcMap.TryGetValue(e.Id, out var fl) ? fl.Select(f => f.Name).ToArray() : new[] { e.Function.ToString() }, breathing = e.WithBreathingApparatus }),
        gesamtStaerke = gesamt,
        nextcloud = new { configured = nc.IsConfigured, folder = nc.IsConfigured ? nc.BuildOperationFolder(list.AlertTime.Year, list.OperationNumber) : "" }
    });
}).DisableAntiforgery(); // Einsatzbericht bewusst ohne Login (Kiosk-Wunsch)

// Einsatzbericht: speichern (Report + externe Kräfte + Mittel + Fahrzeug-Stärken)
app.MapPost("/client-api/operation/{id:int}/report", async (int id, OperationReportRepository repo, ReportSaveRequest req) =>
{
    var existing = await repo.GetOrCreateAsync(id);
    var r = req.Report;
    r.Id = existing.Id; r.OperationListId = id; r.CreatedAt = existing.CreatedAt; r.UpdatedAt = DateTime.Now;
    await repo.UpdateAsync(r);

    var oldForces = await repo.GetExternalForcesAsync(existing.Id);
    foreach (var f in oldForces) await repo.DeleteExternalForceAsync(f.Id);
    foreach (var f in req.ExternalForces ?? new())
        await repo.InsertExternalForceAsync(new OperationReportExternalForce { OperationReportId = existing.Id, Rufname = f.Rufname, Staerke = f.Staerke });

    await repo.ReplaceMittelAsync(existing.Id, (req.Mittel ?? new()).Select(m => new OperationReportMittel { Name = m.Name, Anzahl = m.Anzahl, Dauer = m.Dauer, IsCustom = m.IsCustom }));
    await repo.ReplaceVehicleStrengthsAsync(existing.Id, (req.VehicleStrengths ?? new()).Select(v => new OperationReportVehicleStrength { VehicleName = v.VehicleName, Staerke = v.Staerke }));
    return Results.Ok();
}).DisableAntiforgery(); // Einsatzbericht bewusst ohne Login (Kiosk-Wunsch)

// B8: Gesamtstaerke live neu berechnen. Nimmt die aktuellen (noch ungespeicherten) externen
// Kraefte + Fahrzeug-Staerken aus dem Body; die gescannten Einsatz-Eintraege + funcMap holt der
// Endpoint selbst aus der DB (analog report-GET). Rueckgabe { gesamt } im "F/M/Gesamt"-Format.
app.MapPost("/client-api/operation/{id:int}/report/strength", async (int id, OperationEntryRepository entryRepo, OperationEntryFunctionRepository efRepo, ReportStrengthReq req) =>
{
    var entries = await entryRepo.GetByListIdAsync(id);
    var funcMap = await efRepo.GetFunctionsForEntriesAsync(entries.Select(e => e.Id).ToList());
    var gesamt = StrengthCalc.CombinedTotal(
        entries,
        funcMap,
        (req.VehicleStrengths ?? new()).Select(v => (v.VehicleName, v.Staerke)),
        (req.ExternalForces ?? new()).Select(e => e.Staerke));
    return Results.Json(new { gesamt });
}).DisableAntiforgery(); // Einsatzbericht bewusst ohne Login (Kiosk-Wunsch)

// Einsatzbericht: Bildanzahl (Nextcloud)
app.MapGet("/client-api/operation/{id:int}/report/image-count", async (int id, OperationListRepository opRepo, NextcloudService nc) =>
{
    var list = await opRepo.GetByIdAsync(id);
    if (list == null || !nc.IsConfigured) return Results.Json(new { count = 0 });
    var folder = nc.BuildOperationFolder(list.AlertTime.Year, list.OperationNumber);
    var count = await nc.CountFilesAsync(folder);
    return Results.Json(new { count = count >= 0 ? count : 0 });
}).DisableAntiforgery(); // Einsatzbericht bewusst ohne Login (Kiosk-Wunsch)

// Einsatzbericht: Bilder hochladen (Nextcloud)
app.MapPost("/client-api/operation/{id:int}/report/images", async (int id, HttpRequest http, OperationListRepository opRepo, NextcloudService nc) =>
{
    var list = await opRepo.GetByIdAsync(id);
    if (list == null || !nc.IsConfigured) return Results.BadRequest();
    var folder = nc.BuildOperationFolder(list.AlertTime.Year, list.OperationNumber);
    var existing = await nc.GetExistingFileNamesAsync(folder);
    var form = await http.ReadFormAsync();
    int ok = 0, skipped = 0; var errors = new List<string>();
    foreach (var f in form.Files)
    {
        var name = string.Join("_", f.FileName.Split(Path.GetInvalidFileNameChars()));
        if (existing.Contains(name)) { skipped++; continue; }
        try { using var ms = new MemoryStream(); await f.CopyToAsync(ms); await nc.UploadAsync(folder, name, ms.ToArray(), f.ContentType); ok++; }
        catch (Exception ex) { errors.Add($"{f.FileName}: {ex.Message}"); }
    }
    var count = await nc.CountFilesAsync(folder);
    return Results.Json(new { ok, skipped, errors, count = count >= 0 ? count : ok });
}).DisableAntiforgery(); // Einsatzbericht bewusst ohne Login (Kiosk-Wunsch)

// Einsatz-Eintrag loeschen (Bearbeiten-Seite, angemeldet)
app.MapDelete("/client-api/operation/{id:int}/entry/{entryId:int}", async (int entryId, OperationEntryRepository repo) =>
{ await repo.DeleteAsync(entryId); return Results.Ok(); }).RequireAuthorization().DisableAntiforgery();

// Mitglied fuer Einsatz-Eintrag aufloesen (danach waehlt der Client Fahrzeug + Funktionen)
// Bewusst anonym fuer Kiosk-LAN; bei oeffentlicher Erreichbarkeit absichern (DSGVO).
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
app.MapPost("/client-api/operation/{id:int}/add", async (int id, HttpContext http, OperationListRepository repo, OperationEntryRepository entryRepo, OperationEntryFunctionRepository efRepo, MemberRepository memberRepo, VehicleRepository vehicleRepo, OperationAddRequest req) =>
{
    var list = await repo.GetByIdAsync(id);
    if (list == null) return Results.NotFound();
    // Geschlossene Listen: Nachtragen nur fuer angemeldete Benutzer (analog attendance/add).
    if (list.Status != ListStatus.Open && !(http.User.Identity?.IsAuthenticated ?? false)) return Results.Forbid();
    var member = await memberRepo.GetByIdAsync(req.MemberId);
    if (member == null) return Results.Json(new { status = "notfound" });

    var entries = await entryRepo.GetByListIdAsync(id);
    if (entries.Any(e => e.NameOrId.Contains($"({member.MemberNumber})")))
        return Results.Json(new { status = "duplicate", name = $"{member.FirstName} {member.LastName}" });

    // Serverseitig pruefen, dass das Fahrzeug ueberhaupt einsatz-tauglich ist. Ein reines
    // Ausblenden im GET reicht nicht - der Endpoint ist anonym und direkt aufrufbar.
    var vehicleName = "Ohne Fahrzeug";
    if (!req.NoVehicle && req.VehicleId is int vid)
    {
        var veh = await vehicleRepo.GetByIdAsync(vid);
        if (veh != null && veh.IsActive && veh.ShowInOperations) vehicleName = veh.Name;
    }

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

// Aktive Fahrzeuge (öffentlich, z. B. für Mangel-Meldung)
// Nur von der Maengelliste benutzt (der Name klingt allgemeiner, als der Endpoint ist).
// Bewusst OHNE ShowInOperations-Filter: auch ein kalender-only-Fahrzeug kann Maengel haben.
app.MapGet("/client-api/vehicles-active", async (VehicleRepository repo) =>
    Results.Json((await repo.GetActiveAsync()).Select(v => new { id = v.Id, name = $"{v.Name} ({v.CallSign})" })));

// Mängelliste (öffentlich, mitgliedsnummer-basiert)
app.MapGet("/client-api/defects", async (DefectRepository repo) =>
    Results.Json((await repo.GetPagedAsync(1, 2000, null)).Select(d => new
    {
        id = d.Id, description = d.Description,
        vehicle = string.IsNullOrWhiteSpace(d.CustomVehicle) ? (d.VehicleName ?? "—") : d.CustomVehicle,
        status = d.Status.ToString(), reportedByName = d.ReportedByName, reportedAt = d.ReportedAt,
        resolvedByName = d.ResolvedByName, resolvedAt = d.ResolvedAt
    })));
app.MapPost("/client-api/defects", async (DefectRepository repo, VehicleRepository vRepo, MemberRepository mRepo, EmailSenderService email, SettingsService settings, DefectReportRequest r) =>
{
    var member = await mRepo.GetByMemberNumberAsync((r.ReporterNumber ?? "").Trim());
    if (member == null) return Results.Json(new { status = "notfound" });
    int? vehicleId = null; string? vehicleName = null; string? custom = null;
    if (!string.IsNullOrWhiteSpace(r.CustomVehicle)) custom = r.CustomVehicle.Trim();
    else if (r.VehicleId is int vid && vid > 0) { var v = await vRepo.GetByIdAsync(vid); vehicleId = vid; vehicleName = v != null ? $"{v.Name} ({v.CallSign})" : "Unbekannt"; }
    var defect = new Defect { Description = r.Description.Trim(), VehicleId = vehicleId, VehicleName = vehicleName, CustomVehicle = custom, Status = DefectStatus.Open, ReportedByMemberId = member.Id, ReportedByName = $"{member.FirstName} {member.LastName} ({member.MemberNumber})", ReportedAt = DateTime.Now };
    await repo.CreateAsync(defect);
    try
    {
        var recipients = settings.GetSetting(SettingKeys.NotificationDefectRecipients);
        if (!string.IsNullOrWhiteSpace(recipients))
        {
            var disp = custom ?? vehicleName ?? "—";
            await email.SendAsync(new[] { recipients }, $"Neuer Mangel: {disp} - Feuerwehr Listen",
                $"Neuer Mangel gemeldet\n\nEingetragen am: {defect.ReportedAt:dd.MM.yyyy HH:mm}\nDurch: {defect.ReportedByName}\nFahrzeug: {disp}\n\nBeschreibung:\n{defect.Description}");
        }
    }
    catch { }
    return Results.Json(new { status = "ok" });
}).DisableAntiforgery();
app.MapGet("/client-api/defects/{id:int}/history", async (int id, DefectRepository repo) =>
    Results.Json((await repo.GetStatusChangesAsync(id)).Select(c => new { oldStatus = c.OldStatus.ToString(), newStatus = c.NewStatus.ToString(), changedByName = c.ChangedByName, changedAt = c.ChangedAt, comment = c.Comment })));
// Statusaenderung wie das MELDEN bewusst ohne Anmeldung (Kiosk/Geraetewart-Flow): Der
// Bearbeiter identifiziert sich per Mitgliedsnummer, die zu einem gueltigen Mitglied
// aufloesen MUSS -> "bearbeitet durch"-Nachweis. (Frueher RequireAuthorization; das hat den
// Geraetewart am PC ohne Login blockiert -> 401/"Speichern fehlgeschlagen".)
app.MapPost("/client-api/defects/{id:int}/status", async (int id, DefectRepository repo, MemberRepository mRepo, DefectStatusRequest r) =>
{
    var d = await repo.GetByIdAsync(id); if (d == null) return Results.NotFound();
    if (!Enum.TryParse<DefectStatus>(r.NewStatus, out var ns)) return Results.BadRequest();
    var member = await mRepo.GetByMemberNumberAsync((r.MemberNumber ?? "").Trim());
    if (member == null) return Results.Json(new { status = "notfound" });
    var disp = $"{member.FirstName} {member.LastName} ({member.MemberNumber})";
    await repo.AddStatusChangeAsync(new DefectStatusChange { DefectId = id, OldStatus = d.Status, NewStatus = ns, ChangedByName = disp, ChangedAt = DateTime.Now, Comment = string.IsNullOrWhiteSpace(r.Comment) ? null : r.Comment.Trim() });
    d.Status = ns;
    if (ns == DefectStatus.Done) { d.ResolvedAt = DateTime.Now; d.ResolvedByMemberId = member.Id; d.ResolvedByName = disp; }
    await repo.UpdateAsync(d);
    return Results.Json(new { status = "ok" });
}).DisableAntiforgery();

// B18: Mitglieder-Namenssuche fuer den Maengel-Flow (Melden UND Status). Das Melden ist bewusst
// anonym (Kiosk, keine Anmeldung), daher ist auch diese Suche anonym - analog zur bestehenden
// anonymen Mitglieds-Aufloesung (z. B. /client-api/operation/{id}/resolve). Es werden nur wenige
// Treffer (max 8) mit begrenzten Feldern (id, name, number) zurueckgegeben.
app.MapGet("/client-api/members/search", async (MemberRepository repo, string? q) =>
{
    var term = (q ?? "").Trim();
    if (term.Length < 2) return Results.Json(Array.Empty<object>());
    var found = await repo.SearchAsync(term, 8);
    return Results.Json(found.Select(m => new { id = m.Id, name = $"{m.FirstName} {m.LastName}", number = m.MemberNumber }));
}).DisableAntiforgery();

// Brandsicherheitswache: Detail (öffentlich) + Registrieren/Austragen/Abschließen
app.MapGet("/client-api/firesafetywatch/{id:int}", async (int id, FireSafetyWatchRepository repo, FireSafetyWatchRequirementRepository reqRepo, FireSafetyWatchEntryRepository entryRepo) =>
{
    var w = await repo.GetByIdAsync(id);
    if (w == null) return Results.NotFound();
    var reqs = await reqRepo.GetByWatchIdAsync(id);
    var entries = await entryRepo.GetByWatchIdAsync(id);
    return Results.Json(new
    {
        id = w.Id, name = w.Name, location = w.Location, eventTime = w.EventDateTime, isOpen = w.Status == ListStatus.Open,
        requirements = reqs.OrderBy(r => r.VehicleId == null ? 1 : 0).Select(r => new
        {
            id = r.Id, functionName = r.FunctionDef?.Name ?? "Trupp", vehicleId = r.VehicleId, vehicleName = r.VehicleId == null ? "Ohne Fahrzeug" : r.Vehicle?.Name, amount = r.Amount,
            entries = entries.Where(e => e.RequirementId == r.Id).Select(e => new { entryId = e.Id, memberName = $"{e.Member?.FirstName} {e.Member?.LastName}" })
        })
    });
});

app.MapPost("/client-api/firesafetywatch/{id:int}/register", async (int id, HttpContext http, FireSafetyWatchRepository watchRepo, FireSafetyWatchEntryRepository entryRepo, MemberRepository memberRepo, FswRegisterRequest req) =>
{
    // Geschlossene Wachen: Nachtragen nur fuer angemeldete Benutzer (analog attendance/add).
    var watch = await watchRepo.GetByIdAsync(id);
    if (watch == null) return Results.NotFound();
    if (watch.Status != ListStatus.Open && !(http.User.Identity?.IsAuthenticated ?? false)) return Results.Forbid();

    Member? member = null;
    if (req.MemberId is int mid) member = await memberRepo.GetByIdAsync(mid);
    else if (!string.IsNullOrWhiteSpace(req.Code))
    {
        var input = req.Code.Trim();
        if (int.TryParse(input, out _)) member = await memberRepo.GetByMemberNumberAsync(input);
        if (member == null)
        {
            var found = await memberRepo.SearchAsync(input);
            if (found.Count == 1) member = found[0];
            else if (found.Count > 1) return Results.Json(new { status = "choose-member", members = found.Select(m => new { id = m.Id, name = $"{m.FirstName} {m.LastName}", number = m.MemberNumber }) });
        }
    }
    if (member == null) return Results.Json(new { status = "notfound" });

    var entries = await entryRepo.GetByWatchIdAsync(id);
    if (entries.Any(e => e.MemberId == member.Id)) return Results.Json(new { status = "duplicate", name = $"{member.FirstName} {member.LastName}" });

    await entryRepo.InsertAsync(new FireSafetyWatchEntry { FireSafetyWatchId = id, RequirementId = req.RequirementId, MemberId = member.Id });
    return Results.Json(new { status = "added", name = $"{member.FirstName} {member.LastName}" });
}).DisableAntiforgery();

app.MapDelete("/client-api/firesafetywatch/{id:int}/entry/{entryId:int}", async (int entryId, FireSafetyWatchEntryRepository entryRepo) =>
{ await entryRepo.DeleteAsync(entryId); return Results.Ok(); }).RequireAuthorization("Admin").DisableAntiforgery();

app.MapPost("/client-api/firesafetywatch/{id:int}/close", async (int id, FireSafetyWatchRepository repo, ListNotificationService notif) =>
{
    var w = await repo.GetByIdAsync(id); if (w == null) return Results.NotFound();
    w.Status = ListStatus.Closed; w.ClosedAt = DateTime.Now;
    await repo.UpdateAsync(w);
    await notif.NotifyFireSafetyWatchClosedAsync(w);
    return Results.Ok();
}).RequireAuthorization().DisableAntiforgery();

admin.MapPost("/firesafetywatches", async (FireSafetyWatchRepository repo, VehicleRepository vehicleRepo, CalendarService calendar, FswCreateRequest r) =>
{
    // Name/Ort ohne Null-Check zu trimmen wuerde bei leerem Body eine 500 werfen statt 400.
    if (string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.Location)) return Results.BadRequest();

    // Ende: entweder angegeben oder Standarddauer ab Beginn.
    var end = r.EventEndTime is DateTime e && e > r.EventTime
        ? e
        : r.EventTime.AddHours(calendar.GetWatchDefaultHours());

    var watch = new FireSafetyWatch
    {
        Name = r.Name.Trim(),
        Location = r.Location.Trim(),
        EventDateTime = r.EventTime,
        EndDateTime = end,
        Status = ListStatus.Open
    };
    // Nur einsatz-taugliche Fahrzeuge zulassen; unbekannte/kalender-only werden zu "ohne Fahrzeug".
    var allowedVehicleIds = (await vehicleRepo.GetForOperationsAsync()).Select(v => v.Id).ToHashSet();
    // Keine Funktion gewaehlt (FunctionDefId = 0) ist erlaubt -> Anzeige faellt auf "Trupp" zurueck.
    var reqs = (r.Requirements ?? new()).Where(x => x.Amount > 0)
        .Select(x => new FireSafetyWatchRequirement
        {
            FunctionDefId = x.FunctionDefId,
            Amount = x.Amount,
            VehicleId = x.VehicleId is int vid && allowedVehicleIds.Contains(vid) ? vid : null
        }).ToList();
    if (reqs.Count == 0) return Results.BadRequest();

    // Gegenrichtung: ein im Kalender bereits gebuchtes Fahrzeug blockiert die Wache.
    var vehicleIds = reqs.Where(x => x.VehicleId.HasValue).Select(x => x.VehicleId!.Value).ToList();
    if (vehicleIds.Count > 0)
    {
        var conflicts = await calendar.GetWatchVehicleConflictsAsync(vehicleIds, r.EventTime, end);
        if (conflicts.Count > 0)
        {
            return Results.Json(new
            {
                status = "conflict",
                conflicts = conflicts.Select(c => new { resource = c.ResourceName, title = c.ConflictTitle, start = c.Start, end = c.End })
            }, statusCode: StatusCodes.Status409Conflict);
        }
    }

    await repo.InsertFireSafetyWatchWithRequirements(watch, reqs);
    return Results.Ok();
});

// ===================== Kalender =====================
// Lesen ist bewusst anonym (wie die uebrigen Listen, Kiosk-Betrieb). Buchen ebenfalls,
// aber mit Pflicht-Identifikation (Name oder Mitgliedsnummer) - analog "Mangel melden".
// Aendern/Stornieren/Loeschen verlangt Login, genau wie beim Loeschen von Listeneintraegen.

app.MapGet("/client-api/calendar", async (DateTime? from, DateTime? to,
    CalendarRepository repo, FireSafetyWatchRepository fswRepo, ScheduledListRepository schedRepo, CalendarService svc) =>
{
    var start = (from ?? DateTime.Now.Date.AddDays(-31));
    var end = (to ?? start.AddMonths(2));
    if (end <= start) end = start.AddMonths(1);

    var events = await repo.GetEventsInRangeAsync(start, end);
    var resources = await repo.GetResourcesForEventsAsync(events.Select(e => e.Id).ToList());

    var items = new List<object>();

    foreach (var e in events)
    {
        var res = resources.Where(r => r.CalendarEventId == e.Id).ToList();
        items.Add(new
        {
            id = e.Id,
            kind = e.Type switch
            {
                CalendarEventType.Dienst => "dienst",
                CalendarEventType.Fahrzeugbuchung => "fahrzeug",
                CalendarEventType.Raumbuchung => "raum",
                _ => "veranstaltung"
            },
            title = e.Title,
            location = e.Location,
            start = e.StartTime,
            end = e.EndTime,
            allDay = e.IsAllDay,
            status = e.Status.ToString(),
            requestedBy = e.RequestedBy,
            unitNumber = e.UnitNumber,
            seriesId = e.SeriesId,
            attendanceListId = e.AttendanceListId,
            pendingApprovals = res.Count(r => r.Status == CalendarResourceStatus.Angefragt),
            href = e.AttendanceListId is int alid ? $"/attendance/{alid}" : null
        });
    }

    // Brandsicherheitswachen werden projiziert, nicht dupliziert. Ohne hinterlegtes Ende
    // (Altdaten) gilt die einstellbare Standarddauer.
    var watchDefaultHours = svc.GetWatchDefaultHours();
    foreach (var w in await fswRepo.GetInRangeAsync(start, end))
    {
        items.Add(new
        {
            id = w.Id,
            kind = "wache",
            title = w.Name,
            location = w.Location,
            start = w.EventDateTime,
            end = w.EndDateTime ?? w.EventDateTime.AddHours(watchDefaultHours),
            allDay = false,
            status = w.Status == ListStatus.Open ? "Offen" : "Abgeschlossen",
            requestedBy = (string?)null,
            unitNumber = (int?)null,
            seriesId = (int?)null,
            attendanceListId = (int?)null,
            pendingApprovals = 0,
            href = $"/firesafetywatches/{w.Id}"
        });
    }

    // Geplante Listen als Termin mit anzeigen (noch nicht geoeffnete Anwesenheits-/Einsatzlisten).
    foreach (var s in (await schedRepo.GetPendingAsync()).Where(s => s.ScheduledEventTime >= start && s.ScheduledEventTime < end))
    {
        items.Add(new
        {
            id = s.Id,
            kind = "geplant",
            title = s.Title,
            location = (string?)null,
            start = s.ScheduledEventTime,
            end = s.ScheduledEventTime.AddHours(2),
            allDay = false,
            status = "Geplant",
            requestedBy = (string?)null,
            unitNumber = s.UnitNumber,
            seriesId = (int?)null,
            attendanceListId = (int?)null,
            pendingApprovals = 0,
            href = (string?)null
        });
    }

    // Serverzeit mitliefern: die Uhr alter iPads ist unzuverlaessig, "heute" muss vom Server kommen.
    return Results.Json(new { serverTime = DateTime.Now, from = start, to = end, items });
});

// Einzelner Termin mit allen Angaben - fuer das Detail-Fenster im Kalender.
app.MapGet("/client-api/calendar/events/{id:int}", async (int id, CalendarRepository repo,
    VehicleRepository vRepo, RoomRepository rRepo, SettingsService settings) =>
{
    var e = await repo.GetEventAsync(id);
    if (e == null) return Results.NotFound();
    var res = await repo.GetResourcesForEventAsync(id);

    var resources = new List<object>();
    foreach (var r in res)
    {
        var name = r.ResourceKind == CalendarResourceKind.Vehicle
            ? (await vRepo.GetByIdAsync(r.ResourceId))?.Name ?? $"Fahrzeug #{r.ResourceId}"
            : (await rRepo.GetByIdAsync(r.ResourceId))?.Name ?? $"Raum #{r.ResourceId}";
        resources.Add(new
        {
            kind = r.ResourceKind == CalendarResourceKind.Vehicle ? "Fahrzeug" : "Raum",
            name,
            status = r.Status.ToString(),
            approvedBy = r.ApprovedBy,
            approvedAt = r.ApprovedAt,
            comment = r.DecisionComment
        });
    }

    return Results.Json(new
    {
        id = e.Id,
        type = e.Type.ToString(),
        title = e.Title,
        description = e.Description,
        location = e.Location,
        start = e.StartTime,
        end = e.EndTime,
        allDay = e.IsAllDay,
        status = e.Status.ToString(),
        requestedBy = e.RequestedBy,
        requestedByEmail = e.RequestedByEmail,
        unitLabel = e.UnitNumber is int un ? settings.GetUnitLabel(un) : null,
        seriesId = e.SeriesId,
        isSeriesException = e.IsSeriesException,
        attendanceListId = e.AttendanceListId,
        createdAt = e.CreatedAt,
        resources
    });
});

// Buchbare Ressourcen fuer das Buchungsformular (anonym, damit auch ohne Login buchbar).
app.MapGet("/client-api/calendar/resources", async (VehicleRepository vRepo, RoomRepository rRepo) =>
{
    var vehicles = await vRepo.GetBookableAsync();
    var rooms = await rRepo.GetActiveAsync();
    return Results.Json(new
    {
        vehicles = vehicles.Select(v => new { id = v.Id, name = v.Name, callSign = v.CallSign, requiresApproval = v.RequiresApproval }),
        rooms = rooms.Select(r => new { id = r.Id, name = r.Name, capacity = r.Capacity, requiresApproval = r.RequiresApproval })
    });
});

// Belegungen im Zeitraum - fuer die Verfuegbarkeitsanzeige im Buchungsdialog.
app.MapGet("/client-api/calendar/availability", async (DateTime from, DateTime to, CalendarRepository repo, CalendarService svc) =>
{
    var rows = await repo.GetBookingsInRangeAsync(from, to);
    var result = new List<object>();
    foreach (var (r, e) in rows)
    {
        result.Add(new
        {
            kind = r.ResourceKind.ToString(),
            resourceId = r.ResourceId,
            start = e.StartTime,
            end = e.EndTime,
            title = e.Title,
            status = r.Status.ToString()
        });
    }
    return Results.Json(result);
});

app.MapPost("/client-api/calendar/events", async (CalendarEventRequest r, CalendarService svc, MemberRepository memberRepo) =>
{
    // Identifikation ist Pflicht - entweder ein aufloesbares Mitglied oder ein Freitext-Name.
    var who = (r.RequestedBy ?? "").Trim();
    if (string.IsNullOrWhiteSpace(who))
        return Results.Json(new { status = "invalid", message = "Bitte Name oder Mitgliedsnummer angeben." });

    int? memberId = null;
    if (int.TryParse(who, out _))
    {
        var m = await memberRepo.GetByMemberNumberAsync(who);
        if (m != null) { memberId = m.Id; who = $"{m.FirstName} {m.LastName} ({m.MemberNumber})"; }
    }

    var ev = new CalendarEvent
    {
        Type = Enum.TryParse<CalendarEventType>(r.Type, true, out var t) ? t : CalendarEventType.Veranstaltung,
        Title = (r.Title ?? "").Trim(),
        Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim(),
        Location = string.IsNullOrWhiteSpace(r.Location) ? null : r.Location.Trim(),
        StartTime = r.Start,
        EndTime = r.End,
        IsAllDay = r.AllDay,
        UnitNumber = r.UnitNumber is int u && u >= 1 && u <= 9 ? u : null,
        RequestedBy = who,
        RequestedByEmail = string.IsNullOrWhiteSpace(r.RequestedByEmail) ? null : r.RequestedByEmail.Trim(),
        MemberId = memberId,
        MinutesBeforeEvent = r.MinutesBeforeEvent is int mb && mb >= 0 ? mb : 60
    };

    var resources = new List<(CalendarResourceKind, int)>();
    foreach (var id in r.VehicleIds ?? new()) resources.Add((CalendarResourceKind.Vehicle, id));
    foreach (var id in r.RoomIds ?? new()) resources.Add((CalendarResourceKind.Room, id));

    var result = await svc.CreateEventAsync(ev, resources);
    return result.Outcome switch
    {
        CalendarBookingOutcome.Created => Results.Json(new
        {
            status = "ok",
            id = result.EventId,
            approvalRequired = result.ApprovalRequired,
            mailSent = result.MailSent,
            message = result.Message
        }),
        CalendarBookingOutcome.Conflict => Results.Json(new
        {
            status = "conflict",
            conflicts = result.Conflicts.Select(c => new { resource = c.ResourceName, title = c.ConflictTitle, start = c.Start, end = c.End })
        }),
        CalendarBookingOutcome.ResourceNotFound => Results.Json(new { status = "notfound", message = result.Message }),
        _ => Results.Json(new { status = "invalid", message = result.Message })
    };
}).DisableAntiforgery();

// Stornieren/Loeschen: angemeldet (anonym anlegen ja, fremde Termine entfernen nein).
listMgmt.MapPost("/calendar/events/{id:int}/cancel", async (int id, CalendarRepository repo) =>
{
    var e = await repo.GetEventAsync(id); if (e == null) return Results.NotFound();
    e.Status = CalendarEventStatus.Storniert;
    // Als Ausnahme markieren, damit die Serien-Materialisierung den Termin nicht neu erzeugt.
    if (e.SeriesId != null) e.IsSeriesException = true;
    await repo.UpdateEventAsync(e);
    return Results.Ok();
});

listMgmt.MapPut("/calendar/events/{id:int}", async (int id, CalendarEventRequest r, CalendarRepository repo) =>
{
    var e = await repo.GetEventAsync(id); if (e == null) return Results.NotFound();
    if (!string.IsNullOrWhiteSpace(r.Title)) e.Title = r.Title.Trim();
    e.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
    e.Location = string.IsNullOrWhiteSpace(r.Location) ? null : r.Location.Trim();
    if (r.End > r.Start) { e.StartTime = r.Start; e.EndTime = r.End; }
    e.IsAllDay = r.AllDay;
    e.UnitNumber = r.UnitNumber is int u && u >= 1 && u <= 9 ? u : null;
    // Einzeln geaendert -> Serien-Materialisierung fasst diesen Termin nicht mehr an.
    if (e.SeriesId != null) e.IsSeriesException = true;
    await repo.UpdateEventAsync(e);
    return Results.Ok();
});

admin.MapDelete("/calendar/events/{id:int}", async (int id, CalendarRepository repo) =>
{
    await repo.DeleteEventAsync(id); return Results.Ok();
});

// Dienst -> Anwesenheitsliste sofort anlegen (sonst macht es der Hintergrunddienst kurz vorher).
listMgmt.MapPost("/calendar/events/{id:int}/create-attendance", async (int id, CalendarRepository repo, AttendanceListRepository listRepo, SettingsService settings) =>
{
    var e = await repo.GetEventAsync(id); if (e == null) return Results.NotFound();
    if (e.AttendanceListId is int existing) return Results.Json(new { status = "exists", id = existing });

    var listId = await listRepo.CreateAsync(new AttendanceList
    {
        Title = e.Title,
        Unit = e.UnitNumber is int un ? settings.GetUnitLabel(un) : "Allgemein",
        // Unit und Description sind in der DB NOT NULL - null wuerde die Zeile ablehnen.
        Description = e.Description ?? string.Empty,
        UnitNumber = e.UnitNumber,
        Status = ListStatus.Open,
        CreatedAt = DateTime.Now
    });
    e.AttendanceListId = listId;
    await repo.UpdateEventAsync(e);
    return Results.Json(new { status = "ok", id = listId });
});

// --- Serien (angemeldet: organisatorische Planung) ---
listMgmt.MapGet("/calendar/series", async (CalendarRepository repo) =>
    Results.Json((await repo.GetAllSeriesAsync()).Select(s => new
    {
        id = s.Id, title = s.Title, type = s.Type.ToString(), frequency = s.Frequency.ToString(),
        weekdayMask = s.WeekdayMask, dayOfMonth = s.DayOfMonth, startMinuteOfDay = s.StartMinuteOfDay,
        durationMinutes = s.DurationMinutes, seriesStart = s.SeriesStart, seriesEnd = s.SeriesEnd,
        unitNumber = s.UnitNumber, isActive = s.IsActive
    })));

listMgmt.MapPost("/calendar/series", async (CalendarSeriesRequest r, CalendarRepository repo, CalendarService svc) =>
{
    if (string.IsNullOrWhiteSpace(r.Title)) return Results.BadRequest();
    var s = new CalendarEventSeries
    {
        Type = Enum.TryParse<CalendarEventType>(r.Type, true, out var t) ? t : CalendarEventType.Dienst,
        Title = r.Title.Trim(),
        Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim(),
        Location = string.IsNullOrWhiteSpace(r.Location) ? null : r.Location.Trim(),
        UnitNumber = r.UnitNumber is int u && u >= 1 && u <= 9 ? u : null,
        Frequency = Enum.TryParse<CalendarFrequency>(r.Frequency, true, out var f) ? f : CalendarFrequency.Woechentlich,
        WeekdayMask = r.WeekdayMask,
        DayOfMonth = r.DayOfMonth,
        StartMinuteOfDay = Math.Clamp(r.StartMinuteOfDay, 0, 24 * 60 - 1),
        DurationMinutes = r.DurationMinutes > 0 ? r.DurationMinutes : 120,
        SeriesStart = r.SeriesStart,
        SeriesEnd = r.SeriesEnd,
        MinutesBeforeEvent = r.MinutesBeforeEvent is int mb && mb >= 0 ? mb : 60,
        RequestedBy = string.IsNullOrWhiteSpace(r.RequestedBy) ? "Planung" : r.RequestedBy.Trim(),
        IsActive = true,
        CreatedAt = DateTime.Now
    };
    var id = await repo.InsertSeriesAsync(s);
    s.Id = id;
    var created = await svc.MaterializeSeriesAsync(s, DateTime.Now.Date, DateTime.Now.AddMonths(12));
    return Results.Json(new { id, created });
});

listMgmt.MapPut("/calendar/series/{id:int}", async (int id, CalendarSeriesRequest r, CalendarRepository repo, CalendarService svc) =>
{
    var s = await repo.GetSeriesAsync(id); if (s == null) return Results.NotFound();
    if (!string.IsNullOrWhiteSpace(r.Title)) s.Title = r.Title.Trim();
    s.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
    s.Location = string.IsNullOrWhiteSpace(r.Location) ? null : r.Location.Trim();
    s.UnitNumber = r.UnitNumber is int u && u >= 1 && u <= 9 ? u : null;
    if (Enum.TryParse<CalendarFrequency>(r.Frequency, true, out var f)) s.Frequency = f;
    s.WeekdayMask = r.WeekdayMask;
    s.DayOfMonth = r.DayOfMonth;
    s.StartMinuteOfDay = Math.Clamp(r.StartMinuteOfDay, 0, 24 * 60 - 1);
    if (r.DurationMinutes > 0) s.DurationMinutes = r.DurationMinutes;
    s.SeriesStart = r.SeriesStart;
    s.SeriesEnd = r.SeriesEnd;
    if (r.MinutesBeforeEvent is int mb && mb >= 0) s.MinutesBeforeEvent = mb;
    await repo.UpdateSeriesAsync(s);

    // Kuenftige Termine neu aufbauen - Ausnahmen bleiben dabei unangetastet.
    await repo.DeleteFutureSeriesEventsAsync(id, DateTime.Now.Date);
    var created = await svc.MaterializeSeriesAsync(s, DateTime.Now.Date, DateTime.Now.AddMonths(12));
    return Results.Json(new { created });
});

listMgmt.MapDelete("/calendar/series/{id:int}", async (int id, CalendarRepository repo) =>
{
    var s = await repo.GetSeriesAsync(id); if (s == null) return Results.NotFound();
    s.IsActive = false;
    s.SeriesEnd = DateTime.Now;
    await repo.UpdateSeriesAsync(s);
    // Nur kuenftige, nicht individuell geaenderte Termine entfernen; Vergangenheit bleibt.
    await repo.DeleteFutureSeriesEventsAsync(id, DateTime.Now.Date);
    return Results.Ok();
});

// --- Freigabe per Einmal-Link ---
// GET liest NUR. Ein mutierender GET wuerde von Mail-Scannern und Browser-Prefetch
// automatisch ausgeloest und damit ungewollt freigeben.
app.MapGet("/client-api/approve/{token}", async (string token, CalendarService svc) =>
{
    var (outcome, row, ev, name) = await svc.LoadApprovalAsync(token);
    if (outcome == ApprovalOutcome.UnknownToken || row == null || ev == null)
        return Results.Json(new { status = "unknown" });

    return Results.Json(new
    {
        status = outcome switch
        {
            ApprovalOutcome.Ok => "open",
            ApprovalOutcome.AlreadyDecided => "decided",
            ApprovalOutcome.Expired => "expired",
            _ => "unknown"
        },
        resource = name,
        kind = row.ResourceKind.ToString(),
        title = ev.Title,
        description = ev.Description,
        location = ev.Location,
        start = ev.StartTime,
        end = ev.EndTime,
        requestedBy = ev.RequestedBy,
        decision = row.Status.ToString(),
        decidedBy = row.ApprovedBy,
        decidedAt = row.ApprovedAt,
        comment = row.DecisionComment
    });
});

app.MapPost("/client-api/approve/{token}", async (string token, ApprovalDecisionRequest r, CalendarService svc) =>
{
    var outcome = await svc.DecideAsync(token, r.Approve, r.DecidedBy ?? "", r.Comment);
    return Results.Json(new
    {
        status = outcome switch
        {
            ApprovalOutcome.Ok => "ok",
            ApprovalOutcome.AlreadyDecided => "decided",
            ApprovalOutcome.Expired => "expired",
            _ => "unknown"
        }
    });
}).DisableAntiforgery();

// Anwesenheitsliste: abschliessen (angemeldet)
app.MapPost("/client-api/attendance/{id:int}/close", async (int id, AttendanceListRepository repo, ListNotificationService notif) =>
{
    var l = await repo.GetByIdAsync(id); if (l == null) return Results.NotFound();
    l.Status = ListStatus.Closed; l.ClosedAt = DateTime.Now;
    await repo.UpdateAsync(l);
    await notif.NotifyAttendanceClosedAsync(l);
    return Results.Ok();
}).RequireAuthorization().DisableAntiforgery();

// Anwesenheitsliste: Eintrag loeschen (angemeldet)
app.MapDelete("/client-api/attendance/{id:int}/entry/{entryId:int}", async (int entryId, AttendanceEntryRepository repo) =>
{ await repo.DeleteAsync(entryId); return Results.Ok(); }).RequireAuthorization().DisableAntiforgery();

// Anwesenheitsliste: als entschuldigt eintragen (jeder angemeldete Nutzer)
app.MapPost("/client-api/attendance/{id:int}/excuse", async (int id, AttendanceEntryRepository entryRepo, MemberRepository memberRepo, ExcuseReq req) =>
{
    var member = await memberRepo.FindByNameOrNumberAsync((req.Code ?? "").Trim());
    if (member == null) return Results.Json(new { status = "notfound" });
    var entries = await entryRepo.GetByListIdAsync(id);
    if (entries.Any(e => e.NameOrId.Contains($"({member.MemberNumber})"))) return Results.Json(new { status = "duplicate", name = $"{member.FirstName} {member.LastName}" });
    await entryRepo.CreateAsync(new AttendanceEntry { AttendanceListId = id, NameOrId = $"{member.FirstName} {member.LastName} ({member.MemberNumber})", EnteredAt = DateTime.Now, IsExcused = true });
    return Results.Json(new { status = "added", name = $"{member.FirstName} {member.LastName}" });
}).RequireAuthorization().DisableAntiforgery();

// Entschuldigt-Kandidaten fuer die Switch-Liste: alle aktiven Mitglieder (nach Nachname a-z)
// mit aktuellem Status fuer diese Liste. Angemeldet.
app.MapGet("/client-api/attendance/{id:int}/excuse-candidates", async (int id, AttendanceEntryRepository entryRepo, MemberRepository memberRepo) =>
{
    var members = (await memberRepo.GetAllAsync()).Where(m => m.IsActive);
    var entries = await entryRepo.GetByListIdAsync(id);
    return Results.Json(members
        .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
        .Select(m => new
        {
            id = m.Id,
            name = $"{m.LastName}, {m.FirstName}",
            number = m.MemberNumber,
            isExcused = entries.Any(e => e.NameOrId.Contains($"({m.MemberNumber})") && e.IsExcused),
            isPresent = entries.Any(e => e.NameOrId.Contains($"({m.MemberNumber})") && !e.IsExcused)
        }));
}).RequireAuthorization().DisableAntiforgery();

// Entschuldigt umschalten (Switch): fuegt einen Entschuldigt-Eintrag hinzu oder entfernt ihn.
// Anwesenheits-Eintraege werden dabei NICHT geloescht. Angemeldet.
app.MapPost("/client-api/attendance/{id:int}/excuse-toggle", async (int id, AttendanceEntryRepository entryRepo, MemberRepository memberRepo, ExcuseToggleReq req) =>
{
    var member = await memberRepo.GetByIdAsync(req.MemberId);
    if (member == null) return Results.NotFound();
    var entries = await entryRepo.GetByListIdAsync(id);
    var existing = entries.FirstOrDefault(e => e.NameOrId.Contains($"({member.MemberNumber})"));
    if (req.Excused)
    {
        if (existing != null) return Results.Json(new { status = existing.IsExcused ? "already" : "present", excused = existing.IsExcused });
        await entryRepo.CreateAsync(new AttendanceEntry { AttendanceListId = id, NameOrId = $"{member.FirstName} {member.LastName} ({member.MemberNumber})", EnteredAt = DateTime.Now, IsExcused = true });
        return Results.Json(new { status = "added", excused = true });
    }
    if (existing != null && existing.IsExcused) await entryRepo.DeleteAsync(existing.Id);
    return Results.Json(new { status = "removed", excused = false });
}).RequireAuthorization().DisableAntiforgery();

// Einsatzliste: abschliessen (angemeldet) - inkl. Fahrzeug-Staerken-Korrektur im Bericht
app.MapPost("/client-api/operation/{id:int}/close", async (int id, OperationListRepository repo, OperationEntryRepository entryRepo, OperationEntryFunctionRepository efRepo, OperationReportRepository reportRepo, ListNotificationService notif, OpCloseReq req) =>
{
    var l = await repo.GetByIdAsync(id); if (l == null) return Results.NotFound();
    if (!string.IsNullOrWhiteSpace(req?.Address)) l.Address = req!.Address!.Trim();
    l.Status = ListStatus.Closed; l.ClosedAt = DateTime.Now;
    await repo.UpdateAsync(l);
    var report = await reportRepo.GetByOperationListIdAsync(id);
    if (report != null)
    {
        var entries = await entryRepo.GetByListIdAsync(id);
        var funcs = await efRepo.GetFunctionsForEntriesAsync(entries.Select(e => e.Id).ToList());
        foreach (var g in entries.Where(e => !StrengthCalc.IsNoVehicle(e.Vehicle)).GroupBy(e => e.Vehicle))
            await reportRepo.UpsertVehicleStrengthAsync(report.Id, g.Key, StrengthCalc.VehicleFuehrerMannschaft(g, funcs));
    }
    await notif.NotifyOperationClosedAsync(l);
    return Results.Ok();
}).RequireAuthorization().DisableAntiforgery();

// Einsatzliste: Adresse aktualisieren (+ Geocoding) (angemeldet)
app.MapPost("/client-api/operation/{id:int}/address", async (int id, OperationListRepository repo, GeocodingService geo, OpCloseReq req) =>
{
    var l = await repo.GetByIdAsync(id); if (l == null) return Results.NotFound();
    l.Address = string.IsNullOrWhiteSpace(req.Address) ? null : req.Address.Trim();
    if (!string.IsNullOrWhiteSpace(l.Address)) { var (lat, lon) = await geo.GeocodeAsync(l.Address); l.Latitude = lat; l.Longitude = lon; }
    await repo.UpdateAsync(l);
    return Results.Ok();
}).RequireAuthorization().DisableAntiforgery();

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
// Bewusst anonym fuer Kiosk-LAN; bei oeffentlicher Erreichbarkeit absichern (DSGVO).
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
// Bewusst anonym fuer Kiosk-LAN; bei oeffentlicher Erreichbarkeit absichern (DSGVO).
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
// Bewusst anonym fuer Kiosk-LAN; bei oeffentlicher Erreichbarkeit absichern (DSGVO).
app.MapPost("/client-api/feedback", async (OperationListRepository opRepo, FeedbackService feedback, FeedbackRequest req) =>
{
    var op = await opRepo.GetByIdAsync(req.OperationId);
    if (op == null) return Results.NotFound();
    var result = await feedback.SendFeedbackAsync(op, req.Text ?? "", req.SubmitterName, req.SubmitterNumber);
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

// ShowInOperations/IsBookable mit Default true: fehlt das Feld im Body, wuerde .NET es
// still als false deserialisieren und das Fahrzeug ungewollt ausblenden.
public record VehicleReq(string Name, string CallSign, string Type, bool IsActive,
    bool ShowInOperations = true, bool IsBookable = true, bool RequiresApproval = false, string? ApproverEmails = null);
public record KeywordReq(string Name, string? Description);
public record RoomReq(string Name, string? Description, int? Capacity, bool RequiresApproval, string? ApproverEmails, bool IsActive = true);
/// <summary>Eintrag fuer /client-api/open-lists - passt zu Client/Models/ListItem.
/// Vehicles enthaelt Fahrzeuge bzw. Raeume, sofern dem Eintrag welche zugeordnet sind.</summary>
public record ListItemDto(int Id, string Title, string? Sub, DateTime Time, string Href, List<string>? Vehicles = null);
public record CalendarEventRequest(
    string? Type, string? Title, string? Description, string? Location,
    DateTime Start, DateTime End, bool AllDay, int? UnitNumber,
    string? RequestedBy, string? RequestedByEmail,
    List<int>? VehicleIds, List<int>? RoomIds, int? MinutesBeforeEvent);
public record CalendarSeriesRequest(
    string? Type, string? Title, string? Description, string? Location, int? UnitNumber,
    string? Frequency, int WeekdayMask, int? DayOfMonth, int StartMinuteOfDay, int DurationMinutes,
    DateTime SeriesStart, DateTime? SeriesEnd, int? MinutesBeforeEvent, string? RequestedBy);
public record ApprovalDecisionRequest(bool Approve, string? DecidedBy, string? Comment);
// B1: StrengthPosition als int (1=Zugfuehrer, 2=Gruppenfuehrer, 3=Mannschaft). Default 3.
public record FunctionReq(string Name, bool IsDefault, int StrengthPosition = 3);
public record ReqItem(int FunctionDefId, int MinimumCount, bool IsRequired);
public record MemberReq(string MemberNumber, string FirstName, string LastName, bool IsActive, List<int>? Units);
public record CsvReq(string? Csv);
public record ApiKeyReq(string? Description);
// B15/B17: Email + optionaler Willkommens-Mail-Versand. Der Client generiert das Klartext-Passwort
// (Password) und zeigt es einmalig an; ist SendWelcomeMail=true und Email gesetzt, versendet der
// POST die Zugangsdaten (Benutzername + Klartext-Passwort) per Mail.
public record UserReq(string Username, string? FirstName, string? LastName, string Role, string? Password, string? QrAuthCode, string? AdminPin, string? Email = null, bool SendWelcomeMail = false);
public record ScheduledReq(string Type, string? Title, string? Unit, string? Description, int? UnitNumber, string? OperationNumber, string? Keyword, DateTime EventTime, int MinutesBefore);
public record AuthLoginRequest(string? Username, string? Password);
public record AuthQrRequest(string? Code, string? Pin);
public record ChangePwRequest(string? OldPassword, string? NewPassword);
public record FeedbackRequest(int OperationId, string? Text, string? SubmitterName, string? SubmitterNumber);
public record CreateAttendanceRequest(string Title, string Unit, string? Description, int? UnitNumber);
public record CreateOperationRequest(string OperationNumber, string Keyword, DateTime AlertTime, string? Address);
public record AttendanceAddRequest(string? Code, int? MemberId, int? TargetListId);
public record OperationResolveRequest(string? Code);
public record ExcuseReq(string? Code);
public record ExcuseToggleReq(int MemberId, bool Excused);
public record OpCloseReq(string? Address);
public record OperationAddRequest(int MemberId, int? VehicleId, bool NoVehicle, bool BreathingApparatus, List<int>? FunctionIds);
public record ReportSaveRequest(OperationReport Report, List<ExtForceReq>? ExternalForces, List<MittelReq>? Mittel, List<VsReq>? VehicleStrengths);
public record ExtForceReq(string? Rufname, string? Staerke);
public record MittelReq(string? Name, int Anzahl, string? Dauer, bool IsCustom);
public record VsReq(string? VehicleName, string? Staerke);
public record FswRegisterRequest(int RequirementId, string? Code, int? MemberId);
public record FswReqItem(int FunctionDefId, int Amount, int? VehicleId);
public record FswCreateRequest(string Name, string Location, DateTime EventTime, List<FswReqItem>? Requirements, DateTime? EventEndTime = null);
public record DefectReportRequest(string Description, int? VehicleId, string? CustomVehicle, string? ReporterNumber);
public record DefectStatusRequest(string NewStatus, string? Comment, string? MemberNumber);
// B4: Nextcloud-Verbindungstest. Alle Felder optional -> null testet die gespeicherten Settings.
public record NextcloudTestReq(string? Url, string? User, string? Pass);
// B8: Live-Neuberechnung der Gesamtstaerke aus noch ungespeicherten externen Kraeften + Fahrzeug-Staerken.
public record ReportStrengthReq(List<ExtForceReq>? ExternalForces, List<VsReq>? VehicleStrengths);

public partial class Program { }