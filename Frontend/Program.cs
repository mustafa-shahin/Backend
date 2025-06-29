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
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("http://localhost:5252") 
});

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

await builder.Build().RunAsync();