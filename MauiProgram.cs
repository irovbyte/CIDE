using CIDE.PageModels;
using CIDE.Pages;
using CommunityToolkit.Maui;
using Windows.UI.ApplicationSettings;
namespace CIDE;
internal static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddTransient<WelcomePage>();
        builder.Services.AddTransient<WelcomePageModel>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<MainPageModel>();
        return builder.Build();
    }
}
