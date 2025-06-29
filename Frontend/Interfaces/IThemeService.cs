namespace Frontend.Interface
{
    public interface IThemeService
    {
        bool IsDarkMode { get; }
        event Action? ThemeChanged;
        Task InitializeAsync();
        Task ToggleThemeAsync();
        Task SetThemeAsync(bool isDarkMode);
    }
}
