using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia;
using Iciclecreek.Terminal;

namespace CIDE.Views;

public partial class TerminalView : UserControl, IDisposable
{
    private bool _disposed;
    private TerminalControl? _terminal;

    public TerminalView()
    {
        InitializeComponent();
        DetachedFromVisualTree += (_, _) => Dispose();
    }

    private bool _isRunning;

    public void Initialize()
    {
        if (_isRunning)
        {
            return;
        }

        StartProcess();
    }

    public void StartProcess(string? profileName = null)
    {
        var profile = profileName ?? "PowerShell";
        var exe = profile.ToLowerInvariant() switch
        {
            "cmd" => "cmd.exe",
            "wsl" => "wsl.exe",
            _ => "powershell.exe"
        };
        string[] args = exe == "powershell.exe" ? ["-NoLogo"] : [];

        try
        {
            if (_terminal != null)
            {
                _ = TerminalContainer.Children.Remove(_terminal);
                try
                { _terminal.Kill(); }
                catch { }
                _terminal = null;
            }

            _terminal = new TerminalControl
            {
                FontFamily = FontFamily.Parse("Cascadia Code, Consolas, monospace"),
                FontSize = 14,
                Foreground = Brush.Parse("#D4D4D4"),
                Background = Brushes.Transparent,
                Margin = new Thickness(4)
            };

            TerminalContainer.Children.Add(_terminal);

            _ = _terminal.LaunchProcess(Environment.CurrentDirectory, exe, args);
            _isRunning = true;
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        try
        { _terminal?.Kill(); }
        catch { }
        GC.SuppressFinalize(this);
    }
}
