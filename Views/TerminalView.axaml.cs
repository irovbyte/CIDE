using System;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Iciclecreek.Terminal;
using CIDE.PageModels;
using Microsoft.Extensions.DependencyInjection;
namespace CIDE.Views;

public partial class TerminalView : UserControl, IDisposable
{
    private bool _disposed;
    private TerminalControl? _terminal;
    private bool _isRunning;
    public TerminalView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (!_isRunning)
                StartProcess();
        };
    }
    [System.Diagnostics.DebuggerNonUserCode]
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
        var workingDir = Environment.CurrentDirectory;
        if (App.Services != null)
        {
            var model = App.Services.GetService<MainPageModel>();
            if (model != null && !string.IsNullOrEmpty(model.WorkspacePath))
            {
                workingDir = model.WorkspacePath;
            }
        }
        if (CIDE.Services.WorkspaceService.CurrentProvider is CIDE.Services.SshFileSystemProvider sshProvider)
        {
            var client = sshProvider.GetSshClient();
            if (client != null && client.ConnectionInfo != null)
            {
                exe = "ssh";
                args = [
                    "-p", client.ConnectionInfo.Port.ToString(),
                    $"{client.ConnectionInfo.Username}@{client.ConnectionInfo.Host}"
                ];
            }
        }
        try
        {
            if (_terminal != null)
            {
                TerminalContainer.Children.Remove(_terminal);
                try
                { _terminal.Kill(); }
                catch { }
                _terminal = null;
            }
            _terminal = new TerminalControl
            {
                FontFamily = FontFamily.Parse("Cascadia Code, Consolas, Courier New, monospace"),
                FontSize = 14,
                Foreground = Brush.Parse("#D4D4D4"),
                Background = Brushes.Transparent,
                Margin = new Avalonia.Thickness(4)
            };
            TerminalContainer.Children.Add(_terminal);
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    _terminal.LaunchProcess(workingDir, exe, args);
                    _terminal.Focus();
                    _isRunning = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Terminal Launch Error: {ex.Message}");
                }
            }, DispatcherPriority.Loaded);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Terminal Setup Error: {ex.Message}");
        }
    }
    [System.Diagnostics.DebuggerNonUserCode]
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        try
        {
            if (_terminal != null)
            {
                _terminal.Kill();
            }
        }
        catch { }
        GC.SuppressFinalize(this);
    }
}
