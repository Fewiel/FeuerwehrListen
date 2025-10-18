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
    .AddInteractiveServerComponents();

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
builder.Services.AddScoped<StatisticsService>();
builder.Services.AddScoped<PdfExportService>();
builder.Services.AddScoped<GeocodingService>();
builder.Services.AddScoped<PersonalRequirementsService>();

builder.Services.AddSingleton<SidebarService>();
builder.Services.AddHostedService<ScheduledListBackgroundService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
    runner.MigrateUp();
}

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

app.UseHttpsRedirection();
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
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();