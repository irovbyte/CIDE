namespace CIDE.PageModels;

[QueryProperty(nameof(WorkspacePath), "path")]
internal sealed partial class MainPageModel : ObservableObject
{
    [ObservableProperty]
    public partial ObservableCollection<FileNode> FlatTree { get; set; } = [];
    [ObservableProperty]
    public partial ObservableCollection<EditorTab> Tabs { get; set; } = [];
    [ObservableProperty]
    public partial EditorTab? ActiveTab { get; set; }
    [ObservableProperty]
    public partial string StatusText { get; set; } = "Готов";
    [ObservableProperty]
    public partial string ExplorerTitle { get; set; } = "ОБОЗРЕВАТЕЛЬ";
    [ObservableProperty]
    public partial bool IsEditorVisible { get; set; }
    [ObservableProperty]
    public partial ObservableCollection<string> BuildProfiles { get; set; } = ["Один файл (C/C++)", "Makefile (C/C++)", "Проект C# (.slnx/.csproj)"];
    [ObservableProperty]
    public partial string SelectedBuildProfile { get; set; } = "Один файл (C/C++)";
    [ObservableProperty]
    public partial bool IsOutputVisible { get; set; }
    [ObservableProperty]
    public partial string CompilerOutput { get; set; } = "";
    [ObservableProperty]
    public partial string Breadcrumbs { get; set; } = "";
    [ObservableProperty]
    public partial bool IsAutoSaveEnabled { get; set; } = true;
    [ObservableProperty]
    public partial ObservableCollection<string> TerminalProfiles { get; set; } = [];
    [ObservableProperty]
    public partial string SelectedTerminalProfile { get; set; } = "";
    [ObservableProperty]
    public partial ObservableCollection<string> BuildTargets { get; set; } = [];
    [ObservableProperty]
    public partial string? SelectedBuildTarget { get; set; }
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
        DetectTerminals();
        UpdateBuildProfilesAndTargets(path);
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
    private void ToggleNode(FileNode? node)
    {
        if (node is null || node.Kind == FileNodeKind.File)
        {
            return;
        }
        if (!node.IsPopulated)
        {
            WorkspaceService.FillChildren(node, new DirectoryInfo(node.FullPath), node.Depth + 1);
        }
        node.IsExpanded = !node.IsExpanded;
        var index = FlatTree.IndexOf(node);
        if (index < 0)
        {
            return;
        }
        if (node.IsExpanded)
        {
            var insertIndex = index + 1;
            InsertNodeChildren(node, ref insertIndex);
        }
        else
        {
            var countToRemove = CountVisibleDescendants(node);
            for (var i = 0; i < countToRemove; i++)
            {
                FlatTree.RemoveAt(index + 1);
            }
        }
    }
    private void InsertNodeChildren(FileNode node, ref int insertIndex)
    {
        foreach (var child in node.Children)
        {
            FlatTree.Insert(insertIndex++, child);
            if (child.IsExpanded)
            {
                InsertNodeChildren(child, ref insertIndex);
            }
        }
    }
    private static int CountVisibleDescendants(FileNode node)
    {
        var count = node.Children.Count;
        foreach (var child in node.Children)
        {
            if (child.IsExpanded)
            {
                count += CountVisibleDescendants(child);
            }
        }
        return count;
    }
    [RelayCommand]
    private async Task SelectNodeAsync(FileNode? node)
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
            await ToggleNodeAsync(node);
        }
    }
    private async Task ToggleNodeAsync(FileNode node)
    {
        if (!node.IsPopulated)
        {
            await Task.Run(() => WorkspaceService.FillChildren(node, new DirectoryInfo(node.FullPath), node.Depth + 1));
        }
        node.IsExpanded = !node.IsExpanded;
        var index = FlatTree.IndexOf(node);
        if (index < 0)
        {
            return;
        }
        if (node.IsExpanded)
        {
            var insertIndex = index + 1;
            InsertNodeChildren(node, ref insertIndex);
        }
        else
        {
            var countToRemove = CountVisibleDescendants(node);
            for (var i = 0; i < countToRemove; i++)
            {
                FlatTree.RemoveAt(index + 1);
            }
        }
    }

    private async Task OpenFileAsync(string path)
    {
        var existing = Tabs.FirstOrDefault(t => t.FilePath == path);
        if (existing != null)
        {
            await ActivateTabAsync(existing);
            return;
        }

        StatusText = $"Загрузка: {Path.GetFileName(path)}";
        var content = await WorkspaceService.ReadFileAsync(path);
        EditorTab tab = new() { FilePath = path, Content = content };
        Tabs.Add(tab);
        await ActivateTabAsync(tab);
        StatusText = "Готов";
    }

    [RelayCommand]
    private async Task ActivateTabAsync(EditorTab? tab)
    {
        if (tab is null)
        {
            return;
        }
        if (ActiveTab != null && FileSaving != null)
        {
            await FileSaving.Invoke();
            if (IsAutoSaveEnabled && ActiveTab.IsModified)
            {
                await WorkspaceService.SaveFileAsync(ActiveTab.FilePath, ActiveTab.Content);
                ActiveTab.IsModified = false;
            }
        }

        foreach (var t in Tabs)
        {
            t.IsActive = false;
        }

        tab.IsActive = true;
        ActiveTab = tab;
        Breadcrumbs = tab.FilePath.Replace(WorkspacePath ?? "", "").TrimStart('\\', '/').Replace("\\", " > ").Replace("/", " > ");
        IsEditorVisible = true;
        var monacoLang = Path.GetExtension(tab.FilePath).ToLowerInvariant() switch
        {
            ".c" => "c",
            ".cpp" or ".h" or ".hpp" => "cpp",
            ".cs" => "csharp",
            ".json" => "json",
            ".xml" or ".xaml" or ".csproj" => "xml",
            ".md" => "markdown",
            _ => "plaintext"
        };
        FileOpened?.Invoke(tab.Content, monacoLang);
    }

    [RelayCommand]
    internal async Task CloseTabAsync(EditorTab? tab)
    {
        if (tab != null)
        {
            _ = Tabs.Remove(tab);
        }

        if (Tabs.Count == 0)
        {
            ActiveTab = null;
            IsEditorVisible = false;
            Breadcrumbs = "";
        }
        else
        {
            await ActivateTabAsync(Tabs.Last());
        }
    }

    [RelayCommand]
    private async Task SaveActiveFileAsync()
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
    internal async Task GoBackAsync()
    {
        _ = IsOutputVisible;
        await Shell.Current.GoToAsync("//WelcomePage");
    }

    [RelayCommand]
    internal void ToggleOutput() => IsOutputVisible = !IsOutputVisible;

    [RelayCommand]
    internal async Task RunCommandAsync()
    {
        IsOutputVisible = true;
        CompilerOutput = "";
        await SaveActiveFileAsync();
        await CompileService.RunCompilationAsync(SelectedBuildProfile, ActiveTab?.FilePath ?? "", WorkspacePath ?? "", output => MainThread.BeginInvokeOnMainThread(() => CompilerOutput += output));
    }
    private void DetectTerminals()
    {
        TerminalProfiles.Clear();
        TerminalProfiles.Add("PowerShell");
        TerminalProfiles.Add("CMD");
        var wslPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsl.exe");
        if (File.Exists(wslPath))
        {
            TerminalProfiles.Add("WSL");
        }
        SelectedTerminalProfile = TerminalProfiles[0];
    }
    private void UpdateBuildProfilesAndTargets(string path)
    {
        var dir = new DirectoryInfo(path);
        BuildProfiles.Clear();
        BuildTargets.Clear();
        var hasCSharp = dir.GetFiles("*.csproj", SearchOption.AllDirectories).Length > 0 ||
                         dir.GetFiles("*.slnx", SearchOption.AllDirectories).Length > 0;
        var hasMakefile = dir.GetFiles("Makefile", SearchOption.TopDirectoryOnly).Length > 0;
        if (hasCSharp)
        {
            BuildProfiles.Add("Проект C#");
            BuildTargets.Add("Основной проект");
        }
        else
        {
            BuildProfiles.Add("Один файл (C/C++)");
            if (hasMakefile)
            {
                BuildProfiles.Add("Makefile");
            }
            var cFiles = dir.GetFiles("*.*", SearchOption.AllDirectories)
                            .Where(f => f.Extension == ".c" || f.Extension == ".cpp");
            foreach (var f in cFiles)
            {
                BuildTargets.Add(f.Name);
            }
        }
        if (BuildProfiles.Count > 0)
        {
            SelectedBuildProfile = BuildProfiles[0];
        }
        if (BuildTargets.Count > 0)
        {
            SelectedBuildTarget = BuildTargets[0];
        }
    }

    [RelayCommand]
    internal async Task BuildCommandAsync() => await RunCommandAsync();
}
