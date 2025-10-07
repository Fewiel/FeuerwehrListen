namespace FeuerwehrListen.Services;

public class ThemeService
{
    private bool _isDarkMode = true;
    public event Action? OnChange;

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (_isDarkMode != value)
            {
                _isDarkMode = value;
                NotifyStateChanged();
            }
        }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}


