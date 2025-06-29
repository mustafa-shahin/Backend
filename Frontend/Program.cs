using Blazored.LocalStorage;
using Frontend;
using Frontend.Interface;
using Frontend.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient for API communication
// 
var apiBaseUrl = "https://localhost:7206";
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl)
});

// Add configuration for services
builder.Services.AddSingleton<IConfiguration>(provider => builder.Configuration);

// Add local storage
builder.Services.AddBlazoredLocalStorage();

// Add authentication services
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Add API services
builder.Services.AddScoped<IPageService, PageService>();
builder.Services.AddScoped<IUserService, UserService>();

// Add UI services
builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Configure logging
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();

// Initialize services
try
{
    // Ensure theme service is initialized
    var themeService = app.Services.GetRequiredService<IThemeService>();
    await themeService.InitializeAsync();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Error initializing services");
}

await app.RunAsync();