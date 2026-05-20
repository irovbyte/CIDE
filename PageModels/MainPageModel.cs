namespace CIDE.PageModels;
[QueryProperty(nameof(WorkspacePath), "path")]
internal partial class MainPageModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<FileNode> _flatTree = [];
    [ObservableProperty]
    private ObservableCollection<EditorTab> _tabs = [];
    [ObservableProperty]
    private EditorTab? _activeTab;
    [ObservableProperty]
    private string _statusText = "Готов";
    [ObservableProperty]
    private string _explorerTitle = "ОБОЗРЕВАТЕЛЬ";
    [ObservableProperty]
    private bool _isEditorVisible;
    private FileNode? _rootNode;
    private string? _workspacePath { get; set; }
    internal event Action<string, string>? FileOpened;
    internal event Func<Task>? FileSaving;
    public string? WorkspacePath
    {
        get => _workspacePath;
        set
        {
            _workspacePath = value;
            if (!string.IsNullOrEmpty(value))
            {
                LoadWorkspace(value);
            }
        }
    }
    private void LoadWorkspace(string path)
    {
        _rootNode = WorkspaceService.BuildFolderTree(path);
        ExplorerTitle = Path.GetFileName(path).ToUpperInvariant();
        RebuildFlatTree();
        StatusText = $"Открыт проект: {ExplorerTitle}";
    }
    private void RebuildFlatTree()
    {
        FlatTree.Clear();
        if (_rootNode != null)
        {
            AddFlatNodes(_rootNode);
        }
    }
    private void AddFlatNodes(FileNode node)
    {
        FlatTree.Add(node);
        if (node.IsExpanded)
        {
            foreach (var child in node.Children)
            {
                AddFlatNodes(child);
            }
        }
    }
    [RelayCommand]
    internal void ToggleNode(FileNode? node)
    {
        if (node is null || node.Kind == FileNodeKind.File)
        {
            return;
        }
        node.IsExpanded = !node.IsExpanded;
        RebuildFlatTree();
    }
    [RelayCommand]
    internal async Task SelectNodeAsync(FileNode? node)
    {
        if (node is null)
        {
            return;
        }
        if (node.Kind == FileNodeKind.File)
        {
            await OpenFileAsync(node.FullPath);
        }
        else
        {
            ToggleNode(node);
        }
    }
    private async Task OpenFileAsync(string path)
    {
        var existing = Tabs.FirstOrDefault(t => t.FilePath == path);
        if (existing != null)
        {
            ActivateTab(existing);
            return;
        }
        StatusText = $"Загрузка: {Path.GetFileName(path)}";
        var content = await WorkspaceService.ReadFileAsync(path);
        EditorTab tab = new() { FilePath = path, Content = content };
        Tabs.Add(tab);
        ActivateTab(tab);
        StatusText = "Готов";
    }
    [RelayCommand]
    internal void ActivateTab(EditorTab? tab)
    {
        if (tab is null)
        {
            return;
        }
        foreach (var t in Tabs)
        {
            t.IsActive = false;
        }
        tab.IsActive = true;
        ActiveTab = tab;
        _isEditorVisible = true;
        var monacoLang = Path.GetExtension(tab.FilePath).ToLowerInvariant() switch
        {
            ".c" => "c",
            ".cpp" or ".h" or ".hpp" => "cpp",
            ".cs" => "csharp",
            ".json" => "json",
            ".xml" or ".xaml" or ".csproj" => "xml",
            _ => "plaintext"
        };
        FileOpened?.Invoke(tab.Content, monacoLang);
    }
    [RelayCommand]
    internal void CloseTab(EditorTab? tab)
    {
        if (tab != null)
        {
            _ = Tabs.Remove(tab);
        }
        if (Tabs.Count == 0)
        {
            ActiveTab = null;
            _isEditorVisible = false;
        }
        else
        {
            ActivateTab(Tabs.Last());
        }
    }
    [RelayCommand]
    internal async Task SaveActiveFileAsync()
    {
        if (ActiveTab is null || FileSaving is null)
        {
            return;
        }
        await FileSaving.Invoke();
        await WorkspaceService.SaveFileAsync(ActiveTab.FilePath, ActiveTab.Content);
        ActiveTab.IsModified = false;
        StatusText = "✅ Сохранено";
    }
    [RelayCommand]
    internal static async Task GoBackAsync() => await Shell.Current.GoToAsync("//WelcomePage");
}
