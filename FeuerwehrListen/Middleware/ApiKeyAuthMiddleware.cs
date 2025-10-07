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

    public async Task InvokeAsync(HttpContext context, ApiKeyRepository apiKeyRepository)
    {
        // Nur API-Endpoints prüfen, nicht die Web-UI
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            // Wenn Benutzer bereits authentifiziert (Cookie/Session), keinen API-Key verlangen
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                await _next(context);
                return;
            }

            // Export-Endpunkte: wenn Download-Token vorhanden, API-Key nicht verlangen.
            if (context.Request.Path.StartsWithSegments("/api/export") &&
                context.Request.Query.ContainsKey("token"))
            {
                await _next(context);
                return;
            }

            var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(apiKey))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("API Key erforderlich");
                return;
            }

            var isValidKey = await apiKeyRepository.IsValidApiKeyAsync(apiKey);
            if (!isValidKey)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Ungültiger API Key");
                return;
            }
        }

        await _next(context);
    }
}
