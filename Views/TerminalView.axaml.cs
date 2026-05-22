using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace CIDE.Views;

public partial class TerminalView : UserControl, IDisposable
{
    private bool _disposed;
    private Process? _shellProcess;
    private readonly StringBuilder _outputBuffer = new();
    private bool _isTerminalMode;
    private string _currentProfile = "powershell";

    public TerminalView()
    {
        InitializeComponent();
        
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        OutputToggle.IsCheckedChanged += OnOutputToggleChanged;
        TerminalToggle.IsCheckedChanged += OnTerminalToggleChanged;
        NewTerminalButton.Click += OnNewTerminalClick;
        TerminalInput.KeyDown += OnTerminalInputKeyDown;
        ProfileComboBox.SelectionChanged += OnProfileSelectionChanged;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        
        // Focus input on click in terminal panel
        TerminalPanel.PointerPressed += (s, e) => {
            if (_isTerminalMode)
                TerminalInput.Focus();
        };
    }

    private void OnOutputToggleChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (OutputToggle.IsChecked == true)
        {
            ToggleMode(isTerminal: false);
        }
        else
        {
            if (!_isTerminalMode)
            {
                OutputToggle.IsChecked = true;
            }
        }
    }

    private void OnTerminalToggleChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TerminalToggle.IsChecked == true)
        {
            ToggleMode(isTerminal: true);
        }
        else
        {
            if (_isTerminalMode)
            {
                TerminalToggle.IsChecked = true;
            }
        }
    }

    private void ToggleMode(bool isTerminal)
    {
        _isTerminalMode = isTerminal;
        OutputToggle.IsChecked = !isTerminal;
        TerminalToggle.IsChecked = isTerminal;

        OutputToggle.Foreground = isTerminal
            ? new SolidColorBrush(Color.Parse("#858585"))
            : new SolidColorBrush(Color.Parse("#CCCCCC"));
        TerminalToggle.Foreground = isTerminal
            ? new SolidColorBrush(Color.Parse("#CCCCCC"))
            : new SolidColorBrush(Color.Parse("#858585"));

        OutputToggle.BorderThickness = isTerminal
            ? new Thickness(0)
            : new Thickness(0, 0, 0, 1);
        OutputToggle.BorderBrush = new SolidColorBrush(Color.Parse("#007ACC"));

        TerminalToggle.BorderThickness = isTerminal
            ? new Thickness(0, 0, 0, 1)
            : new Thickness(0);
        TerminalToggle.BorderBrush = new SolidColorBrush(Color.Parse("#007ACC"));

        OutputScrollViewer.IsVisible = !isTerminal;
        TerminalPanel.IsVisible = isTerminal;

        if (isTerminal && _shellProcess is null)
        {
            StartShell(_currentProfile);
        }
        if (isTerminal)
        {
            Dispatcher.UIThread.Post(() => TerminalInput.Focus(), DispatcherPriority.Background);
        }
    }

    private void StartShell(string profile)
    {
        StopShell();

        _currentProfile = profile.ToLowerInvariant();
        _outputBuffer.Clear();
        TerminalOutput.Text = string.Empty;

        var (fileName, args, prompt) = _currentProfile switch
        {
            "cmd" => ("cmd.exe", "/Q", ">"),
            "wsl" => ("wsl.exe", "", "$"),
            _ => ("powershell.exe", "-NoLogo -NoProfile", "PS>")
        };

        PromptLabel.Text = prompt;

        try
        {
            Encoding shellEncoding;
            try 
            {
                shellEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
            }
            catch 
            {
                shellEncoding = Encoding.GetEncoding(866);
            }

            _shellProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    StandardOutputEncoding = shellEncoding,
                    StandardErrorEncoding = shellEncoding
                },
                EnableRaisingEvents = true
            };

            _shellProcess.OutputDataReceived += OnShellOutputReceived;
            _shellProcess.ErrorDataReceived += OnShellErrorReceived;
            _shellProcess.Exited += OnShellExited;

            _shellProcess.Start();
            _shellProcess.BeginOutputReadLine();
            _shellProcess.BeginErrorReadLine();

            AppendOutput($"[{profile} started]\n");
        }
        catch (Exception ex)
        {
            AppendOutput($"[Error starting {profile}: {ex.Message}]\n");
            _shellProcess = null;
        }
    }

    private void StopShell()
    {
        if (_shellProcess is null)
            return;

        try
        {
            _shellProcess.OutputDataReceived -= OnShellOutputReceived;
            _shellProcess.ErrorDataReceived -= OnShellErrorReceived;
            _shellProcess.Exited -= OnShellExited;

            if (!_shellProcess.HasExited)
            {
                _shellProcess.Kill(entireProcessTree: true);
            }
        }
        catch { }
        finally
        {
            _shellProcess.Dispose();
            _shellProcess = null;
        }
    }

    private void OnShellOutputReceived(object? sender, DataReceivedEventArgs e)
    {
        if (e.Data is not null)
        {
            AppendOutput(e.Data + "\n");
        }
    }

    private void OnShellErrorReceived(object? sender, DataReceivedEventArgs e)
    {
        if (e.Data is not null)
        {
            AppendOutput(e.Data + "\n");
        }
    }

    private void OnShellExited(object? sender, EventArgs e) => AppendOutput("\n[Process exited]\n");

    private void AppendOutput(string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _outputBuffer.Append(text);
            if (_outputBuffer.Length > 60000)
            {
                _outputBuffer.Remove(0, _outputBuffer.Length - 50000);
            }

            TerminalOutput.Text = _outputBuffer.ToString();
            TerminalScrollViewer.ScrollToEnd();
        });
    }

    private void SendCommand(string command)
    {
        if (_shellProcess is null || _shellProcess.HasExited)
        {
            AppendOutput("[Terminal is not running. Restarting...]\n");
            StartShell(_currentProfile);
            return;
        }

        try
        {
            _shellProcess.StandardInput.WriteLine(command);
            _shellProcess.StandardInput.Flush();
        }
        catch (Exception ex)
        {
            AppendOutput($"[Error sending command: {ex.Message}]\n");
        }
    }

    private void OnTerminalInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var command = TerminalInput.Text ?? string.Empty;
            AppendOutput($"{PromptLabel.Text} {command}\n");
            SendCommand(command);
            TerminalInput.Text = string.Empty;
            e.Handled = true;
        }
    }

    private void OnNewTerminalClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        StartShell(_currentProfile);
        if (!_isTerminalMode)
        {
            ToggleMode(isTerminal: true);
        }
    }

    private void OnProfileSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ProfileComboBox.SelectedItem is string selected && !string.IsNullOrEmpty(selected))
        {
            var profile = selected.ToLowerInvariant();
            if (profile != _currentProfile && _isTerminalMode)
            {
                StartShell(profile);
            }
            else
            {
                _currentProfile = profile;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        StopShell();
        
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e) => StopShell();
}
