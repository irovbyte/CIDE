using System.Runtime.Versioning;
using Microsoft.UI.Xaml;
namespace CIDE.WinUI;

[SupportedOSPlatform("windows10.0.22000.0")]
public sealed partial class App : MauiWinUIApplication
{
    public App() => InitializeComponent();
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
