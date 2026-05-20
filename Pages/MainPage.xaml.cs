using System.Text.Json;
using CIDE.Models;
using CIDE.Services;
using CIDE.PageModels;
namespace CIDE.Pages;

internal sealed partial class MainPage : ContentPage
{
    private readonly MainPageModel _model;
    public MainPage(MainPageModel model)
    {
        InitializeComponent();
        _model = model;
        BindingContext = _model;
        _model.FileOpened += OnFileOpenedAsync;
        _model.FileSaving += OnFileSavingAsync;
        _model.PropertyChanged += OnModelPropertyChanged;
        MonacoView.Navigating += OnMonacoViewNavigating;
        LoadMonacoEditorAsync();
    }
    private void OnMonacoViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url is { } url)
        {
            if (url.StartsWith("cide://modified", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
                if (_model.ActiveTab is { } tab)
                {
                    tab.IsModified = true;
                }
            }
            else if (url.StartsWith("cide://save", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
                if (_model.SaveActiveFileCommand.CanExecute(null))
                {
                    _model.SaveActiveFileCommand.Execute(null);
                }
            }
        }
    }
    private async void LoadMonacoEditorAsync()
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync("monaco.html");
        using StreamReader reader = new(stream);
        var html = await reader.ReadToEndAsync();
        MonacoView.Source = new HtmlWebViewSource { Html = html };
    }
    private void OnTreeSelectionChangedAsync(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection is { Count: > 0 } selection && selection[0] is FileNode node)
        {
            _model.SelectNodeCommand.Execute(node);
            ((CollectionView)sender!).SelectedItem = null;
        }
    }
    private async void OnFileOpenedAsync(string content, string language)
    {
        try
        {
            content ??= "";
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            var base64Content = Convert.ToBase64String(bytes);
            var jsScript = $@"
            try {{
                var binaryStr = window.atob('{base64Content}');
                var bytes = new Uint8Array(binaryStr.length);
                for (var i = 0; i < binaryStr.length; i++) {{
                    bytes[i] = binaryStr.charCodeAt(i);
                }}
                var decodedStr = new TextDecoder('utf-8').decode(bytes);
                // Вызываем твой метод из monaco.html!
                if (typeof setEditorText === 'function') {{
                    setEditorText(decodedStr, '{language}');
                }} else if (window.editor) {{
                    window.editor.setValue(decodedStr);
                    monaco.editor.setModelLanguage(window.editor.getModel(), '{language}');
                }}
            }} catch(e) {{ console.error('JS Error:', e); }}
        ";
            _ = await MonacoView.EvaluateJavaScriptAsync(jsScript);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка вставки: {ex.Message}");
        }
    }
    private async Task OnFileSavingAsync()
    {
        try
        {
            var jsScript = @"
            (function() {
                try {
                    var text = typeof getEditorText === 'function' ? getEditorText() : (window.editor ? window.editor.getValue() : '');
                    var bytes = new TextEncoder().encode(text);
                    var binaryStr = '';
                    for (var i = 0; i < bytes.byteLength; i++) {
                        binaryStr += String.fromCharCode(bytes[i]);
                    }
                    return window.btoa(binaryStr);
                } catch(e) { return ''; }
            })();
        ";
            var rawBase64 = await MonacoView.EvaluateJavaScriptAsync(jsScript);
            if (!string.IsNullOrEmpty(rawBase64) && rawBase64 != "null" && _model.ActiveTab is { } tab)
            {
                var cleanBase64 = rawBase64.Trim('"');
                if (!string.IsNullOrEmpty(cleanBase64))
                {
                    var bytes = Convert.FromBase64String(cleanBase64);
                    tab.Content = System.Text.Encoding.UTF8.GetString(bytes);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка сохранения кода: {ex.Message}");
        }
    }
    private void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainPageModel.CompilerOutput))
        {
            UpdateColoredOutput(_model.CompilerOutput);
        }
    }
    private void OnSaveHotkeyPressed(object? sender, EventArgs e)
    {
        if (_model.SaveActiveFileCommand.CanExecute(null))
        {
            _model.SaveActiveFileCommand.Execute(null);
        }
    }
    private void UpdateColoredOutput(string newOutput)
    {
        if (string.IsNullOrEmpty(newOutput))
        {
            OutputLabel.FormattedText = null;
            return;
        }
        var formatted = new FormattedString();
        var lines = newOutput.Split('\n');
        foreach (var line in lines)
        {
            var color = line switch
            {
                _ when line.StartsWith("$", StringComparison.OrdinalIgnoreCase) ||
                       line.StartsWith("[", StringComparison.OrdinalIgnoreCase) => Color.FromArgb("#569CD6"),
                _ when line.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                       line.Contains("Ошибка", StringComparison.OrdinalIgnoreCase) => Color.FromArgb("#F44336"),
                _ when line.Contains("warning", StringComparison.OrdinalIgnoreCase) ||
                       line.Contains("Предупреждение", StringComparison.OrdinalIgnoreCase) => Color.FromArgb("#FFC107"),
                _ when line.Contains("success", StringComparison.OrdinalIgnoreCase) ||
                       line.Contains("Успешно", StringComparison.OrdinalIgnoreCase) => Color.FromArgb("#4CAF50"),
                _ => Color.FromArgb("#D4D4D4")
            };
            formatted.Spans.Add(new Span { Text = line + "\n", TextColor = color });
        }
        OutputLabel.FormattedText = formatted;
    }
}
