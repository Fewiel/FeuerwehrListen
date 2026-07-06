using System.Net.Http.Json;
using FeuerwehrListen.Client.Models;

namespace FeuerwehrListen.Client.Services;

/// <summary>
/// Laedt den <see cref="AppContextDto"/> (AppName, Logo, Modul-Flags, Unit-Labels)
/// EINMAL vom Endpoint <c>client-api/app-context</c> und cached das Ergebnis fuer die
/// Lebensdauer des Scopes. Ersetzt die bisher mehrfach unabhaengigen Fetches in NavMenu
/// und diversen Pages (jeder Seitenwechsel loeste zuvor einen eigenen Request aus).
///
/// Fehlerverhalten: Kann der Kontext nicht geladen werden (Netzwerk/Serverfehler), liefert
/// <see cref="GetAsync"/> <c>null</c> zurueck (der Fehler wird NICHT gecacht - der naechste
/// Aufruf versucht es erneut). Aufrufer entscheiden selbst ueber den Fallback; NavMenu
/// behandelt <c>null</c> wie bisher (alle Module sichtbar), damit bei kurzem Ausfall keine
/// Navigationspunkte verschwinden. Bewusst KEIN hart eingebauter "alles sichtbar"-DTO.
/// </summary>
public sealed class AppContextService
{
    private readonly HttpClient _http;
    private Task<AppContextDto?>? _cached;

    public AppContextService(HttpClient http) => _http = http;

    /// <summary>
    /// Liefert den gecachten <see cref="AppContextDto"/> oder laedt ihn beim ersten Aufruf.
    /// Bei einem Ladefehler wird <c>null</c> zurueckgegeben und NICHT gecacht.
    /// </summary>
    public Task<AppContextDto?> GetAsync()
    {
        // Erfolgreiches Task cachen; ein fehlgeschlagenes wieder verwerfen, damit ein
        // spaeterer Aufruf einen neuen Versuch startet.
        return _cached ??= LoadAsync();
    }

    private async Task<AppContextDto?> LoadAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<AppContextDto>("client-api/app-context");
        }
        catch
        {
            _cached = null; // Fehler nicht festhalten -> naechster GetAsync() versucht es erneut.
            return null;
        }
    }

    /// <summary>
    /// Verwirft den Cache, sodass der naechste <see cref="GetAsync"/> neu laedt
    /// (z.B. nach Aenderung von App-Einstellungen/Modulen in der Verwaltung).
    /// </summary>
    public void Invalidate() => _cached = null;
}
