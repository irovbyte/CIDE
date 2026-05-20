using System.Runtime.Versioning;
using CIDE.PageModels;
using CIDE.Pages;
using CommunityToolkit.Maui;
using Windows.UI.ApplicationSettings;
using Microsoft.Extensions.Logging;
namespace CIDE;

[SupportedOSPlatform("windows10.0.22000.0")]
internal static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        _ = builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                _ = fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                _ = fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });
        _ = builder.Services.AddMauiBlazorWebView();
        _ = builder.Services.AddTransient<WelcomePage>();
        _ = builder.Services.AddTransient<WelcomePageModel>();
        _ = builder.Services.AddTransient<SettingsPage>();
        _ = builder.Services.AddTransient<MainPage>();
        _ = builder.Services.AddTransient<MainPageModel>();
        return builder.Build();
    }
}
