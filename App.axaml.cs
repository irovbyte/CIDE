using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CIDE.PageModels;
using Microsoft.Extensions.DependencyInjection;
namespace CIDE;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        GC.KeepAlive(typeof(Avalonia.Svg.Skia.Svg));
    }
    public override void OnFrameworkInitializationCompleted()
    {
        AppPaths.EnsureDirectoriesExist();
        var services = new ServiceCollection();
        _ = services.AddTransient<MainPageModel>();
        Services = services.BuildServiceProvider();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
