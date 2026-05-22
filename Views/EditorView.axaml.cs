using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using CIDE.PageModels;
using CIDE.Services;
using TextMateSharp.Grammars;

namespace CIDE.Views;

public partial class EditorView : UserControl
{
    private bool _isUpdatingFromViewModel;
    private RegistryOptions? _registryOptions;
    private TextMate.Installation? _textMateInstallation;
    private TextMate.Installation? _minimapTextMateInstallation;

    public EditorView()
    {
        InitializeComponent();
        SetupSyntaxHighlighting();
        CodeEditor.AddHandler(PointerWheelChangedEvent, CodeEditorPointerWheelChanged, Avalonia.Interactivity.RoutingStrategies.Bubble);
        MinimapEditor.Document = CodeEditor.Document;
        CodeEditor.TemplateApplied += (s, e) =>
        {
            if (CodeEditor.FindDescendantOfType<ScrollViewer>() is { } sv)
            {
                sv.ScrollChanged += ScrollViewer_ScrollChanged;
            }
        };
        SettingsService.Instance.PropertyChanged += Settings_PropertyChanged;
        ApplySettings();
    }

    private void SetupSyntaxHighlighting()
    {
        _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        _textMateInstallation = CodeEditor.InstallTextMate(_registryOptions);
        _minimapTextMateInstallation = MinimapEditor.InstallTextMate(_registryOptions);
    }

    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e) => ApplySettings();

    private void ApplySettings()
    {
        var settings = SettingsService.Instance;
        CodeEditor.Options.ShowSpaces = settings.ShowWhitespaces;
        CodeEditor.Options.ShowTabs = settings.ShowWhitespaces;
        CodeEditor.Options.HighlightCurrentLine = true;
    }

    private void ScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        var mainSv = CodeEditor.FindDescendantOfType<ScrollViewer>();
        var miniSv = MinimapEditor.FindDescendantOfType<ScrollViewer>();

        if (mainSv != null && miniSv != null)
        {
            if (mainSv.Extent.Height > mainSv.Viewport.Height)
            {
                var percentage = mainSv.Offset.Y / (mainSv.Extent.Height - mainSv.Viewport.Height);
                var miniOffset = percentage * (miniSv.Extent.Height - miniSv.Viewport.Height);
                if (miniOffset > 0)
                {
                    miniSv.Offset = new Vector(miniSv.Offset.X, miniOffset);
                }
            }
        }
    }

    public void CodeEditorPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            if (e.Delta.Y > 0)
            {
                CodeEditor.FontSize = Math.Min(72, CodeEditor.FontSize + 1);
            }
            else if (e.Delta.Y < 0)
            {
                CodeEditor.FontSize = Math.Max(8, CodeEditor.FontSize - 1);
            }
            e.Handled = true;
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainPageModel vm)
        {
            vm.PropertyChanged -= ViewModel_PropertyChanged;
            vm.PropertyChanged += ViewModel_PropertyChanged;
            UpdateEditorFromTab(vm);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainPageModel.ActiveTab) && DataContext is MainPageModel vm)
        {
            UpdateEditorFromTab(vm);
        }
    }

    private void UpdateEditorFromTab(MainPageModel vm)
    {
        _isUpdatingFromViewModel = true;
        try
        {
            if (vm.ActiveTab != null)
            {
                CodeEditor.Text = vm.ActiveTab.Content ?? string.Empty;
                SetLanguage(vm.ActiveTab.FilePath);
                if (!string.IsNullOrEmpty(vm.WorkspacePath))
                {
                    var editorConfig = EditorConfigService.Parse(vm.WorkspacePath, vm.ActiveTab.FilePath);
                    CodeEditor.Options.ConvertTabsToSpaces = editorConfig.IndentStyle == "space";
                    CodeEditor.Options.IndentationSize = editorConfig.IndentSize;
                }
            }
            else
            {
                CodeEditor.Text = string.Empty;
            }
        }
        finally
        {
            _isUpdatingFromViewModel = false;
        }
    }

    private void SetLanguage(string filePath)
    {
        if (_textMateInstallation != null && _registryOptions != null && _minimapTextMateInstallation != null)
        {
            var extension = Path.GetExtension(filePath);
            var language = _registryOptions.GetLanguageByExtension(extension);
            if (language != null)
            {
                _textMateInstallation.SetGrammar(_registryOptions.GetScopeByLanguageId(language.Id));
                _minimapTextMateInstallation.SetGrammar(_registryOptions.GetScopeByLanguageId(language.Id));
            }
        }
    }

    private void CodeEditor_TextChanged(object? sender, EventArgs e)
    {
        if (!_isUpdatingFromViewModel && DataContext is MainPageModel vm && vm.ActiveTab != null)
        {
            vm.ActiveTab.Content = CodeEditor.Text;
            vm.ActiveTab.IsModified = true;
        }
    }
}
