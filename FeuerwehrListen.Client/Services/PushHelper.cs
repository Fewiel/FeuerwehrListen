using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace FeuerwehrListen.Client.Services;

/// <summary>Web-Push im Mitgliederbereich: Berechtigung anfragen, Abo am Server hinterlegen.
/// Auf Geraeten ohne Push-Unterstuetzung (alte iPads, Server-Modus, kein HTTPS) "unsupported".</summary>
public static class PushHelper
{
    public static async Task<string> PermissionAsync(IJSRuntime js)
    {
        try { return await js.InvokeAsync<string>("fwPush.permission"); } catch { return "unsupported"; }
    }

    public static async Task<bool> ShouldAskAsync(IJSRuntime js)
    {
        try { return await js.InvokeAsync<bool>("fwPush.shouldAsk"); } catch { return false; }
    }

    public static async Task<bool> IsSubscribedAsync(IJSRuntime js)
    {
        try { return await js.InvokeAsync<bool>("fwPush.isSubscribed"); } catch { return false; }
    }

    public static async Task DismissAsync(IJSRuntime js)
    {
        try { await js.InvokeVoidAsync("fwPush.dismiss"); } catch { }
    }

    /// <summary>Fragt die Berechtigung an und meldet das Abo beim Server an. True = aktiv.</summary>
    public static async Task<bool> EnableAsync(IJSRuntime js, HttpClient http)
    {
        try
        {
            var key = await http.GetFromJsonAsync<KeyDto>("client-api/me/push/key");
            var sub = await js.InvokeAsync<SubDto?>("fwPush.subscribe", key!.PublicKey);
            if (sub is null) return false;
            var resp = await http.PostAsJsonAsync("client-api/me/push/subscribe", sub);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public static async Task DisableAsync(IJSRuntime js, HttpClient http)
    {
        try
        {
            var ep = await js.InvokeAsync<string?>("fwPush.unsubscribe");
            if (!string.IsNullOrEmpty(ep)) await http.PostAsJsonAsync("client-api/me/push/unsubscribe", new { Endpoint = ep });
        }
        catch { }
    }

    private record KeyDto(string PublicKey);
    public record SubDto(string Endpoint, string P256dh, string Auth);
}
