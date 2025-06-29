using Blazored.LocalStorage;
using Frontend.Interface;
using Microsoft.JSInterop;

namespace Frontend.Services
{
    public class ThemeService : IThemeService
    {
        private readonly ILocalStorageService _localStorage;
        private readonly IJSRuntime _jsRuntime;
        private bool _isDarkMode;

        public bool IsDarkMode => _isDarkMode;
        public event Action? ThemeChanged;

        public ThemeService(ILocalStorageService localStorage, IJSRuntime jsRuntime)
        {
            _localStorage = localStorage;
            _jsRuntime = jsRuntime;
        }

        public async Task InitializeAsync()
        {
            try
            {
                // Check if theme preference exists in localStorage
                var savedTheme = await _localStorage.GetItemAsync<string>("theme");

                if (string.IsNullOrEmpty(savedTheme))
                {
                    // Check system preference
                    var prefersDark = await _jsRuntime.InvokeAsync<bool>("window.matchMedia", "(prefers-color-scheme: dark)");
                    _isDarkMode = prefersDark;
                }
                else
                {
                    _isDarkMode = savedTheme == "dark";
                }

                await ApplyThemeAsync();
            }
            catch
            {
                // Default to light mode if initialization fails
                _isDarkMode = false;
                await ApplyThemeAsync();
            }
        }

        public async Task ToggleThemeAsync()
        {
            await SetThemeAsync(!_isDarkMode);
        }

        public async Task SetThemeAsync(bool isDarkMode)
        {
            _isDarkMode = isDarkMode;
            await ApplyThemeAsync();
            await _localStorage.SetItemAsync("theme", _isDarkMode ? "dark" : "light");
            ThemeChanged?.Invoke();
        }

        private async Task ApplyThemeAsync()
        {
            try
            {
                if (_isDarkMode)
                {
                    await _jsRuntime.InvokeVoidAsync("document.documentElement.classList.add", "dark");
                }
                else
                {
                    await _jsRuntime.InvokeVoidAsync("document.documentElement.classList.remove", "dark");
                }
            }
            catch
            {
                // Ignore JS errors during theme application
            }
        }
    }
}

