using System.Collections.Concurrent;
using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;

namespace FeuerwehrListen.Services;

public class SettingsService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, string> _cache = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private volatile bool _isLoaded;

    public event Action? OnSettingsChanged;

    public SettingsService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Must be called once at startup (from Program.cs) to pre-warm the cache.
    /// This avoids sync-over-async deadlocks in Blazor Server components.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _loadLock.WaitAsync();
        try
        {
            if (_isLoaded) return;

            using var scope = _serviceProvider.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<SettingsRepository>();
            var settings = await repo.GetAllAsync();

            foreach (var s in settings)
            {
                _cache[s.Key] = s.Value;
            }

            _isLoaded = true;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public bool IsModuleVisible(string moduleKey)
    {
        // Cache is pre-warmed at startup, so no async needed
        return _cache.TryGetValue(moduleKey, out var value) && value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public string? GetSetting(string key)
    {
        return _cache.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>Alias/Name einer Einheit (1-9) oder null, wenn keiner gesetzt ist.</summary>
    public string? GetUnitAlias(int unitNumber)
    {
        var alias = GetSetting(SettingKeys.GetUnitAliasKey(unitNumber));
        return string.IsNullOrWhiteSpace(alias) ? null : alias.Trim();
    }

    /// <summary>
    /// Anzeige-Label einer Einheit: mit Alias nur der Alias (z. B. „Jugendfeuerwehr"),
    /// ohne Alias „Einheit N". Mit includeNumber=true wird bei Alias zusätzlich die Nummer
    /// vorangestellt („Einheit 3 – Jugendfeuerwehr").
    /// </summary>
    public string GetUnitLabel(int unitNumber, bool includeNumber = false)
    {
        var alias = GetUnitAlias(unitNumber);
        if (string.IsNullOrWhiteSpace(alias))
            return $"Einheit {unitNumber}";
        return includeNumber ? $"Einheit {unitNumber} – {alias}" : alias;
    }

    public int GetAutoCloseMinutes(string key)
    {
        // Cache is pre-warmed at startup, so no async needed
        if (_cache.TryGetValue(key, out var value) && int.TryParse(value, out var minutes))
            return Math.Max(0, minutes); // Never return negative values
        return 0;
    }

    public async Task UpdateSettingAsync(string key, string value)
    {
        using var scope = _serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<SettingsRepository>();
        await repo.UpsertAsync(key, value);
        _cache[key] = value;
        OnSettingsChanged?.Invoke();
    }

    public async Task<Dictionary<string, string>> GetAllSettingsAsync()
    {
        if (!_isLoaded)
        {
            await InitializeAsync();
        }
        return new Dictionary<string, string>(_cache);
    }
}
