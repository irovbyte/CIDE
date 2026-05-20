using Microsoft.Extensions.DependencyInjection;
namespace CIDE;

internal sealed partial class App : Application
{
    public App() => InitializeComponent();
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());
#if WINDOWS
        window.Created += (s, e) =>
        {
            var nativeWindow = window.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (nativeWindow != null)
            {
                var appWindow = nativeWindow.AppWindow;
                appWindow.Title = "CIDE";
                if (Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported())
                {
                    var titleBar = appWindow.TitleBar;
                    var darkColor = Microsoft.UI.ColorHelper.FromArgb(255, 9, 7, 20);
                    var hoverColor = Microsoft.UI.ColorHelper.FromArgb(255, 30, 26, 50);
                    var pressColor = Microsoft.UI.ColorHelper.FromArgb(255, 20, 16, 35);
                    var activeTextColor = Microsoft.UI.ColorHelper.FromArgb(255, 255, 255, 255);
                    var inactiveTextColor = Microsoft.UI.ColorHelper.FromArgb(255, 128, 128, 128);
                    titleBar.BackgroundColor = darkColor;
                    titleBar.ForegroundColor = activeTextColor;
                    titleBar.ButtonBackgroundColor = darkColor;
                    titleBar.ButtonHoverBackgroundColor = hoverColor;
                    titleBar.ButtonHoverForegroundColor = activeTextColor;
                    titleBar.ButtonPressedBackgroundColor = pressColor;
                    titleBar.ButtonPressedForegroundColor = activeTextColor;
                    titleBar.ButtonInactiveBackgroundColor = darkColor;
                    titleBar.ButtonInactiveForegroundColor = inactiveTextColor;
                    titleBar.InactiveBackgroundColor = darkColor;
                    titleBar.InactiveForegroundColor = inactiveTextColor;
                }
            }
        };
#endif
        return window;
    }
}
