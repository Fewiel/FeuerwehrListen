using FeuerwehrListen.Components;
using FeuerwehrListen.Data;
using FeuerwehrListen.Services;
using FeuerwehrListen.Repositories;
using FeuerwehrListen.Middleware;
using LinqToDB;
using LinqToDB.AspNet;
using LinqToDB.AspNet.Logging;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.Components.Authorization;
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
    });

builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthenticationService>());
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore();

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

    return Results.Json(new
    {
        serverTime = DateTime.Now,
        operations,
        attendance,
        watches
    });
});
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program { }