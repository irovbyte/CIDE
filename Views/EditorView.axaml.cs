using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Search;
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
    private readonly Avalonia.Threading.DispatcherTimer? _autoSaveTimer;
    private readonly Avalonia.Threading.DispatcherTimer? _stickyScrollTimer;
    private bool _isMinimapDragging;
    private readonly List<TextSegment> _searchResults = [];
    private int _searchIndex = -1;
    private bool _replaceMode;
    private readonly SearchHighlightTransformer _searchHighlightTransformer = new();
    private readonly PasteHighlightTransformer _pasteTransformer = new();
    private double _targetScrollY;
    private bool _isSmoothScrolling;
    private readonly Avalonia.Threading.DispatcherTimer? _renderTimer;

    public EditorView()
    {
        InitializeComponent();
        _autoSaveTimer = new Avalonia.Threading.DispatcherTimer
        { Interval = TimeSpan.FromSeconds(2) };
        _autoSaveTimer.Tick += AutoSaveTimerTickAsync;
        _stickyScrollTimer = new Avalonia.Threading.DispatcherTimer
        { Interval = TimeSpan.FromMilliseconds(80) };
        _stickyScrollTimer.Tick += StickyScrollTimer_Tick;
        _renderTimer = new Avalonia.Threading.DispatcherTimer(
            TimeSpan.FromMilliseconds(1000.0 / 120.0), // Поддержка до 120-144 Гц
            Avalonia.Threading.DispatcherPriority.Render,
            RenderTimer_Tick);
        SetupSyntaxHighlighting();
        SetupEditorOptions();
        SetupBracketHandling();
        CodeEditor.AddHandler(PointerWheelChangedEvent,
            CodeEditorPointerWheelChanged, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        CodeEditor.AddHandler(KeyDownEvent,
            CodeEditorKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        CodeEditor.TextArea.Caret.CaretBrush = Brushes.Transparent;
        CodeEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged;
        CodeEditor.TextArea.TextView.ScrollOffsetChanged += TextView_ScrollOffsetChanged;
        CodeEditor.TextArea.TextView.LineTransformers.Add(_searchHighlightTransformer);
        CodeEditor.TextArea.TextView.LineTransformers.Add(_pasteTransformer);
        MinimapEditor.Document = CodeEditor.Document;
        CodeEditor.TemplateApplied += (s, e) =>
        {
            if (CodeEditor.FindDescendantOfType<ScrollViewer>() is { } sv)
            {
                sv.ScrollChanged += ScrollViewer_ScrollChanged;
                _stickyScrollTimer.Start();
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
    private void SetupEditorOptions()
    {
        CodeEditor.Options.EnableHyperlinks = true;
        CodeEditor.Options.RequireControlModifierForHyperlinkClick = true;
        CodeEditor.Options.HighlightCurrentLine = true;
        CodeEditor.Options.ShowBoxForControlCharacters = true;
        CodeEditor.Options.EnableEmailHyperlinks = false;
        CodeEditor.Options.IndentationSize = 4;
        CodeEditor.Options.ConvertTabsToSpaces = true;
        CodeEditor.Options.EnableRectangularSelection = true;
        CodeEditor.ShowLineNumbers = true;
        CodeEditor.TextArea.IndentationStrategy = new SmartIndentationStrategy();
        CodeEditor.TextArea.SelectionCornerRadius = 3;
        CodeEditor.TextArea.SelectionBrush = new SolidColorBrush(Color.FromArgb(80, 59, 130, 246));
    }
    private void SetupBracketHandling()
    {
        CodeEditor.TextArea.TextEntering += TextArea_TextEntering;
        CodeEditor.TextArea.TextEntered += TextArea_TextEntered;
    }
    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        => ApplySettings();
    private void ApplySettings()
    {
        var s = SettingsService.Instance;
        CodeEditor.Options.ShowSpaces = s.ShowWhitespaces;
        CodeEditor.Options.ShowTabs = s.ShowWhitespaces;
        CodeEditor.Options.HighlightCurrentLine = true;
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
                UpdateEditorFromTab(newVm);
            }
        }
    }
    private Task ViewModel_FileSaving()
    {
        if (DataContext is MainPageModel vm && vm.ActiveTab != null)
        {
            vm.ActiveTab.Content = CodeEditor.Text;
        }
        return Task.CompletedTask;
    }
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainPageModel.ActiveTab)
            && DataContext is MainPageModel vm)
        {
            UpdateEditorFromTab(vm);
        }
    }
    private EditorTab? _previousTab;
    private void UpdateEditorFromTab(MainPageModel vm)
    {
        _isUpdatingFromViewModel = true;
        try
        {
            if (_previousTab != null && CodeEditor.Document != null)
            {
                _previousTab.Document?.Changed -= Document_Changed;
                _previousTab.SavedCaretOffset = CodeEditor.CaretOffset;
                var sv = CodeEditor.FindDescendantOfType<ScrollViewer>();
                if (sv != null)
                {
                    _previousTab.SavedScrollOffset = sv.Offset;
                }

                _previousTab.Content = CodeEditor.Document.Text;
            }
            if (vm.ActiveTab != null)
            {
                if (vm.ActiveTab.Document == null)
                {
                    vm.ActiveTab.Document = new TextDocument(vm.ActiveTab.Content ?? string.Empty);
                }
                CodeEditor.Document = vm.ActiveTab.Document;
                CodeEditor.Document.Changed += Document_Changed;
                MinimapEditor.Document = vm.ActiveTab.Document;
                SetLanguage(vm.ActiveTab.FilePath);
                if (!string.IsNullOrEmpty(vm.WorkspacePath))
                {
                    var cfg = EditorConfigService.Parse(vm.WorkspacePath, vm.ActiveTab.FilePath);
                    CodeEditor.Options.ConvertTabsToSpaces = cfg.IndentStyle == "space";
                    CodeEditor.Options.IndentationSize = cfg.IndentSize;
                }
                ClearSearch();
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    CodeEditor.CaretOffset = Math.Min(vm.ActiveTab.SavedCaretOffset, CodeEditor.Document.TextLength);
                    var sv = CodeEditor.FindDescendantOfType<ScrollViewer>();
                    _ = (sv?.Offset = vm.ActiveTab.SavedScrollOffset);
                    _ = CodeEditor.Focus();
                });
            }
            else
            {
                CodeEditor.Document = new TextDocument();
                MinimapEditor.Document = CodeEditor.Document;
            }
            _previousTab = vm.ActiveTab;
        }
        finally
        {
            _isUpdatingFromViewModel = false;
        }
    }
    private void SetLanguage(string filePath)
    {
        if (_textMateInstallation is null || _registryOptions is null
            || _minimapTextMateInstallation is null)
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
        _textMateInstallation.SetGrammar(scope);
        _minimapTextMateInstallation.SetGrammar(scope);
    }
    private void CodeEditor_TextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingFromViewModel || DataContext is not MainPageModel vm || vm.ActiveTab is null)
        {
            return;
        }

        vm.ActiveTab.IsModified = true;
        if (SettingsService.Instance.AutoSave)
        {
            _autoSaveTimer?.Stop();
            _autoSaveTimer?.Start();
        }
        if (SearchPanel?.IsVisible == true || ReplacePanel?.IsVisible == true)
        {
            RunSearch();
        }
    }
    private async void AutoSaveTimerTickAsync(object? sender, EventArgs e)
    {
        _autoSaveTimer?.Stop();
        if (DataContext is MainPageModel vm && vm.ActiveTab?.IsModified == true)
        {
            var textToSave = CodeEditor.Text;
            var filePath = vm.ActiveTab.FilePath;
            try
            {
                var provider = vm.ActiveTab.Root?.Provider ?? new LocalFileSystemProvider();
                await WorkspaceService.SaveFileAsync(provider, filePath, textToSave);
                vm.ActiveTab.Content = textToSave;
                vm.ActiveTab.IsModified = false;
                vm.StatusText = $"✅ Автосохранено ({DateTime.Now:HH:mm:ss})";
            }
            catch (Exception ex)
            {
                vm.StatusText = $"❌ Ошибка автосохранения: {ex.Message}";
            }
        }
    }
    private void CodeEditorKeyDown(object? sender, KeyEventArgs e)
    {
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
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
            ToggleLineComment();
            e.Handled = true;
            return;
        }
        if (ctrl && !shift && e.Key == Key.D)
        {
            SelectNextOccurrence();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Tab && !ctrl && !shift)
        {
            if (CodeEditor.TextArea.Selection.IsEmpty)
            {
                return;
            }

            IndentSelection();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Tab && shift)
        {
            UnindentSelection();
            e.Handled = true;
            return;
        }
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            if (e.Key == Key.Up)
            { MoveLineUp(); e.Handled = true; }
            else if (e.Key == Key.Down)
            { MoveLineDown(); e.Handled = true; }
        }
    }
    private static readonly Dictionary<char, char> t_bracketPairs = new()
    {
        { '(', ')' }, { '[', ']' }, { '{', '}' }, { '"', '"' }, { '\'', '\'' }
    };
    private char _pendingClose;
    private void TextArea_TextEntering(object? sender, TextInputEventArgs e)
    {
        if (e.Text?.Length != 1)
        {
            return;
        }

        var ch = e.Text[0];
        if (ch == _pendingClose && _pendingClose != '\0')
        {
            var offset = CodeEditor.TextArea.Caret.Offset;
            if (offset < CodeEditor.Document.TextLength
                && CodeEditor.Document.GetCharAt(offset) == ch)
            {
                CodeEditor.TextArea.Caret.Offset++;
                e.Handled = true;
                _pendingClose = '\0';
            }
        }
    }
    private void TextArea_TextEntered(object? sender, TextInputEventArgs e)
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

        var offset = CodeEditor.TextArea.Caret.Offset;
        if (offset < CodeEditor.Document.TextLength)
        {
            var next = CodeEditor.Document.GetCharAt(offset);
            if (!char.IsWhiteSpace(next) && next != '\n' && next != '\r')
            {
                return;
            }
        }
        CodeEditor.Document.Insert(offset, close.ToString());
        CodeEditor.TextArea.Caret.Offset = offset;
        _pendingClose = close;
    }
    private void ShowSearch(bool replaceMode)
    {
        _replaceMode = replaceMode;
        if (replaceMode)
        {
            SearchPanel.IsVisible = false;
            ReplacePanel.IsVisible = true;
            _ = ReplacePanel.SlideAndFadeInAsync();
            _ = SearchBox2.Focus();
            if (!CodeEditor.TextArea.Selection.IsEmpty)
            {
                SearchBox2.Text = CodeEditor.TextArea.Selection.GetText();
            }
        }
        else
        {
            ReplacePanel.IsVisible = false;
            SearchPanel.IsVisible = true;
            _ = SearchPanel.SlideAndFadeInAsync();
            _ = SearchBox.Focus();
            if (!CodeEditor.TextArea.Selection.IsEmpty)
            {
                SearchBox.Text = CodeEditor.TextArea.Selection.GetText();
            }
        }
    }
    private void HideSearch()
    {
        SearchPanel.IsVisible = false;
        ReplacePanel.IsVisible = false;
        ClearSearch();
        _ = CodeEditor.Focus();
    }
    private void ClearSearch()
    {
        _searchResults.Clear();
        _searchIndex = -1;
        _searchHighlightTransformer?.UpdateSearch(_searchResults, _searchIndex);
        CodeEditor?.TextArea.TextView.Redraw();
    }
    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e) => RunSearch();
    private void OnReplaceSearchChanged(object? sender, TextChangedEventArgs e) => RunSearch();
    private void OnSearchOptionsChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => RunSearch();
    private void RunSearch()
    {
        var query = _replaceMode ? SearchBox2.Text : SearchBox.Text;
        if (string.IsNullOrEmpty(query))
        {
            ClearSearch();
            return;
        }
        var matchCase = _replaceMode ? MatchCase2Btn.IsChecked == true : MatchCaseBtn.IsChecked == true;
        var wholeWord = _replaceMode ? WholeWord2Btn.IsChecked == true : WholeWordBtn.IsChecked == true;
        var useRegex = _replaceMode ? Regex2Btn.IsChecked == true : RegexBtn.IsChecked == true;
        _searchResults.Clear();
        _searchIndex = -1;
        try
        {
            var mode = useRegex ? SearchMode.RegEx : SearchMode.Normal;
            var strategy = SearchStrategyFactory.Create(query, !matchCase, wholeWord, mode);
            var results = strategy.FindAll(CodeEditor.Document, 0, CodeEditor.Document.TextLength);
            foreach (var r in results)
            {
                _searchResults.Add(new TextSegment { StartOffset = r.Offset, Length = r.Length });
            }
        }
        catch { }
        if (_searchResults.Count > 0)
        {
            var cur = CodeEditor.TextArea.Caret.Offset;
            _searchIndex = _searchResults.FindIndex(s => s.StartOffset >= cur);
            if (_searchIndex < 0)
            {
                _searchIndex = 0;
            }

            NavigateToSearchResult(_searchIndex);
        }
        _searchHighlightTransformer.UpdateSearch(_searchResults, _searchIndex);
        CodeEditor.TextArea.TextView.Redraw();
    }

    private void NavigateToSearchResult(int index)
    {
        if (index < 0 || index >= _searchResults.Count)
        {
            return;
        }

        var seg = _searchResults[index];
        CodeEditor.Select(seg.StartOffset, seg.Length);
        CodeEditor.TextArea.Caret.Offset = seg.StartOffset;
        CodeEditor.ScrollTo(
            CodeEditor.Document.GetLineByOffset(seg.StartOffset).LineNumber, 0);
        _searchHighlightTransformer.UpdateSearch(_searchResults, index);
        CodeEditor.TextArea.TextView.Redraw();
    }
    private void OnSearchNext(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_searchResults.Count == 0)
        { RunSearch(); return; }
        _searchIndex = (_searchIndex + 1) % _searchResults.Count;
        NavigateToSearchResult(_searchIndex);
    }
    private void OnSearchPrev(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_searchResults.Count == 0)
        { RunSearch(); return; }
        _searchIndex = (_searchIndex - 1 + _searchResults.Count) % _searchResults.Count;
        NavigateToSearchResult(_searchIndex);
    }
    private void OnReplaceOne(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_searchIndex < 0 || _searchIndex >= _searchResults.Count)
        {
            return;
        }

        var seg = _searchResults[_searchIndex];
        var replacement = ReplaceBox.Text ?? "";
        CodeEditor.Document.Replace(seg.StartOffset, seg.Length, replacement);
        RunSearch();
    }
    private void OnReplaceAll(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        RunSearch();
        if (_searchResults.Count == 0)
        {
            return;
        }

        var replacement = ReplaceBox.Text ?? "";
        using (CodeEditor.Document.RunUpdate())
        {
            for (var i = _searchResults.Count - 1; i >= 0; i--)
            {
                var seg = _searchResults[i];
                CodeEditor.Document.Replace(seg.StartOffset, seg.Length, replacement);
            }
        }
        ClearSearch();
    }
    private void ToggleLineComment()
    {
        if (DataContext is not MainPageModel vm || vm.ActiveTab is null)
        {
            return;
        }

        var ext = Path.GetExtension(vm.ActiveTab.FilePath).ToLower(System.Globalization.CultureInfo.CurrentCulture);
        var prefix = ext switch
        {
            ".cs" or ".c" or ".cpp" or ".h" or ".hpp" or ".java" or ".js" or ".ts" => "//",
            ".py" or ".sh" or ".yaml" or ".yml" => "#",
            ".sql" => "--",
            _ => "//"
        };
        var selection = CodeEditor.TextArea.Selection;
        var doc = CodeEditor.Document;
        int startLine, endLine;
        if (selection.IsEmpty)
        {
            startLine = endLine = CodeEditor.TextArea.Caret.Line;
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
    private void SelectNextOccurrence()
    {
        var sel = CodeEditor.TextArea.Selection;
        string query;
        if (sel.IsEmpty)
        {
            var offset = CodeEditor.TextArea.Caret.Offset;
            var doc = CodeEditor.Document;
            var start = offset;
            var end = offset;
            while (start > 0 && char.IsLetterOrDigit(doc.GetCharAt(start - 1)))
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

            CodeEditor.Select(start, end - start);
            return;
        }
        query = sel.GetText();
        if (string.IsNullOrEmpty(query))
        {
            return;
        }

        var text = CodeEditor.Document.Text;
        var from = CodeEditor.TextArea.Selection.SurroundingSegment.Offset
                   + CodeEditor.TextArea.Selection.SurroundingSegment.Length;
        var next = text.IndexOf(query, from, StringComparison.Ordinal);
        if (next < 0)
        {
            next = text.IndexOf(query, 0, StringComparison.Ordinal);
        }

        if (next >= 0)
        {
            CodeEditor.Select(next, query.Length);
            CodeEditor.TextArea.Caret.Offset = next;
            CodeEditor.ScrollTo(CodeEditor.Document.GetLineByOffset(next).LineNumber, 0);
        }
    }
    private void IndentSelection()
    {
        var doc = CodeEditor.Document;
        var sel = CodeEditor.TextArea.Selection.SurroundingSegment;
        var startLine = doc.GetLineByOffset(sel.Offset).LineNumber;
        var endLine = doc.GetLineByOffset(sel.Offset + sel.Length).LineNumber;
        var indent = CodeEditor.Options.ConvertTabsToSpaces
            ? new string(' ', CodeEditor.Options.IndentationSize)
            : "\t";
        using (doc.RunUpdate())
        {
            for (var ln = startLine; ln <= endLine; ln++)
            {
                doc.Insert(doc.GetLineByNumber(ln).Offset, indent);
            }
        }
    }
    private void UnindentSelection()
    {
        var doc = CodeEditor.Document;
        var sel = CodeEditor.TextArea.Selection.SurroundingSegment;
        var startLine = doc.GetLineByOffset(sel.Offset).LineNumber;
        var endLine = doc.GetLineByOffset(sel.Offset + sel.Length).LineNumber;
        var size = CodeEditor.Options.IndentationSize;
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
    private void MoveLineUp()
    {
        var doc = CodeEditor.Document;
        var ln = CodeEditor.TextArea.Caret.Line;
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
        CodeEditor.TextArea.Caret.Line = ln - 1;
    }
    private void MoveLineDown()
    {
        var doc = CodeEditor.Document;
        var ln = CodeEditor.TextArea.Caret.Line;
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
        CodeEditor.TextArea.Caret.Line = ln + 1;
    }
    private void StickyScrollTimer_Tick(object? sender, EventArgs e)
    {
        if (!IsVisible)
        {
            return;
        }

        UpdateStickyScroll();
    }
    private void UpdateStickyScroll()
    {
        var sv = CodeEditor.FindDescendantOfType<ScrollViewer>();
        if (sv is null)
        {
            return;
        }

        var firstVisibleLine = CodeEditor.TextArea.TextView
            .GetDocumentLineByVisualTop(sv.Offset.Y);
        if (firstVisibleLine is null)
        {
            StickyScrollBorder.IsVisible = false;
            return;
        }
        var doc = CodeEditor.Document;
        var lineNum = firstVisibleLine.LineNumber;
        var scopeLine = FindScopeLine(doc, lineNum);
        if (scopeLine is null)
        {
            StickyScrollBorder.IsVisible = false;
            return;
        }
        var text = doc.GetText(scopeLine.Offset, scopeLine.Length).Trim();
        StickyScrollText.Text = text;
        StickyScrollBorder.IsVisible = true;
    }
    private static DocumentLine? FindScopeLine(TextDocument doc, int fromLine)
    {
        for (var ln = fromLine - 1; ln >= 1; ln--)
        {
            var line = doc.GetLineByNumber(ln);
            var text = doc.GetText(line.Offset, line.Length).TrimStart();
            if (text.StartsWith("class ") || text.StartsWith("struct ") ||
                text.StartsWith("namespace ") || text.StartsWith("public ") ||
                text.StartsWith("private ") || text.StartsWith("protected ") ||
                text.StartsWith("internal ") || text.StartsWith("static ") ||
                text.StartsWith("void ") || text.StartsWith("async ") ||
                text.StartsWith("def ") || text.StartsWith("fn "))
            {
                return line;
            }
        }
        return null;
    }
    private void ScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        var mainSv = CodeEditor.FindDescendantOfType<ScrollViewer>();
        var miniSv = MinimapEditor.FindDescendantOfType<ScrollViewer>();
        if (mainSv is null || miniSv is null)
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

            var vpRatio = MinimapEditor.Bounds.Height / mainSv.Extent.Height;
            MinimapViewport.Height = Math.Max(10, mainSv.Viewport.Height * vpRatio);

            var pctTop = mainSv.Offset.Y / mainSv.Extent.Height;
            var vpTop = pctTop * MinimapEditor.Bounds.Height;
            MinimapViewport.Margin = new Thickness(0, vpTop, 0, 0);
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
            ScrollMainEditorToMinimapY(e.GetPosition(sender as Control).Y);
            e.Handled = true;
        }
    }
    private void Minimap_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isMinimapDragging && sender is Control control)
        {
            ScrollMainEditorToMinimapY(e.GetPosition(control).Y);
            e.Handled = true;
        }
    }
    private void Minimap_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isMinimapDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }
    private void Minimap_PointerWheelChanged(object? sender, PointerWheelEventArgs e) => CodeEditorPointerWheelChanged(CodeEditor, e);
    private void ScrollMainEditorToMinimapY(double minimapY)
    {
        var mainSv = CodeEditor.FindDescendantOfType<ScrollViewer>();
        if (mainSv == null || mainSv.Extent.Height == 0 || MinimapEditor.Bounds.Height == 0)
        {
            return;
        }

        var clickRatio = minimapY / MinimapEditor.Bounds.Height;
        var targetY = (clickRatio * mainSv.Extent.Height) - (mainSv.Viewport.Height / 2);
        targetY = Math.Clamp(targetY, 0, mainSv.Extent.Height - mainSv.Viewport.Height);

        _targetScrollY = targetY;
        _isSmoothScrolling = true;
        _renderTimer?.Start();
    }
    public void CodeEditorPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            CodeEditor.FontSize = Math.Clamp(
                CodeEditor.FontSize + (e.Delta.Y > 0 ? 1 : -1), 8, 72);
            e.Handled = true;
            return;
        }

        var sv = CodeEditor.FindDescendantOfType<ScrollViewer>();
        if (sv == null || sv.Extent.Height <= sv.Viewport.Height)
        {
            return;
        }

        if (!_isSmoothScrolling)
        {
            _targetScrollY = sv.Offset.Y;
            _isSmoothScrolling = true;
        }

        _targetScrollY -= e.Delta.Y * 60;
        _targetScrollY = Math.Clamp(_targetScrollY, 0, sv.Extent.Height - sv.Viewport.Height);

        e.Handled = true;
        _renderTimer?.Start();
    }

    private void RenderTimer_Tick(object? sender, EventArgs e)
    {
        var sv = CodeEditor.FindDescendantOfType<ScrollViewer>();
        if (sv == null)
        { _renderTimer?.Stop(); _isSmoothScrolling = false; return; }

        var currentY = sv.Offset.Y;

        // Линейная интерполяция (Lerp) для кинематографичного скольжения
        var newY = currentY + ((_targetScrollY - currentY) * 0.15);

        // Если почти доехали - останавливаемся
        if (Math.Abs(_targetScrollY - newY) < 1.0)
        {
            sv.Offset = new Vector(sv.Offset.X, _targetScrollY);
            _renderTimer?.Stop();
            _isSmoothScrolling = false;
        }
        else
        {
            sv.Offset = new Vector(sv.Offset.X, newY);
        }
    }

    private void TextView_ScrollOffsetChanged(object? sender, EventArgs e) => UpdateSmoothCaret();

    private void UpdateSmoothCaret()
    {
        if (CodeEditor.Document == null)
        {
            return;
        }

        var textView = CodeEditor.TextArea.TextView;
        if (textView == null || !textView.VisualLinesValid)
        {
            return;
        }

        try
        {
            var pos = textView.GetVisualPosition(CodeEditor.TextArea.Caret.Position, AvaloniaEdit.Rendering.VisualYPosition.LineTop);
            var scrollOffset = textView.ScrollOffset;

            var p = new Point(pos.X - scrollOffset.X, pos.Y - scrollOffset.Y);
            var translated = textView.TranslatePoint(p, CodeEditor);

            if (translated.HasValue)
            {
                var x = translated.Value.X;
                var y = translated.Value.Y;

                SmoothCaret.Height = CodeEditor.FontSize * 1.2;
                SmoothCaret.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse(
                    $"translate({x.ToString(System.Globalization.CultureInfo.InvariantCulture)}px, {y.ToString(System.Globalization.CultureInfo.InvariantCulture)}px)");
            }
        }
        catch
        {
            // VisualLines могут быть еще невалидны при переключении вкладок
        }
    }

    private void Document_Changed(object? sender, DocumentChangeEventArgs e)
    {
        if (e.InsertedText?.Text.Length > 1)
        {
            _pasteTransformer.StartOffset = e.Offset;
            _pasteTransformer.EndOffset = e.Offset + e.InsertionLength;
            _pasteTransformer.CurrentOpacity = 1.0;

            var timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            timer.Tick += (s, ev) =>
            {
                _pasteTransformer.CurrentOpacity -= 0.08;
                if (_pasteTransformer.CurrentOpacity <= 0)
                {
                    _pasteTransformer.CurrentOpacity = 0;
                    timer.Stop();
                }
                CodeEditor.TextArea.TextView.Redraw(new TextSegment
                {
                    StartOffset = _pasteTransformer.StartOffset,
                    Length = _pasteTransformer.EndOffset - _pasteTransformer.StartOffset
                });
            };
            timer.Start();
        }
    }

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        UpdateSmoothCaret();
        if (DataContext is MainPageModel vm)
        {
            var line = CodeEditor.TextArea.Caret.Line;
            var column = CodeEditor.TextArea.Caret.Column;
            vm.CursorPosition = $"Ln {line}, Col {column}";
        }
    }
}
