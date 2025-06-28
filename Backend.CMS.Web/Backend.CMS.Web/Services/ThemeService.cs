using Blazored.LocalStorage;

namespace Backend.CMS.Blazor.Services;

public interface IThemeService
{
    event Action<string>? ThemeChanged;
    Task<string> GetThemeAsync();
    Task SetThemeAsync(string theme);
    Task ToggleThemeAsync();
    bool IsDarkMode(string theme);
}

public class ThemeService : IThemeService
{
    private readonly ILocalStorageService _localStorage;
    private readonly ILogger<ThemeService> _logger;
    private const string THEME_KEY = "theme";
    private const string DARK_THEME = "dark";
    private const string LIGHT_THEME = "light";

    public event Action<string>? ThemeChanged;

    public ThemeService(ILocalStorageService localStorage, ILogger<ThemeService> logger)
    {
        _localStorage = localStorage;
        _logger = logger;
    }

    public async Task<string> GetThemeAsync()
    {
        try
        {
            var theme = await _localStorage.GetItemAsync<string>(THEME_KEY);

            if (string.IsNullOrEmpty(theme))
            {
                // Default to system preference or light mode
                theme = await DetectSystemThemeAsync();
                await SetThemeAsync(theme);
            }

            return theme;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting theme from storage");
            return LIGHT_THEME;
        }
    }

    public async Task SetThemeAsync(string theme)
    {
        try
        {
            if (theme != DARK_THEME && theme != LIGHT_THEME)
            {
                theme = LIGHT_THEME;
            }

            await _localStorage.SetItemAsync(THEME_KEY, theme);

            // Apply theme to document
            await ApplyThemeToDocumentAsync(theme);

            ThemeChanged?.Invoke(theme);

            _logger.LogDebug("Theme changed to: {Theme}", theme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting theme: {Theme}", theme);
        }
    }

    public async Task ToggleThemeAsync()
    {
        var currentTheme = await GetThemeAsync();
        var newTheme = currentTheme == DARK_THEME ? LIGHT_THEME : DARK_THEME;
        await SetThemeAsync(newTheme);
    }

    public bool IsDarkMode(string theme)
    {
        return theme == DARK_THEME;
    }

    private async Task<string> DetectSystemThemeAsync()
    {
        try
        {
            // For now, default to light mode
            // In a real implementation, you might use JavaScript interop to detect system preference
            return LIGHT_THEME;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting system theme");
            return LIGHT_THEME;
        }
    }

    private async Task ApplyThemeToDocumentAsync(string theme)
    {
        try
        {
            // This would require JavaScript interop in a real implementation
            // For now, we'll rely on the App component to handle theme application
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying theme to document");
        }
    }
}