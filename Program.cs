using Avalonia;
using System;

namespace CIDE;

internal sealed class Program
{
    public static string[] StartupArgs { get; private set; } = [];
    [STAThread]
    public static void Main(string[] args)
    {
        StartupArgs = args;
        var oldExe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName + ".old";
        if (oldExe != null && File.Exists(oldExe))
        {
            try
            { File.Delete(oldExe); }
            catch { }
        }

        _ = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions
            {
                RenderingMode = [Win32RenderingMode.AngleEgl, Win32RenderingMode.Software],
                CompositionMode = [Win32CompositionMode.WinUIComposition, Win32CompositionMode.LowLatencyDxgiSwapChain]
            })
            .WithInterFont()
            .LogToTrace();
}
