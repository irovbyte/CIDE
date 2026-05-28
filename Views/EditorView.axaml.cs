using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Search;
using AvaloniaEdit.TextMate;
using CIDE.Helpers;
using CIDE.Models;
using CIDE.PageModels;
using CIDE.Services;
using TextMateSharp.Grammars;
namespace CIDE.Views;

public partial class EditorView : UserControl
{
    private bool _isUpdatingFromViewModel;
    private RegistryOptions? _registryOptions;
    private TextMate.Installation? _leftTextMateInstallation;
    private TextMate.Installation? _rightTextMateInstallation;
    private TextMate.Installation? _leftMinimapTextMateInstallation;
    private TextMate.Installation? _rightMinimapTextMateInstallation;
    private readonly Avalonia.Threading.DispatcherTimer? _autoSaveTimer;
    private readonly Avalonia.Threading.DispatcherTimer? _stickyScrollTimer;
    private bool _isMinimapDragging;
    private readonly List<TextSegment> _searchResults = [];
    private int _searchIndex = -1;
    private bool _replaceMode;
    private readonly SearchHighlightTransformer _leftSearchHighlightTransformer = new();
    private readonly SearchHighlightTransformer _rightSearchHighlightTransformer = new();
    private readonly PasteHighlightTransformer _leftPasteTransformer = new();
    private readonly PasteHighlightTransformer _rightPasteTransformer = new();
    private readonly Avalonia.Threading.DispatcherTimer? _renderTimer;
    private bool _isScrolling;
    private readonly Avalonia.Threading.DispatcherTimer? _blinkResetTimer;
    private readonly Border[] _leftCaretGhosts = new Border[6];
    private int _leftGhostIndex;
    private double _leftLastCaretX = -1, _leftLastCaretY = -1;
    private readonly Border[] _rightCaretGhosts = new Border[6];
    private int _rightGhostIndex;
    private double _rightLastCaretX = -1, _rightLastCaretY = -1;
    private readonly EditorGroup _leftGroup;
    private readonly EditorGroup _rightGroup;
    private sealed class EditorGroup
    {
        public TextEditor CodeEditor { get; init; } = null!;
        public TextEditor MinimapEditor { get; init; } = null!;
        public Border MinimapViewport { get; init; } = null!;
        public Border StickyScrollBorder { get; init; } = null!;
        public TextBlock StickyScrollText { get; init; } = null!;
        public Border SmoothCaret { get; init; } = null!;
        public Canvas CaretLayer { get; init; } = null!;
        public Border SearchPanel { get; init; } = null!;
        public TextBox SearchBox { get; init; } = null!;
        public ToggleButton MatchCaseBtn { get; init; } = null!;
        public ToggleButton WholeWordBtn { get; init; } = null!;
        public ToggleButton RegexBtn { get; init; } = null!;
        public Border ReplacePanel { get; init; } = null!;
        public TextBox SearchBox2 { get; init; } = null!;
        public ToggleButton MatchCase2Btn { get; init; } = null!;
        public ToggleButton WholeWord2Btn { get; init; } = null!;
        public ToggleButton Regex2Btn { get; init; } = null!;
        public TextBox ReplaceBox { get; init; } = null!;
        public SearchHighlightTransformer SearchHighlightTransformer { get; init; } = null!;
        public PasteHighlightTransformer PasteTransformer { get; init; } = null!;
        public double TargetScrollY { get; set; }
        public bool IsSmoothScrolling { get; set; }
    }
    public EditorView()
    {
        InitializeComponent();
        _leftGroup = new EditorGroup
        {
            CodeEditor = LeftCodeEditor,
            MinimapEditor = LeftMinimapEditor,
            MinimapViewport = LeftMinimapViewport,
            StickyScrollBorder = LeftStickyScrollBorder,
            StickyScrollText = LeftStickyScrollText,
            SmoothCaret = LeftSmoothCaret,
            CaretLayer = LeftCaretLayer,
            SearchPanel = LeftSearchPanel,
            SearchBox = LeftSearchBox,
            MatchCaseBtn = LeftMatchCaseBtn,
            WholeWordBtn = LeftWholeWordBtn,
            RegexBtn = LeftRegexBtn,
            ReplacePanel = LeftReplacePanel,
            SearchBox2 = LeftSearchBox2,
            MatchCase2Btn = LeftMatchCase2Btn,
            WholeWord2Btn = LeftWholeWord2Btn,
            Regex2Btn = LeftRegex2Btn,
            ReplaceBox = LeftReplaceBox,
            SearchHighlightTransformer = _leftSearchHighlightTransformer,
            PasteTransformer = _leftPasteTransformer
        };
        _rightGroup = new EditorGroup
        {
            CodeEditor = RightCodeEditor,
            MinimapEditor = RightMinimapEditor,
            MinimapViewport = RightMinimapViewport,
            StickyScrollBorder = RightStickyScrollBorder,
            StickyScrollText = RightStickyScrollText,
            SmoothCaret = RightSmoothCaret,
            CaretLayer = RightCaretLayer,
            SearchPanel = RightSearchPanel,
            SearchBox = RightSearchBox,
            MatchCaseBtn = RightMatchCaseBtn,
            WholeWordBtn = RightWholeWordBtn,
            RegexBtn = RightRegexBtn,
            ReplacePanel = RightReplacePanel,
            SearchBox2 = RightSearchBox2,
            MatchCase2Btn = RightMatchCase2Btn,
            WholeWord2Btn = RightWholeWord2Btn,
            Regex2Btn = RightRegex2Btn,
            ReplaceBox = RightReplaceBox,
            SearchHighlightTransformer = _rightSearchHighlightTransformer,
            PasteTransformer = _rightPasteTransformer
        };
        _autoSaveTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _autoSaveTimer.Tick += AutoSaveTimerTickAsync;
        _stickyScrollTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _stickyScrollTimer.Tick += StickyScrollTimer_Tick;
        _blinkResetTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _blinkResetTimer.Tick += (s, e) =>
        {
            LeftSmoothCaret.Classes.Add("blink");
            RightSmoothCaret.Classes.Add("blink");
        };
        InitializeCaretGhosts(LeftCaretLayer, LeftSmoothCaret, _leftCaretGhosts);
        InitializeCaretGhosts(RightCaretLayer, RightSmoothCaret, _rightCaretGhosts);
        _renderTimer = new Avalonia.Threading.DispatcherTimer(
            TimeSpan.FromMilliseconds(1000.0 / 120.0),
            Avalonia.Threading.DispatcherPriority.Render,
            RenderTimer_Tick);
        SetupSyntaxHighlighting();
        SetupEditorOptions(LeftCodeEditor);
        SetupEditorOptions(RightCodeEditor);
        SetupBracketHandling(LeftCodeEditor);
        SetupBracketHandling(RightCodeEditor);
        LeftCodeEditor.AddHandler(PointerWheelChangedEvent, CodeEditorPointerWheelChanged, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        LeftCodeEditor.AddHandler(KeyDownEvent, CodeEditorKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        LeftCodeEditor.TextArea.Caret.CaretBrush = Brushes.Transparent;
        LeftCodeEditor.TextArea.Caret.PositionChanged += (s, e) => Caret_PositionChanged(LeftCodeEditor);
        LeftCodeEditor.TextArea.TextView.ScrollOffsetChanged += (s, e) => TextView_ScrollOffsetChanged(LeftCodeEditor);
        LeftCodeEditor.TextArea.TextView.VisualLinesChanged += (s, e) => UpdateSmoothCaret(LeftCodeEditor);
        LeftCodeEditor.TextArea.TextView.LineTransformers.Add(_leftSearchHighlightTransformer);
        LeftCodeEditor.TextArea.TextView.LineTransformers.Add(_leftPasteTransformer);
        LeftMinimapEditor.Document = LeftCodeEditor.Document;
        LeftCodeEditor.TemplateApplied += (s, e) =>
        {
            if (LeftCodeEditor.FindDescendantOfType<ScrollViewer>() is { } sv)
            {
                sv.ScrollChanged += ScrollViewer_ScrollChanged;
                _stickyScrollTimer.Start();
            }
        };
        RightCodeEditor.AddHandler(PointerWheelChangedEvent, CodeEditorPointerWheelChanged, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        RightCodeEditor.AddHandler(KeyDownEvent, CodeEditorKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        RightCodeEditor.TextArea.Caret.CaretBrush = Brushes.Transparent;
        RightCodeEditor.TextArea.Caret.PositionChanged += (s, e) => Caret_PositionChanged(RightCodeEditor);
        RightCodeEditor.TextArea.TextView.ScrollOffsetChanged += (s, e) => TextView_ScrollOffsetChanged(RightCodeEditor);
        RightCodeEditor.TextArea.TextView.VisualLinesChanged += (s, e) => UpdateSmoothCaret(RightCodeEditor);
        RightCodeEditor.TextArea.TextView.LineTransformers.Add(_rightSearchHighlightTransformer);
        RightCodeEditor.TextArea.TextView.LineTransformers.Add(_rightPasteTransformer);
        RightMinimapEditor.Document = RightCodeEditor.Document;
        RightCodeEditor.TemplateApplied += (s, e) =>
        {
            if (RightCodeEditor.FindDescendantOfType<ScrollViewer>() is { } sv)
            {
                sv.ScrollChanged += ScrollViewer_ScrollChanged;
                _stickyScrollTimer.Start();
            }
        };
        LeftCodeEditor.TextArea.GotFocus += (s, e) =>
        {
            if (DataContext is MainPageModel vm)
            {
                vm.ActiveTargetGroup = 0;
                if (vm.ActiveLeftTab != null)
                {
                    vm.ActiveTab = vm.ActiveLeftTab;
                }
            }
        };
        RightCodeEditor.TextArea.GotFocus += (s, e) =>
        {
            if (DataContext is MainPageModel vm)
            {
                vm.ActiveTargetGroup = 1;
                if (vm.ActiveRightTab != null)
                {
                    vm.ActiveTab = vm.ActiveRightTab;
                }
            }
        };
        SettingsService.Instance.PropertyChanged += Settings_PropertyChanged;
        ApplySettings();
        AttachedToVisualTree += (_, _) => SetupTabDragDrop();
    }
    private void SetupTabDragDrop()
    {
        SetupTabStripDnd(this.FindControl<ItemsControl>("LeftTabItemsControl"));
        SetupTabStripDnd(this.FindControl<ItemsControl>("RightTabItemsControl"));
    }

    [Obsolete]
    private static void SetupTabStripDnd(ItemsControl? tabControl)
    {
        if (tabControl == null)
        {
            return;
        }

        tabControl.ContainerPrepared += (_, e) =>
        {
            if (e.Container is not ContentPresenter cp)
            {
                return;
            }

            cp.AttachedToVisualTree += (_, _) =>
            {
                var tabBtn = cp.FindDescendantOfType<Button>();
                if (tabBtn == null)
                {
                    return;
                }

                tabBtn.AddHandler(PointerPressedEvent, (s, pe) =>
                {
                    if (pe.GetCurrentPoint(tabBtn).Properties.IsLeftButtonPressed &&
                        tabBtn.DataContext is EditorTab dragTab)
                    {
                        var data = new DataObject();
                        data.Set("EditorTabDragFormat", dragTab);
                        _ = DragDrop.DoDragDrop(pe, data, DragDropEffects.Move);
                    }
                }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            };
        };
    }
    private static void InitializeCaretGhosts(Canvas caretLayer, Border smoothCaret, Border[] ghosts)
    {
        for (var i = 0; i < ghosts.Length; i++)
        {
            var ghost = new Border
            {
                Width = 2,
                Background = smoothCaret.Background,
                Opacity = 0,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                IsHitTestVisible = false,
                Transitions =
                [
                    new Avalonia.Animation.DoubleTransition
                    {
                        Property = OpacityProperty,
                        Duration = TimeSpan.FromMilliseconds(300),
                        Easing = new Avalonia.Animation.Easings.CubicEaseOut()
                    }
                ]
            };
            caretLayer.Children.Add(ghost);
            ghosts[i] = ghost;
        }
    }
    private void SetupSyntaxHighlighting()
    {
        _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        _leftTextMateInstallation = LeftCodeEditor.InstallTextMate(_registryOptions);
        _rightTextMateInstallation = RightCodeEditor.InstallTextMate(_registryOptions);
        _leftMinimapTextMateInstallation = LeftMinimapEditor.InstallTextMate(_registryOptions);
        _rightMinimapTextMateInstallation = RightMinimapEditor.InstallTextMate(_registryOptions);
    }
    private static void SetupEditorOptions(TextEditor editor)
    {
        editor.Options.EnableHyperlinks = true;
        editor.Options.RequireControlModifierForHyperlinkClick = true;
        editor.Options.HighlightCurrentLine = false;
        editor.Options.ShowBoxForControlCharacters = true;
        editor.Options.EnableEmailHyperlinks = false;
        editor.Options.IndentationSize = 4;
        editor.Options.ConvertTabsToSpaces = true;
        editor.Options.EnableRectangularSelection = true;
        editor.ShowLineNumbers = true;
        editor.TextArea.IndentationStrategy = new SmartIndentationStrategy();
        editor.TextArea.SelectionBrush = new SolidColorBrush(Color.FromArgb(80, 59, 130, 246));
    }
    private void SetupBracketHandling(TextEditor editor)
    {
        editor.TextArea.TextEntering += (s, e) => TextArea_TextEntering(editor, e);
        editor.TextArea.TextEntered += (s, e) => TextArea_TextEntered(editor, e);
    }
    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e) => ApplySettings();
    private void ApplySettings()
    {
        var s = SettingsService.Instance;
        LeftCodeEditor.Options.ShowSpaces = s.ShowWhitespaces;
        LeftCodeEditor.Options.ShowTabs = s.ShowWhitespaces;
        LeftCodeEditor.Options.HighlightCurrentLine = false;
        RightCodeEditor.Options.ShowSpaces = s.ShowWhitespaces;
        RightCodeEditor.Options.ShowTabs = s.ShowWhitespaces;
        RightCodeEditor.Options.HighlightCurrentLine = false;
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataContextProperty)
        {
            if (change.OldValue is MainPageModel oldVm)
            {
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
                oldVm.FileSaving -= ViewModel_FileSaving;
            }
            if (change.NewValue is MainPageModel newVm)
            {
                newVm.PropertyChanged += ViewModel_PropertyChanged;
                newVm.FileSaving += ViewModel_FileSaving;
                UpdateEditorFromTab(LeftCodeEditor, LeftMinimapEditor, newVm.ActiveLeftTab, ref _previousLeftTab);
                UpdateEditorFromTab(RightCodeEditor, RightMinimapEditor, newVm.ActiveRightTab, ref _previousRightTab);
            }
        }
    }
    private Task ViewModel_FileSaving()
    {
        if (DataContext is MainPageModel vm)
        {
            _ = vm.ActiveLeftTab?.Content = LeftCodeEditor.Text;
            _ = vm.ActiveRightTab?.Content = RightCodeEditor.Text;
        }
        return Task.CompletedTask;
    }
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is MainPageModel vm)
        {
            if (e.PropertyName == nameof(MainPageModel.ActiveLeftTab))
            {
                UpdateEditorFromTab(LeftCodeEditor, LeftMinimapEditor, vm.ActiveLeftTab, ref _previousLeftTab);
            }
            else if (e.PropertyName == nameof(MainPageModel.ActiveRightTab))
            {
                UpdateEditorFromTab(RightCodeEditor, RightMinimapEditor, vm.ActiveRightTab, ref _previousRightTab);
            }
        }
    }
    private EditorTab? _previousLeftTab;
    private EditorTab? _previousRightTab;
    private void UpdateEditorFromTab(TextEditor editor, TextEditor minimapEditor, EditorTab? tab, ref EditorTab? previousTab)
    {
        _isUpdatingFromViewModel = true;
        try
        {
            if (previousTab != null && editor.Document != null)
            {
                previousTab.Document!.Changed -= Document_Changed;
                previousTab.SavedCaretOffset = editor.CaretOffset;
                var svOld = editor.FindDescendantOfType<ScrollViewer>();
                if (svOld != null)
                {
                    previousTab.SavedScrollOffset = svOld.Offset;
                }
                previousTab.Content = editor.Document.Text;
            }
            if (tab != null)
            {
                tab.Document ??= new TextDocument(tab.Content ?? string.Empty);
                editor.Document = tab.Document;
                editor.Document.Changed += Document_Changed;
                minimapEditor.Document = tab.Document;
                SetLanguage(editor, minimapEditor, tab.FilePath);
                if (DataContext is MainPageModel vm && !string.IsNullOrEmpty(vm.WorkspacePath))
                {
                    var cfg = EditorConfigService.Parse(vm.WorkspacePath, tab.FilePath);
                    editor.Options.ConvertTabsToSpaces = cfg.IndentStyle == "space";
                    editor.Options.IndentationSize = cfg.IndentSize;
                }
                var group = editor == LeftCodeEditor ? _leftGroup : _rightGroup;
                ClearSearch(group);
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    editor.CaretOffset = Math.Min(tab.SavedCaretOffset, editor.Document.TextLength);
                    var sv = editor.FindDescendantOfType<ScrollViewer>();
                    _ = sv?.Offset = tab.SavedScrollOffset;
                    _ = editor.Focus();
                });
            }
            else
            {
                editor.Document = new TextDocument();
                minimapEditor.Document = editor.Document;
            }
            previousTab = tab;
        }
        finally
        {
            _isUpdatingFromViewModel = false;
        }
    }
    private void SetLanguage(TextEditor editor, TextEditor minimapEditor, string filePath)
    {
        if (_registryOptions is null)
        {
            return;
        }

        var ext = Path.GetExtension(filePath);
        var lang = _registryOptions.GetLanguageByExtension(ext);
        if (lang is null)
        {
            return;
        }

        var scope = _registryOptions.GetScopeByLanguageId(lang.Id);
        var inst = editor == LeftCodeEditor ? _leftTextMateInstallation : _rightTextMateInstallation;
        var miniInst = minimapEditor == LeftMinimapEditor ? _leftMinimapTextMateInstallation : _rightMinimapTextMateInstallation;
        inst?.SetGrammar(scope);
        miniInst?.SetGrammar(scope);
    }
    private void CodeEditor_TextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingFromViewModel || DataContext is not MainPageModel vm)
        {
            return;
        }

        var editor = sender as TextEditor ?? GetActiveEditor();
        var tab = editor == LeftCodeEditor ? vm.ActiveLeftTab : vm.ActiveRightTab;
        if (tab == null)
        {
            return;
        }

        tab.IsModified = true;
        if (SettingsService.Instance.AutoSave)
        {
            _autoSaveTimer?.Stop();
            _autoSaveTimer?.Start();
        }
        var group = editor == LeftCodeEditor ? _leftGroup : _rightGroup;
        if (group.SearchPanel?.IsVisible == true || group.ReplacePanel?.IsVisible == true)
        {
            RunSearch();
        }
    }
    private async void AutoSaveTimerTickAsync(object? sender, EventArgs e)
    {
        _autoSaveTimer?.Stop();
        if (DataContext is not MainPageModel vm)
        {
            return;
        }

        foreach (var tab in vm.Tabs.ToList())
        {
            if (tab.IsModified)
            {
                var textToSave = tab.Document != null ? tab.Document.Text : tab.Content;
                var filePath = tab.FilePath;
                try
                {
                    var provider = tab.Root?.Provider ?? new LocalFileSystemProvider();
                    await WorkspaceService.SaveFileAsync(provider, filePath, textToSave);
                    tab.Content = textToSave;
                    tab.IsModified = false;
                    vm.StatusText = $"✅ Автосохранено ({DateTime.Now:HH:mm:ss})";
                }
                catch (Exception ex)
                {
                    vm.StatusText = $"❌ Ошибка автосохранения: {ex.Message}";
                }
            }
        }
    }
    private void CodeEditorKeyDown(object? sender, KeyEventArgs e)
    {
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var editor = sender as TextEditor ?? GetActiveEditor();
        if (ctrl && !shift && e.Key == Key.S)
        {
            if (DataContext is MainPageModel vm)
            {
                _ = vm.SaveActiveFileCommand.ExecuteAsync(null);
            }

            e.Handled = true;
            return;
        }
        if (ctrl && !shift && e.Key == Key.F)
        {
            ShowSearch(replaceMode: false);
            e.Handled = true;
            return;
        }
        if (ctrl && !shift && e.Key == Key.H)
        {
            ShowSearch(replaceMode: true);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape)
        {
            HideSearch();
            e.Handled = true;
            return;
        }
        if (ctrl && e.Key == Key.OemQuestion)
        {
            ToggleLineComment(editor);
            e.Handled = true;
            return;
        }
        if (ctrl && !shift && e.Key == Key.D)
        {
            SelectNextOccurrence(editor);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Tab && !ctrl && !shift)
        {
            if (editor.TextArea.Selection.IsEmpty)
            {
                if (TryInsertSnippet(editor))
                { e.Handled = true; return; }
                return;
            }
            IndentSelection(editor);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Tab && shift)
        {
            UnindentSelection(editor);
            e.Handled = true;
            return;
        }
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            if (e.Key == Key.Up)
            { MoveLineUp(editor); e.Handled = true; }
            else if (e.Key == Key.Down)
            { MoveLineDown(editor); e.Handled = true; }
        }
    }
    private static readonly Dictionary<char, char> t_bracketPairs = new()
    {
        { '(', ')' }, { '[', ']' }, { '{', '}' }, { '"', '"' }, { '\'', '\'' }
    };
    private char _leftPendingClose;
    private char _rightPendingClose;
    private void TextArea_TextEntering(TextEditor editor, TextInputEventArgs e)
    {
        if (e.Text?.Length != 1)
        {
            return;
        }

        var ch = e.Text[0];
        ref var pendingClose = ref (editor == LeftCodeEditor ? ref _leftPendingClose : ref _rightPendingClose);
        if (ch == pendingClose && pendingClose != '\0')
        {
            var offset = editor.TextArea.Caret.Offset;
            if (offset < editor.Document.TextLength && editor.Document.GetCharAt(offset) == ch)
            {
                editor.TextArea.Caret.Offset++;
                e.Handled = true;
                pendingClose = '\0';
            }
        }
    }
    private void TextArea_TextEntered(TextEditor editor, TextInputEventArgs e)
    {
        if (e.Text?.Length != 1)
        {
            return;
        }

        var ch = e.Text[0];
        if (!t_bracketPairs.TryGetValue(ch, out var close))
        {
            return;
        }

        var offset = editor.TextArea.Caret.Offset;
        if (offset < editor.Document.TextLength)
        {
            var next = editor.Document.GetCharAt(offset);
            if (!char.IsWhiteSpace(next) && next != '\n' && next != '\r')
            {
                return;
            }
        }
        editor.Document.Insert(offset, close.ToString());
        editor.TextArea.Caret.Offset = offset;
        if (editor == LeftCodeEditor)
        {
            _leftPendingClose = close;
        }
        else
        {
            _rightPendingClose = close;
        }
    }
    private void ShowSearch(bool replaceMode)
    {
        _replaceMode = replaceMode;
        var group = GetActiveGroup();
        var editor = group.CodeEditor;
        if (replaceMode)
        {
            group.SearchPanel.IsVisible = false;
            group.ReplacePanel.IsVisible = true;
            _ = group.ReplacePanel.SlideAndFadeInAsync();
            _ = group.SearchBox2.Focus();
            if (!editor.TextArea.Selection.IsEmpty)
            {
                group.SearchBox2.Text = editor.TextArea.Selection.GetText();
            }
        }
        else
        {
            group.ReplacePanel.IsVisible = false;
            group.SearchPanel.IsVisible = true;
            _ = group.SearchPanel.SlideAndFadeInAsync();
            _ = group.SearchBox.Focus();
            if (!editor.TextArea.Selection.IsEmpty)
            {
                group.SearchBox.Text = editor.TextArea.Selection.GetText();
            }
        }
    }
    private void HideSearch()
    {
        var group = GetActiveGroup();
        group.SearchPanel.IsVisible = false;
        group.ReplacePanel.IsVisible = false;
        ClearSearch(group);
        _ = group.CodeEditor.Focus();
    }
    private void ClearSearch(EditorGroup group)
    {
        _searchResults.Clear();
        _searchIndex = -1;
        group.SearchHighlightTransformer?.UpdateSearch(_searchResults, _searchIndex);
        group.CodeEditor.TextArea.TextView.Redraw();
    }
    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e) => RunSearch();
    private void OnReplaceSearchChanged(object? sender, TextChangedEventArgs e) => RunSearch();
    private void OnSearchOptionsChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => RunSearch();
    private void RunSearch()
    {
        var group = GetActiveGroup();
        var query = _replaceMode ? group.SearchBox2.Text : group.SearchBox.Text;
        if (string.IsNullOrEmpty(query))
        {
            ClearSearch(group);
            return;
        }
        var matchCase = _replaceMode ? group.MatchCase2Btn.IsChecked == true : group.MatchCaseBtn.IsChecked == true;
        var wholeWord = _replaceMode ? group.WholeWord2Btn.IsChecked == true : group.WholeWordBtn.IsChecked == true;
        var useRegex = _replaceMode ? group.Regex2Btn.IsChecked == true : group.RegexBtn.IsChecked == true;
        _searchResults.Clear();
        _searchIndex = -1;
        var editor = group.CodeEditor;
        try
        {
            var mode = useRegex ? SearchMode.RegEx : SearchMode.Normal;
            var strategy = SearchStrategyFactory.Create(query, !matchCase, wholeWord, mode);
            var results = strategy.FindAll(editor.Document, 0, editor.Document.TextLength);
            foreach (var r in results)
            {
                _searchResults.Add(new TextSegment { StartOffset = r.Offset, Length = r.Length });
            }
        }
        catch { }
        if (_searchResults.Count > 0)
        {
            var cur = editor.TextArea.Caret.Offset;
            _searchIndex = _searchResults.FindIndex(s => s.StartOffset >= cur);
            if (_searchIndex < 0)
            {
                _searchIndex = 0;
            }

            NavigateToSearchResult(group, _searchIndex);
        }
        group.SearchHighlightTransformer.UpdateSearch(_searchResults, _searchIndex);
        editor.TextArea.TextView.Redraw();
    }
    private void NavigateToSearchResult(EditorGroup group, int index)
    {
        if (index < 0 || index >= _searchResults.Count)
        {
            return;
        }

        var seg = _searchResults[index];
        var editor = group.CodeEditor;
        editor.Select(seg.StartOffset, seg.Length);
        editor.TextArea.Caret.Offset = seg.StartOffset;
        editor.ScrollTo(editor.Document.GetLineByOffset(seg.StartOffset).LineNumber, 0);
        group.SearchHighlightTransformer.UpdateSearch(_searchResults, index);
        editor.TextArea.TextView.Redraw();
    }
    private void OnSearchNext(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_searchResults.Count == 0)
        { RunSearch(); return; }
        _searchIndex = (_searchIndex + 1) % _searchResults.Count;
        NavigateToSearchResult(GetActiveGroup(), _searchIndex);
    }
    private void OnSearchPrev(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_searchResults.Count == 0)
        { RunSearch(); return; }
        _searchIndex = (_searchIndex - 1 + _searchResults.Count) % _searchResults.Count;
        NavigateToSearchResult(GetActiveGroup(), _searchIndex);
    }
    private void OnReplaceOne(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_searchIndex < 0 || _searchIndex >= _searchResults.Count)
        {
            return;
        }

        var group = GetActiveGroup();
        var seg = _searchResults[_searchIndex];
        var replacement = group.ReplaceBox.Text ?? "";
        group.CodeEditor.Document.Replace(seg.StartOffset, seg.Length, replacement);
        RunSearch();
    }
    private void OnReplaceAll(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        RunSearch();
        if (_searchResults.Count == 0)
        {
            return;
        }

        var group = GetActiveGroup();
        var replacement = group.ReplaceBox.Text ?? "";
        var editor = group.CodeEditor;
        using (editor.Document.RunUpdate())
        {
            for (var i = _searchResults.Count - 1; i >= 0; i--)
            {
                var seg = _searchResults[i];
                editor.Document.Replace(seg.StartOffset, seg.Length, replacement);
            }
        }
        ClearSearch(group);
    }
    private void ToggleLineComment(TextEditor editor)
    {
        if (DataContext is not MainPageModel vm)
        {
            return;
        }

        var tab = editor == LeftCodeEditor ? vm.ActiveLeftTab : vm.ActiveRightTab;
        if (tab == null)
        {
            return;
        }

        var ext = Path.GetExtension(tab.FilePath).ToLower(System.Globalization.CultureInfo.CurrentCulture);
        var prefix = ext switch
        {
            ".cs" or ".c" or ".cpp" or ".h" or ".hpp" or ".java" or ".js" or ".ts" => "//",
            ".py" or ".sh" or ".yaml" or ".yml" => "#",
            ".sql" => "--",
            _ => "//"
        };
        var selection = editor.TextArea.Selection;
        var doc = editor.Document;
        int startLine, endLine;
        if (selection.IsEmpty)
        {
            startLine = endLine = editor.TextArea.Caret.Line;
        }
        else
        {
            startLine = doc.GetLineByOffset(selection.SurroundingSegment.Offset).LineNumber;
            var endOffset = selection.SurroundingSegment.Offset + selection.SurroundingSegment.Length;
            endLine = doc.GetLineByOffset(endOffset).LineNumber;
        }
        var allCommented = true;
        for (var ln = startLine; ln <= endLine; ln++)
        {
            var line = doc.GetLineByNumber(ln);
            var text = doc.GetText(line.Offset, line.Length).TrimStart();
            if (!text.StartsWith(prefix))
            { allCommented = false; break; }
        }
        using (doc.RunUpdate())
        {
            for (var ln = startLine; ln <= endLine; ln++)
            {
                var line = doc.GetLineByNumber(ln);
                var text = doc.GetText(line.Offset, line.Length);
                if (allCommented)
                {
                    var trimmed = text.TrimStart();
                    var spaces = text.Length - trimmed.Length;
                    var newText = string.Concat(text.AsSpan(0, spaces), trimmed.AsSpan(prefix.Length));
                    if (newText.StartsWith(' ') && trimmed.StartsWith(prefix + " ", StringComparison.Ordinal))
                    {
                        newText = string.Concat(text.AsSpan(0, spaces), trimmed.AsSpan(prefix.Length + 1));
                    }
                    doc.Replace(line.Offset, line.Length, newText);
                }
                else
                {
                    var spaces = text.Length - text.TrimStart().Length;
                    doc.Insert(line.Offset + spaces, prefix + " ");
                }
            }
        }
    }
    private static void SelectNextOccurrence(TextEditor editor)
    {
        var sel = editor.TextArea.Selection;
        if (sel.IsEmpty)
        {
            var offset = editor.TextArea.Caret.Offset;
            var doc = editor.Document;
            var start = offset;
            var end = offset;
            while (start > 0 && start - 1 < doc.TextLength && char.IsLetterOrDigit(doc.GetCharAt(start - 1)))
            {
                start--;
            }

            while (end < doc.TextLength && char.IsLetterOrDigit(doc.GetCharAt(end)))
            {
                end++;
            }

            if (start == end)
            {
                return;
            }

            editor.Select(start, end - start);
            return;
        }
        var query = sel.GetText();
        if (string.IsNullOrEmpty(query))
        {
            return;
        }

        var text = editor.Document.Text;
        var from = editor.TextArea.Selection.SurroundingSegment.Offset + editor.TextArea.Selection.SurroundingSegment.Length;
        var next = text.IndexOf(query, from, StringComparison.Ordinal);
        if (next < 0)
        {
            next = text.IndexOf(query, 0, StringComparison.Ordinal);
        }

        if (next >= 0)
        {
            editor.Select(next, query.Length);
            editor.TextArea.Caret.Offset = next;
            editor.ScrollTo(editor.Document.GetLineByOffset(next).LineNumber, 0);
        }
    }
    private static void IndentSelection(TextEditor editor)
    {
        var doc = editor.Document;
        var sel = editor.TextArea.Selection.SurroundingSegment;
        var startLine = doc.GetLineByOffset(sel.Offset).LineNumber;
        var endLine = doc.GetLineByOffset(sel.Offset + sel.Length).LineNumber;
        var indent = editor.Options.ConvertTabsToSpaces ? new string(' ', editor.Options.IndentationSize) : "\t";
        using (doc.RunUpdate())
        {
            for (var ln = startLine; ln <= endLine; ln++)
            {
                doc.Insert(doc.GetLineByNumber(ln).Offset, indent);
            }
        }
    }
    private static void UnindentSelection(TextEditor editor)
    {
        var doc = editor.Document;
        var sel = editor.TextArea.Selection.SurroundingSegment;
        var startLine = doc.GetLineByOffset(sel.Offset).LineNumber;
        var endLine = doc.GetLineByOffset(sel.Offset + sel.Length).LineNumber;
        var size = editor.Options.IndentationSize;
        using (doc.RunUpdate())
        {
            for (var ln = startLine; ln <= endLine; ln++)
            {
                var line = doc.GetLineByNumber(ln);
                var text = doc.GetText(line.Offset, line.Length);
                if (text.StartsWith('\t'))
                {
                    doc.Remove(line.Offset, 1);
                }
                else
                {
                    var spaces = 0;
                    while (spaces < text.Length && text[spaces] == ' ' && spaces < size)
                    {
                        spaces++;
                    }

                    if (spaces > 0)
                    {
                        doc.Remove(line.Offset, spaces);
                    }
                }
            }
        }
    }
    private static void MoveLineUp(TextEditor editor)
    {
        var doc = editor.Document;
        var ln = editor.TextArea.Caret.Line;
        if (ln <= 1)
        {
            return;
        }

        var cur = doc.GetLineByNumber(ln);
        var prev = doc.GetLineByNumber(ln - 1);
        var curText = doc.GetText(cur.Offset, cur.Length);
        var prevText = doc.GetText(prev.Offset, prev.Length);
        using (doc.RunUpdate())
        {
            doc.Replace(prev.Offset, prev.Length, curText);
            doc.Replace(cur.Offset, cur.Length, prevText);
        }
        editor.TextArea.Caret.Line = ln - 1;
    }
    private static void MoveLineDown(TextEditor editor)
    {
        var doc = editor.Document;
        var ln = editor.TextArea.Caret.Line;
        if (ln >= doc.LineCount)
        {
            return;
        }

        var cur = doc.GetLineByNumber(ln);
        var next = doc.GetLineByNumber(ln + 1);
        var curText = doc.GetText(cur.Offset, cur.Length);
        var nextText = doc.GetText(next.Offset, next.Length);
        using (doc.RunUpdate())
        {
            doc.Replace(next.Offset, next.Length, curText);
            doc.Replace(cur.Offset, cur.Length, nextText);
        }
        editor.TextArea.Caret.Line = ln + 1;
    }
    private void StickyScrollTimer_Tick(object? sender, EventArgs e)
    {
        if (!IsVisible)
        {
            return;
        }

        UpdateStickyScroll(LeftCodeEditor, LeftStickyScrollBorder, LeftStickyScrollText);
        UpdateStickyScroll(RightCodeEditor, RightStickyScrollBorder, RightStickyScrollText);
    }
    private static void UpdateStickyScroll(TextEditor editor, Border stickyScrollBorder, TextBlock stickyScrollText)
    {
        var sv = editor.FindDescendantOfType<ScrollViewer>();
        if (sv == null)
        {
            return;
        }

        var firstVisibleLine = editor.TextArea.TextView.GetDocumentLineByVisualTop(sv.Offset.Y);
        if (firstVisibleLine == null)
        { stickyScrollBorder.IsVisible = false; return; }
        var scopeLine = FindScopeLine(editor.Document, firstVisibleLine.LineNumber);
        if (scopeLine == null)
        { stickyScrollBorder.IsVisible = false; return; }
        stickyScrollText.Text = editor.Document.GetText(scopeLine.Offset, scopeLine.Length).Trim();
        stickyScrollBorder.IsVisible = true;
    }
    private static DocumentLine? FindScopeLine(TextDocument doc, int fromLine)
    {
        for (var ln = fromLine - 1; ln >= 1; ln--)
        {
            var line = doc.GetLineByNumber(ln);
            var text = doc.GetText(line.Offset, line.Length).TrimStart();
            if (text.StartsWith("class ", StringComparison.Ordinal) || text.StartsWith("struct ", StringComparison.Ordinal) || text.StartsWith("namespace ", StringComparison.Ordinal) ||
                text.StartsWith("public ", StringComparison.Ordinal) || text.StartsWith("private ", StringComparison.Ordinal) || text.StartsWith("protected ", StringComparison.Ordinal) ||
                text.StartsWith("internal ", StringComparison.Ordinal) || text.StartsWith("static ", StringComparison.Ordinal) || text.StartsWith("void ", StringComparison.Ordinal) ||
                text.StartsWith("async ", StringComparison.Ordinal) || text.StartsWith("def ", StringComparison.Ordinal) || text.StartsWith("fn ", StringComparison.Ordinal))
            {
                return line;
            }
        }
        return null;
    }
    private void ScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        UpdateMinimapViewport(_leftGroup);
        UpdateMinimapViewport(_rightGroup);
    }
    private static void UpdateMinimapViewport(EditorGroup group)
    {
        var mainSv = group.CodeEditor.FindDescendantOfType<ScrollViewer>();
        var miniSv = group.MinimapEditor.FindDescendantOfType<ScrollViewer>();
        if (mainSv == null || miniSv == null)
        {
            return;
        }

        if (mainSv.Extent.Height > mainSv.Viewport.Height && mainSv.Extent.Height > 0)
        {
            var pct = mainSv.Offset.Y / (mainSv.Extent.Height - mainSv.Viewport.Height);
            var miniOffset = pct * Math.Max(0, miniSv.Extent.Height - miniSv.Viewport.Height);
            if (miniOffset >= 0)
            {
                miniSv.Offset = new Vector(miniSv.Offset.X, miniOffset);
            }

            var vpRatio = group.MinimapEditor.Bounds.Height / mainSv.Extent.Height;
            group.MinimapViewport.Height = Math.Max(10, mainSv.Viewport.Height * vpRatio);
            var pctTop = mainSv.Offset.Y / mainSv.Extent.Height;
            var vpTop = pctTop * group.MinimapEditor.Bounds.Height;
            group.MinimapViewport.Margin = new Thickness(0, vpTop, 0, 0);
        }
    }
    private void Minimap_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isMinimapDragging = true;
            if (sender is Control control)
            {
                e.Pointer.Capture(control);
            }

            var group = GetGroupFromControl(sender);
            ScrollMainEditorToMinimapY(group, e.GetPosition(sender as Control).Y);
            e.Handled = true;
        }
    }
    private void Minimap_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isMinimapDragging && sender is Control control)
        {
            var group = GetGroupFromControl(sender);
            ScrollMainEditorToMinimapY(group, e.GetPosition(control).Y);
            e.Handled = true;
        }
    }
    private void Minimap_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isMinimapDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }
    private void Minimap_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var group = GetGroupFromControl(sender);
        CodeEditorPointerWheelChanged(group.CodeEditor, e);
    }
    private void ScrollMainEditorToMinimapY(EditorGroup group, double minimapY)
    {
        var mainSv = group.CodeEditor.FindDescendantOfType<ScrollViewer>();
        if (mainSv == null || mainSv.Extent.Height == 0 || group.MinimapEditor.Bounds.Height == 0)
        {
            return;
        }

        var clickRatio = minimapY / group.MinimapEditor.Bounds.Height;
        var targetY = (clickRatio * mainSv.Extent.Height) - (mainSv.Viewport.Height / 2);
        group.TargetScrollY = Math.Clamp(targetY, 0, mainSv.Extent.Height - mainSv.Viewport.Height);
        group.IsSmoothScrolling = true;
        _renderTimer?.Start();
    }
    public void CodeEditorPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var editor = sender as TextEditor ?? GetActiveEditor();
        var group = editor == LeftCodeEditor ? _leftGroup : _rightGroup;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            editor.FontSize = Math.Clamp(editor.FontSize + (e.Delta.Y > 0 ? 1 : -1), 8, 72);
            e.Handled = true;
            return;
        }
        var sv = editor.FindDescendantOfType<ScrollViewer>();
        if (sv == null || sv.Extent.Height <= sv.Viewport.Height)
        {
            return;
        }

        if (!group.IsSmoothScrolling)
        {
            group.TargetScrollY = sv.Offset.Y;
            group.IsSmoothScrolling = true;
        }
        group.TargetScrollY -= e.Delta.Y * 60;
        group.TargetScrollY = Math.Clamp(group.TargetScrollY, 0, sv.Extent.Height - sv.Viewport.Height);
        e.Handled = true;
        _renderTimer?.Start();
    }
    private void RenderTimer_Tick(object? sender, EventArgs e)
    {
        RenderTimerForGroup(_leftGroup);
        RenderTimerForGroup(_rightGroup);
        if (!_leftGroup.IsSmoothScrolling && !_rightGroup.IsSmoothScrolling)
        {
            _renderTimer?.Stop();
        }
    }
    private static void RenderTimerForGroup(EditorGroup group)
    {
        if (!group.IsSmoothScrolling)
        {
            return;
        }

        var sv = group.CodeEditor.FindDescendantOfType<ScrollViewer>();
        if (sv == null)
        { group.IsSmoothScrolling = false; return; }
        var currentY = sv.Offset.Y;
        var newY = currentY + ((group.TargetScrollY - currentY) * 0.15);
        if (Math.Abs(group.TargetScrollY - newY) < 1.0)
        {
            sv.Offset = new Vector(sv.Offset.X, group.TargetScrollY);
            group.IsSmoothScrolling = false;
        }
        else
        {
            sv.Offset = new Vector(sv.Offset.X, newY);
        }
    }
    private void TextView_ScrollOffsetChanged(TextEditor editor)
    {
        _isScrolling = true;
        UpdateSmoothCaret(editor);
    }
    private void UpdateSmoothCaret(TextEditor editor)
    {
        if (editor.Document == null)
        {
            return;
        }

        var textView = editor.TextArea.TextView;
        if (textView == null || !textView.VisualLinesValid)
        {
            return;
        }

        var isLeft = editor == LeftCodeEditor;
        var smoothCaret = isLeft ? LeftSmoothCaret : RightSmoothCaret;
        var ghosts = isLeft ? _leftCaretGhosts : _rightCaretGhosts;
        ref var ghostIndex = ref (isLeft ? ref _leftGhostIndex : ref _rightGhostIndex);
        ref var lastCaretX = ref (isLeft ? ref _leftLastCaretX : ref _rightLastCaretX);
        ref var lastCaretY = ref (isLeft ? ref _leftLastCaretY : ref _rightLastCaretY);
        try
        {
            var pos = textView.GetVisualPosition(editor.TextArea.Caret.Position, AvaloniaEdit.Rendering.VisualYPosition.LineTop);
            var x = pos.X + editor.TextArea.LeftMargins.Sum(m => m.Bounds.Width) - textView.ScrollOffset.X;
            var y = pos.Y - textView.ScrollOffset.Y;
            smoothCaret.Height = editor.FontSize * 1.2;
            if (_isScrolling)
            {
                smoothCaret.Transitions?.Clear();
                smoothCaret.RenderTransform = new TranslateTransform(x, y);
            }
            else
            {
                if (lastCaretX >= 0 && lastCaretY >= 0 && (Math.Abs(lastCaretX - x) > 0.1 || Math.Abs(lastCaretY - y) > 0.1))
                {
                    var ghost = ghosts[ghostIndex];
                    ghost.Height = smoothCaret.Height;
                    ghost.RenderTransform = new TranslateTransform(lastCaretX, lastCaretY);
                    ghost.Opacity = 0.4;
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => ghost.Opacity = 0);
                    ghostIndex = (ghostIndex + 1) % ghosts.Length;
                }
                if (smoothCaret.Transitions == null || smoothCaret.Transitions.Count == 0)
                {
                    smoothCaret.Transitions =
                    [
                        new Avalonia.Animation.TransformOperationsTransition
                        {
                            Property = RenderTransformProperty,
                            Duration = TimeSpan.FromMilliseconds(80),
                            Easing = new Avalonia.Animation.Easings.CubicEaseOut()
                        }
                    ];
                }
                smoothCaret.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse(
                    string.Create(System.Globalization.CultureInfo.InvariantCulture, $"translate({x}px, {y}px)")
                );
            }
            lastCaretX = x;
            lastCaretY = y;
        }
        catch { }
    }
    private void Document_Changed(object? sender, DocumentChangeEventArgs e)
    {
        if (sender is not TextDocument doc)
        {
            return;
        }

        var isLeft = doc == LeftCodeEditor.Document;
        var editor = isLeft ? LeftCodeEditor : RightCodeEditor;
        var pasteTransformer = isLeft ? _leftPasteTransformer : _rightPasteTransformer;
        if (e.InsertedText?.Text.Length > 1)
        {
            pasteTransformer.StartOffset = e.Offset;
            pasteTransformer.EndOffset = e.Offset + e.InsertionLength;
            pasteTransformer.CurrentOpacity = 1.0;
            var timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            timer.Tick += (s, ev) =>
            {
                pasteTransformer.CurrentOpacity -= 0.08;
                if (pasteTransformer.CurrentOpacity <= 0)
                {
                    pasteTransformer.CurrentOpacity = 0;
                    timer.Stop();
                }
                editor.TextArea.TextView.Redraw();
            };
            timer.Start();
        }
    }
    private void Caret_PositionChanged(TextEditor editor)
    {
        UpdateSmoothCaret(editor);
        if (DataContext is MainPageModel vm && editor == GetActiveEditor())
        {
            var line = editor.TextArea.Caret.Line;
            var column = editor.TextArea.Caret.Column;
            vm.CursorPosition = $"Ln {line}, Col {column}";
        }
    }
    private static bool TryInsertSnippet(TextEditor editor)
    {
        var doc = editor.Document;
        var caretOffset = editor.TextArea.Caret.Offset;
        var line = doc.GetLineByOffset(caretOffset);
        var textBeforeCaret = doc.GetText(line.Offset, caretOffset - line.Offset);
        var words = textBeforeCaret.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return false;
        }

        var lastWord = words[^1];
        var replaceStartOffset = caretOffset - lastWord.Length;
        var snippet = lastWord switch
        {
            "cw" => "Console.WriteLine($0);",
            "for" => "for (int i = 0; i < $1; i++)\n{\n    $0\n}",
            "mainc" => "int main() \n{\n    $0\n    return 0;\n}",
            _ => ""
        };
        if (string.IsNullOrEmpty(snippet))
        {
            return false;
        }

        using (doc.RunUpdate())
        {
            doc.Remove(replaceStartOffset, lastWord.Length);
            var cursorTargetIndex = snippet.IndexOf("$0", StringComparison.Ordinal);
            var cleanSnippet = snippet.Replace("$1", "length", StringComparison.Ordinal).Replace("$0", "", StringComparison.Ordinal);
            var indent = textBeforeCaret[..^lastWord.Length];
            cleanSnippet = cleanSnippet.Replace("\n", "\n" + indent, StringComparison.Ordinal);
            doc.Insert(replaceStartOffset, cleanSnippet);
            if (cursorTargetIndex != -1)
            {
                editor.TextArea.Caret.Offset = replaceStartOffset + cursorTargetIndex;
            }
        }
        return true;
    }
    private EditorGroup GetActiveGroup()
    {
        return DataContext is MainPageModel vm
            ? !vm.IsSplitView ? _leftGroup : vm.ActiveTargetGroup == 1 ? _rightGroup : _leftGroup
            : _leftGroup;
    }
    public TextEditor GetActiveEditor() => GetActiveGroup().CodeEditor;
    private EditorGroup GetGroupFromControl(object? sender)
    {
        if (sender is Visual visual)
        {
            var current = visual;
            while (current != null)
            {
                if (current == LeftCodeEditor || current == LeftMinimapEditor || current == LeftMinimapViewport)
                {
                    return _leftGroup;
                }

                if (current == RightCodeEditor || current == RightMinimapEditor || current == RightMinimapViewport)
                {
                    return _rightGroup;
                }

                if (current is Control ctrl && !string.IsNullOrEmpty(ctrl.Name))
                {
                    if (ctrl.Name.StartsWith("Left", StringComparison.Ordinal))
                    {
                        return _leftGroup;
                    }

                    if (ctrl.Name.StartsWith("Right", StringComparison.Ordinal))
                    {
                        return _rightGroup;
                    }
                }
                current = current.GetVisualParent();
            }
        }
        return _leftGroup;
    }
}
