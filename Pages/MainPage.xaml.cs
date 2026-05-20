using System.Text.Json;
namespace CIDE.Pages;

internal partial class MainPage : ContentPage
{
    private readonly MainPageModel _model;
    public MainPage(MainPageModel model)
    {
        InitializeComponent();
        _model = model;
        BindingContext = _model;
        _model.FileOpened += OnFileOpenedAsync;
        _model.FileSaving += OnFileSavingAsync;
        LoadMonacoEditorAsync();
    }
    private async void LoadMonacoEditorAsync()
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync("monaco.html");
        using StreamReader reader = new(stream);
        var html = await reader.ReadToEndAsync();
        MonacoView.Source = new HtmlWebViewSource { Html = html };
    }
    private async void OnFileOpenedAsync(string content, string language)
    {
        var escapedContent = Uri.EscapeDataString(content);
        _ = await MonacoView.EvaluateJavaScriptAsync($"setEditorText('{escapedContent}', '{language}')");
    }
    private async Task OnFileSavingAsync()
    {
        var rawJsonString = await MonacoView.EvaluateJavaScriptAsync("getEditorText()");
        if (!string.IsNullOrEmpty(rawJsonString) && rawJsonString != "null")
        {
            var actualText = JsonSerializer.Deserialize(rawJsonString, CideJsonContext.Default.String);
            if (_model.ActiveTab != null)
            {
                _model.ActiveTab.Content = actualText ?? string.Empty;
            }
        }
    }
    private void OnTreeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Models.FileNode)
        {
        }
    }
}
