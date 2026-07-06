using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FeuerwehrListen.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// HttpClient zeigt auf den eigenen Host (dieselbe Origin) - alle Daten laufen ueber
// die Fast-Endpoints unter /client-api. Keine Dauerverbindung, kein SignalR.
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// App-Kontext (AppName/Logo/Module) einmal laden und cachen statt pro Seite/NavMenu neu.
builder.Services.AddScoped<AppContextService>();

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<CookieAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CookieAuthStateProvider>());
builder.Services.AddCascadingAuthenticationState();

await builder.Build().RunAsync();
