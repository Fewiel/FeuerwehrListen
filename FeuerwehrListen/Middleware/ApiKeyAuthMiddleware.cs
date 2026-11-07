using FeuerwehrListen.Repositories;

namespace FeuerwehrListen.Middleware;

public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;

    public ApiKeyAuthMiddleware(RequestDelegate next, ILogger<ApiKeyAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ApiKeyRepository apiKeyRepository, Services.LoginThrottle throttle, Services.NetworkAccessService access)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            // Angemeldete Verwaltungskonten duerfen die API ohne Key nutzen - Mitglieder-Konten
            // (Mitgliederbereich) ausdruecklich NICHT: die API liefert z. B. die Mitgliederliste.
            if (context.User?.Identity?.IsAuthenticated == true && !context.User.IsInRole("Member")
                && !(context.User.Identity.Name ?? "").StartsWith(Services.UserAccessService.MemberPrefix, StringComparison.Ordinal))
            {
                await _next(context);
                return;
            }

            if (context.Request.Path.StartsWithSegments("/api/export") &&
                context.Request.Query.ContainsKey("token"))
            {
                await _next(context);
                return;
            }

            var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
            var ip = access.GetClientIp(context)?.ToString();

            // Bremse gegen Durchprobieren von API-Keys: IP nach zu vielen Fehlversuchen gesperrt.
            if (throttle.LockedUntil("apikey", ip) != null)
            {
                context.Response.StatusCode = 429;
                await context.Response.WriteAsync("Zu viele Fehlversuche - bitte spaeter erneut versuchen.");
                return;
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("API Key erforderlich");
                return;
            }

            var isValidKey = await apiKeyRepository.IsValidApiKeyAsync(apiKey);
            if (!isValidKey)
            {
                await throttle.FailAsync("apikey", ip);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Ungültiger API Key");
                return;
            }
        }

        await _next(context);
    }
}
