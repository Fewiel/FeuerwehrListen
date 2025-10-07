using Microsoft.Extensions.Caching.Memory;

namespace FeuerwehrListen.Services;

public class DownloadTokenService
{
    private readonly IMemoryCache _cache;

    public DownloadTokenService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string CreateToken(string path)
    {
        var token = Guid.NewGuid().ToString("N");
        var cacheKey = GetCacheKey(token);
        _cache.Set(cacheKey, path, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
        });
        return token;
    }

    public bool ValidateAndConsume(string token, string path)
    {
        var cacheKey = GetCacheKey(token);
        if (_cache.TryGetValue<string>(cacheKey, out var storedPath) && string.Equals(storedPath, path, StringComparison.OrdinalIgnoreCase))
        {
            _cache.Remove(cacheKey);
            return true;
        }
        return false;
    }

    private static string GetCacheKey(string token) => $"download-token:{token}";
}


