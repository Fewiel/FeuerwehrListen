using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// HttpClient zeigt auf den eigenen Host (dieselbe Origin) - alle Daten laufen ueber
// die Fast-Endpoints unter /client-api. Keine Dauerverbindung, kein SignalR.
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

await builder.Build().RunAsync();
