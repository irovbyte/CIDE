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

    private bool _isMinimapDragging;
    private Point _minimapDragStartPoint;
    private double _minimapDragStartOffset;

    private Avalonia.Threading.DispatcherTimer? _autoSaveTimer;

    public EditorView()
    {
        InitializeComponent();

        _autoSaveTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _autoSaveTimer.Tick += AutoSaveTimer_Tick;
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

            if (SettingsService.Instance.AutoSave)
            {
                _autoSaveTimer?.Stop();
                _autoSaveTimer?.Start();
            }
        }
    }

    private async void AutoSaveTimer_Tick(object? sender, EventArgs e)
    {
        _autoSaveTimer?.Stop();
        if (DataContext is MainPageModel vm && vm.ActiveTab != null && vm.ActiveTab.IsModified)
        {
            await WorkspaceService.SaveFileAsync(vm.ActiveTab.FilePath, vm.ActiveTab.Content);
            vm.ActiveTab.IsModified = false;
            vm.StatusText = "✅ Автосохранено";
        }
    }

    private void MinimapEditor_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(MinimapEditor).Properties.IsLeftButtonPressed)
        {
            _isMinimapDragging = true;
            _minimapDragStartPoint = e.GetPosition(MinimapEditor);
            _minimapDragStartOffset = CodeEditor.TextArea.TextView.ScrollOffset.Y;
            e.Handled = true;
        }
    }

    private void MinimapEditor_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isMinimapDragging)
        {
            var point = e.GetPosition(MinimapEditor);
            var delta = point.Y - _minimapDragStartPoint.Y;

            var miniSv = MinimapEditor.FindDescendantOfType<ScrollViewer>();
            var mainSv = CodeEditor.FindDescendantOfType<ScrollViewer>();

            if (miniSv != null && mainSv != null)
            {
                var totalMainHeight = CodeEditor.TextArea.TextView.DocumentHeight;
                var miniHeight = miniSv.Extent.Height;
                var scale = (miniHeight > 0) ? (totalMainHeight / miniHeight) : 1;
                var newOffset = _minimapDragStartOffset + (delta * scale);
                var maxScroll = Math.Max(0, mainSv.Extent.Height - mainSv.Viewport.Height);
                newOffset = Math.Max(0, Math.Min(newOffset, maxScroll));
                mainSv.Offset = new Vector(CodeEditor.TextArea.TextView.ScrollOffset.X, newOffset);
            }
            e.Handled = true;
        }
    }

    private void MinimapEditor_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isMinimapDragging)
        {
            _isMinimapDragging = false;
            e.Handled = true;
        }
    }
}
