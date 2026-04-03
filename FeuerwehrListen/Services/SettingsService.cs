using System.Collections.Concurrent;
using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;

namespace FeuerwehrListen.Services;

public class SettingsService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, string> _cache = new();
    private bool _isLoaded;

    public event Action? OnSettingsChanged;

    public SettingsService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private async Task EnsureLoadedAsync()
    {
        if (_isLoaded) return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbConnection>();
        var repo = new SettingsRepository(db);
        var settings = await repo.GetAllAsync();

        foreach (var s in settings)
        {
            _cache[s.Key] = s.Value;
        }

        _isLoaded = true;
    }

    public bool IsModuleVisible(string moduleKey)
    {
        EnsureLoadedAsync().GetAwaiter().GetResult();
        return _cache.TryGetValue(moduleKey, out var value) && value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public int GetAutoCloseMinutes(string key)
    {
        EnsureLoadedAsync().GetAwaiter().GetResult();
        if (_cache.TryGetValue(key, out var value) && int.TryParse(value, out var minutes))
            return minutes;
        return 0;
    }

    public async Task UpdateSettingAsync(string key, string value)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbConnection>();
        var repo = new SettingsRepository(db);
        await repo.UpsertAsync(key, value);
        _cache[key] = value;
        OnSettingsChanged?.Invoke();
    }

    public async Task<Dictionary<string, string>> GetAllSettingsAsync()
    {
        await EnsureLoadedAsync();
        return new Dictionary<string, string>(_cache);
    }
}
