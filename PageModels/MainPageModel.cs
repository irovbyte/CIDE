namespace CIDE.PageModels;

public enum AppState
{
    Loading,
    Welcome,
    Editor
}

public sealed partial class MainPageModel : ObservableObject
{
    [ObservableProperty]
    public partial AppState CurrentState { get; set; } = AppState.Loading;

    public bool IsLoadingState => CurrentState == AppState.Loading;
    public bool IsWelcomeState => CurrentState == AppState.Welcome;
    public bool IsWorkspaceOpen => CurrentState == AppState.Editor;

    partial void OnCurrentStateChanged(AppState value)
    {
        OnPropertyChanged(nameof(IsLoadingState));
        OnPropertyChanged(nameof(IsWelcomeState));
        OnPropertyChanged(nameof(IsWorkspaceOpen));
    }

    [ObservableProperty]
    public partial ObservableCollection<FileNode> FlatTree { get; set; } = [];
    [ObservableProperty]
    public partial ObservableCollection<EditorTab> Tabs { get; set; } = [];

    public static SettingsService Settings => SettingsService.Instance;

    [ObservableProperty]
    public partial EditorTab? ActiveTab { get; set; }
    [ObservableProperty]
    public partial string StatusText { get; set; } = "Готов";
    [ObservableProperty]
    public partial string ExplorerTitle { get; set; } = "ОБОЗРЕВАТЕЛЬ";
    [ObservableProperty]
    public partial bool IsEditorVisible { get; set; }
    [ObservableProperty]
    public partial ObservableCollection<string> BuildProfiles { get; set; } = [];
    [ObservableProperty]
    public partial string SelectedBuildProfile { get; set; } = "Один файл (C/C++)";
    [ObservableProperty]
    public partial bool IsOutputVisible { get; set; }
    [ObservableProperty]
    public partial string CompilerOutput { get; set; } = "";
    [ObservableProperty]
    public partial string Breadcrumbs { get; set; } = "";
    [ObservableProperty]
    public partial ObservableCollection<string> TerminalProfiles { get; set; } = [];
    [ObservableProperty]
    public partial string SelectedTerminalProfile { get; set; } = "";
    [ObservableProperty]
    public partial ObservableCollection<string> BuildTargets { get; set; } = [];
    [ObservableProperty]
    public partial string? SelectedBuildTarget { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<string> RecentWorkspaces { get; set; } = [];

    private FileNode? _rootNode;
#pragma warning disable IDE0032
    private string? _workspacePath;
#pragma warning restore IDE0032

    public event Action<string, string, FileDisplayMode>? FileOpened;
    public event Func<Task>? FileSaving;

    public MainPageModel()
    {
        foreach (var rw in Settings.RecentWorkspaces)
        {
            RecentWorkspaces.Add(rw);
        }
        _ = CheckToolchainAsync();
    }

    private async Task CheckToolchainAsync()
    {
        await AutoUpdaterService.CheckForUpdatesAsync(msg => Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusText = msg));
        await ToolchainService.InstallMinGWAsync(msg => Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusText = msg));
        if (CurrentState == AppState.Loading)
        {
            if (Program.StartupArgs.Length > 0 && Directory.Exists(Program.StartupArgs[0]))
            {
                WorkspacePath = Path.GetFullPath(Program.StartupArgs[0]);
            }
            else
            {
                CurrentState = AppState.Welcome;
            }
        }
    }

    public string? WorkspacePath
    {
        get => _workspacePath;
        set
        {
            if (SetProperty(ref _workspacePath, value))
            {
                if (!string.IsNullOrEmpty(value))
                {
                    CurrentState = AppState.Editor;
                    OnPropertyChanged(nameof(IsWorkspaceOpen));
                    _ = Settings.RecentWorkspaces.Remove(value);
                    Settings.RecentWorkspaces.Insert(0, value);
                    if (Settings.RecentWorkspaces.Count > 10)
                    {
                        Settings.RecentWorkspaces.RemoveAt(Settings.RecentWorkspaces.Count - 1);
                    }
                    Settings.Save();

                    _ = LoadWorkspaceAsync(value);
                }
                else
                {
                    CurrentState = AppState.Welcome;
                    OnPropertyChanged(nameof(IsWorkspaceOpen));
                }
            }
        }
    }

    private async Task LoadWorkspaceAsync(string path)
    {
        try
        {
            var root = await WorkspaceService.BuildFolderTreeAsync(path);
            _rootNode = root;
            ExplorerTitle = Path.GetFileName(path).ToUpperInvariant();
            RebuildFlatTree();
            StatusText = $"Открыт: {ExplorerTitle}";
            DetectTerminals();
            UpdateBuildProfilesAndTargets(path);
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка загрузки рабочей области: {ex.Message}";
        }
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
    private void OpenRecentWorkspace(string path)
    {
        if (Directory.Exists(path) || File.Exists(path))
        {
            WorkspacePath = path;
        }
        else
        {
            StatusText = $"Путь не найден: {path}";
            _ = Settings.RecentWorkspaces.Remove(path);
            _ = RecentWorkspaces.Remove(path);
            Settings.Save();
        }
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        var path = await WorkspaceService.PickFolderAsync();
        if (!string.IsNullOrEmpty(path))
        {
            WorkspacePath = path;
        }
    }

    [RelayCommand]
    private async Task OpenSolutionAsync()
    {
        var path = await WorkspaceService.PickSolutionAsync();
        if (!string.IsNullOrEmpty(path))
        {
            WorkspacePath = path;
        }
    }

    [RelayCommand]
    private async Task CreateCppProjectAsync()
    {
        if (CurrentState == AppState.Loading)
        {
            return;
        }
        var desktop = Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
        if (desktop?.MainWindow == null)
        {
            return;
        }

        var dialog = new Views.CreateCppProjectDialog();
        var result = await dialog.ShowDialog<Views.CppProjectResult>(desktop.MainWindow);

        if (result != null)
        {
            try
            {
                _ = Directory.CreateDirectory(result.FullPath);
                var srcPath = Path.Combine(result.FullPath, "src");
                _ = Directory.CreateDirectory(srcPath);
                var mainFile = result.IsCpp ? "main.cpp" : "main.c";
                var mainContent = result.IsCpp
                    ? "#include <iostream>\n\nint main() {\n    std::cout << \"Hello, CIDE!\" << std::endl;\n    return 0;\n}\n"
                    : "#include <stdio.h>\n\nint main() {\n    printf(\"Hello, CIDE!\\n\");\n    return 0;\n}\n";
                await File.WriteAllTextAsync(Path.Combine(srcPath, mainFile), mainContent);
                var compiler = result.IsCpp ? "g++" : "gcc";
                var makefileContent = $"CC={compiler}\nCFLAGS=-Wall\n\nall: run\n\nbuild:\n\t$(CC) $(CFLAGS) src/{mainFile} -o bin/{result.Name}\n\nrun: build\n\t./bin/{result.Name}\n";
                await File.WriteAllTextAsync(Path.Combine(result.FullPath, "Makefile"), makefileContent);
                _ = Directory.CreateDirectory(Path.Combine(result.FullPath, "bin"));
                WorkspacePath = result.FullPath;
            }
            catch (Exception ex)
            {
                StatusText = $"Ошибка создания проекта: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private async Task CreateCsharpProjectAsync()
    {
        if (CurrentState == AppState.Loading)
        {
            return;
        }
        var desktop = Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
        if (desktop?.MainWindow == null)
        {
            return;
        }

        var dialog = new Views.CreateCsharpProjectDialog();
        var result = await dialog.ShowDialog<Views.CsharpProjectResult>(desktop.MainWindow);

        if (result != null)
        {
            try
            {
                StatusText = "Создание проекта C#...";
                var slnDir = Path.Combine(result.BasePath, result.SolutionName);
                if (!result.SameFolder)
                {
                    _ = Directory.CreateDirectory(slnDir);
                }
                else
                {
                    slnDir = Path.Combine(result.BasePath, result.ProjectName);
                    _ = Directory.CreateDirectory(slnDir);
                }

                var tcs = new TaskCompletionSource();
                var processArgs = result.SameFolder
                    ? $"new {result.TemplateShortName} -n {result.ProjectName} -o \"{slnDir}\""
                    : $"new {result.TemplateShortName} -n {result.ProjectName} -o \"{Path.Combine(slnDir, result.ProjectName)}\"";

                var createProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = processArgs,
                        WorkingDirectory = result.BasePath,
                        CreateNoWindow = true
                    },
                    EnableRaisingEvents = true
                };

                createProcess.Exited += (s, e) => tcs.SetResult();
                _ = createProcess.Start();
                await tcs.Task;
                if (!result.SameFolder)
                {
                    var slnTcs = new TaskCompletionSource();
                    var slnProcess = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "dotnet",
                            Arguments = $"new sln -n {result.SolutionName} -f slnx",
                            WorkingDirectory = slnDir,
                            CreateNoWindow = true
                        },
                        EnableRaisingEvents = true
                    };
                    slnProcess.Exited += (s, e) => slnTcs.SetResult();
                    _ = slnProcess.Start();
                    await slnTcs.Task;

                    var addTcs = new TaskCompletionSource();
                    var addProcess = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "dotnet",
                            Arguments = $"sln add \"{Path.Combine(result.ProjectName, result.ProjectName + ".csproj")}\"",
                            WorkingDirectory = slnDir,
                            CreateNoWindow = true
                        },
                        EnableRaisingEvents = true
                    };
                    addProcess.Exited += (s, e) => addTcs.SetResult();
                    _ = addProcess.Start();
                    await addTcs.Task;
                }
                WorkspacePath = slnDir;
            }
            catch (Exception ex)
            {
                StatusText = $"Ошибка: {ex.Message}";
            }
        }
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
            await WorkspaceService.FillChildrenAsync(node, node.FullPath, node.Depth + 1);
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
    private async Task OpenFileAsync(string path)
    {
        var existing = Tabs.FirstOrDefault(t => t.FilePath == path);
        if (existing != null)
        {
            await ActivateTabAsync(existing);
            return;
        }

        var mode = FileTypeHelper.GetDisplayMode(path);
        if (mode == FileDisplayMode.Executable)
        {
            StatusText = $"Запуск: {Path.GetFileName(path)}";
            try
            {
                _ = System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
                await Task.Delay(600);
            }
            catch (Exception ex)
            {
                StatusText = $"Ошибка запуска: {ex.Message}";
                return;
            }
            StatusText = "Готов";
            return;
        }

        StatusText = $"Открытие: {Path.GetFileName(path)}";

        var content = mode == FileDisplayMode.Binary ? string.Empty : await WorkspaceService.ReadFileAsync(path);

        var tab = new EditorTab { FilePath = path, Content = content, DisplayMode = mode };
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
            if (Settings.AutoSave && ActiveTab.IsModified)
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
        Breadcrumbs = tab.FilePath
            .Replace(WorkspacePath ?? "", "")
            .TrimStart('\\', '/')
            .Replace("\\", " > ")
            .Replace("/", " > ");
        IsEditorVisible = true;

        var monacoLang = FileTypeHelper.GetMonacoLanguage(tab.FilePath);
        FileOpened?.Invoke(tab.Content, monacoLang, tab.DisplayMode);
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
        if (ActiveTab is null || ActiveTab.DisplayMode != FileDisplayMode.Text)
        {
            return;
        }

        if (FileSaving != null)
        {
            await FileSaving.Invoke();
        }

        await WorkspaceService.SaveFileAsync(ActiveTab.FilePath, ActiveTab.Content);
        ActiveTab.IsModified = false;
        StatusText = "✅ Сохранено";
    }
    [RelayCommand]
    internal static Task GoBackAsync() => Task.CompletedTask;
    [RelayCommand]
    internal void ToggleOutput() => IsOutputVisible = !IsOutputVisible;
    [RelayCommand]
    internal async Task RunCommandAsync()
    {
        IsOutputVisible = true;
        CompilerOutput = "";
        await SaveActiveFileAsync();
        await CompileService.RunCompilationAsync(
            SelectedBuildProfile,
            ActiveTab?.FilePath ?? "",
            WorkspacePath ?? "",
            output => Avalonia.Threading.Dispatcher.UIThread.Post(() => CompilerOutput += output));
    }

    [RelayCommand]
    internal async Task BuildCommandAsync() => await RunCommandAsync();
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

        var hasCSharp = dir.GetFiles("*.csproj", SearchOption.AllDirectories).Length > 0
                       || dir.GetFiles("*.slnx", SearchOption.AllDirectories).Length > 0;
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

            foreach (var f in dir.GetFiles("*.*", SearchOption.AllDirectories)
                                  .Where(f => f.Extension is ".c" or ".cpp"))
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

#pragma warning disable CA1822
    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var desktop = Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
        if (desktop?.MainWindow == null)
        {
            return;
        }

        var dialog = new Views.SettingsDialog();
        await dialog.ShowDialog(desktop.MainWindow);
    }
#pragma warning restore CA1822
}
