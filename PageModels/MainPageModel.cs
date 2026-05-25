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
    public partial FileNode? SelectedNode { get; set; }
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
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string BusyText { get; set; } = "";

    [ObservableProperty]
    public partial string ProjectTypeDisplay { get; set; } = "ПРОЕКТ";

    [ObservableProperty]
    public partial bool IsZenMode { get; set; }

    [ObservableProperty]
    public partial bool IsWslWorkspace { get; set; }

    [ObservableProperty]
    public partial string RemoteStatusText { get; set; } = "Локально";

    [ObservableProperty]
    public partial string RemoteStatusColor { get; set; } = "#9061F9";

    [ObservableProperty]
    public partial bool IsOutputVisible { get; set; }

    [ObservableProperty]
    public partial string CursorPosition { get; set; } = "Ln 1, Col 1";
    [ObservableProperty]
    public partial ObservableCollection<string> BuildProfiles { get; set; } = [];
    [ObservableProperty]
    public partial string SelectedBuildProfile { get; set; } = "Один файл (C/C++)";
    [ObservableProperty]
    public partial bool IsSidebarVisible { get; set; } = true;
    [ObservableProperty]
    public partial string CompilerOutput { get; set; } = "";
    [ObservableProperty]
    public partial ObservableCollection<BuildError> CompilerErrors { get; set; } = [];
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

    public ObservableCollection<WorkspaceRoot> WorkspaceRoots { get; } = [];
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
        try
        {
            await AutoUpdaterService.CheckForUpdatesAsync(msg => Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusText = msg));
            await ToolchainService.InstallMinGWAsync(msg => Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusText = msg));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Toolchain error: {ex}");
            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusText = "Ошибка загрузки: " + ex.Message);
            await Task.Delay(2000);
        }
        finally
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (CurrentState == AppState.Loading)
                {
                    if (Program.StartupArgs != null && Program.StartupArgs.Length > 0)
                    {
                        IsWslWorkspace = Program.StartupArgs.Contains("--wsl");

                        var pathArg = Program.StartupArgs[0];
                        if (pathArg != "--wsl" && Directory.Exists(pathArg))
                        {
                            WorkspacePath = Path.GetFullPath(pathArg);
                        }
                        else
                        {
                            CurrentState = AppState.Welcome;
                        }
                    }
                    else
                    {
                        CurrentState = AppState.Welcome;
                    }
                }
            });
        }
    }

    public bool IsSshWorkspace => WorkspacePath?.StartsWith("sftp://") == true || WorkspacePath?.StartsWith("ssh://") == true;

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

                    if (value.StartsWith(@"\\wsl$\") || value.StartsWith(@"\\wsl.localhost\"))
                    {
                        IsWslWorkspace = true;
                    }

                    if (IsWslWorkspace)
                    {
                        var distro = "WSL";
                        var parts = value.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            distro = $"WSL: {parts[1]}";
                        }

                        RemoteStatusText = distro;
                        RemoteStatusColor = "#0078D7";
                    }
                    else if (IsSshWorkspace)
                    {
                        RemoteStatusText = "SSH";
                        RemoteStatusColor = "#FF9800";
                    }
                    else
                    {
                        RemoteStatusText = "Локально";
                        RemoteStatusColor = "#9061F9";
                    }

                    OnPropertyChanged(nameof(IsWorkspaceOpen));
                    OnPropertyChanged(nameof(IsSshWorkspace));
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
                    OnPropertyChanged(nameof(IsSshWorkspace));
                }
            }
        }
    }

    private async Task LoadWorkspaceAsync(string path)
    {
        try
        {
            if (File.Exists(path) && !path.EndsWith(".cide-workspace"))
            {
                IsSidebarVisible = false;
                ExplorerTitle = Path.GetFileName(path).ToUpperInvariant();
                foreach (var r in WorkspaceRoots)
                {
                    if (r.Provider is IDisposable disp)
                    {
                        disp.Dispose();
                    }
                }
                WorkspaceRoots.Clear();
                FlatTree.Clear();
                StatusText = $"Открыт: {ExplorerTitle}";
                await OpenFileAsync(path);
                return;
            }

            IsSidebarVisible = true;
            foreach (var r in WorkspaceRoots)
            {
                if (r.Provider is IDisposable disp)
                {
                    disp.Dispose();
                }
            }
            WorkspaceRoots.Clear();

            var workspaceFile = path;
            if (Directory.Exists(path))
            {
                var possibleConfig = Path.Combine(path, ".cide-workspace");
                if (File.Exists(possibleConfig))
                {
                    workspaceFile = possibleConfig;
                }
            }

            if (File.Exists(workspaceFile) && workspaceFile.EndsWith(".cide-workspace"))
            {
                var json = await File.ReadAllTextAsync(workspaceFile);
                var config = System.Text.Json.JsonSerializer.Deserialize<WorkspaceConfig>(json);
                if (config != null)
                {
                    foreach (var rc in config.Roots)
                    {
                        if (rc.Type == "Local")
                        {
                            var provider = new LocalFileSystemProvider();
                            var wsRoot = new WorkspaceRoot { Name = rc.Name, Provider = provider };
                            var rootNode = await WorkspaceService.BuildFolderTreeAsync(wsRoot, rc.Path, rc.Name, FileNodeKind.WorkspaceRoot);
                            wsRoot.RootNode = rootNode;
                            WorkspaceRoots.Add(wsRoot);
                        }
                        else if (rc.Type == "SSH")
                        {
                            try
                            {
                                var info = new SshConnectionInfo { Host = rc.Host ?? "", Username = rc.Username ?? "", Password = rc.Password ?? "", RootPath = rc.Path };
                                var provider = new SshFileSystemProvider(info);
                                await Task.Run(provider.Connect);
                                var wsRoot = new WorkspaceRoot { Name = rc.Name, Provider = provider };
                                var rootNode = await WorkspaceService.BuildFolderTreeAsync(wsRoot, rc.Path, rc.Name, FileNodeKind.WorkspaceRoot);
                                wsRoot.RootNode = rootNode;
                                WorkspaceRoots.Add(wsRoot);
                            }
                            catch (Exception ex)
                            {
                                Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusText = $"Ошибка подключения {rc.Name}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            else
            {
                var provider = new LocalFileSystemProvider();
                var wsRoot = new WorkspaceRoot
                {
                    Name = "💻 Локально (Client)",
                    Provider = provider
                };
                var rootNode = await WorkspaceService.BuildFolderTreeAsync(wsRoot, path, "💻 Локально (Client)", FileNodeKind.WorkspaceRoot);
                wsRoot.RootNode = rootNode;
                WorkspaceRoots.Add(wsRoot);
            }

            ExplorerTitle = "ГЛОБАЛЬНЫЙ WORKSPACE";
            RebuildFlatTree();
            StatusText = $"Открыт workspace";
            DetectTerminals();
            DetectProjectType(path);
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка загрузки рабочей области: {ex.Message}";
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
    private async Task CloneRepositoryAsync()
    {
        var desktop = Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
        if (desktop?.MainWindow == null)
        {
            return;
        }

        var urlDialog = new Views.InputDialog("Клон репозитория", "Введите URL Git-репозитория:");
        var url = await urlDialog.ShowDialog<string>(desktop.MainWindow);
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
        if (topLevel == null)
        {
            return;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "Выберите папку для клонирования"
        });

        var folder = (folders != null && folders.Count > 0) ? folders[0].Path.LocalPath : null;
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        try
        {
            StatusText = "Клонирование репозитория...";
            CurrentState = AppState.Loading;

            await GitService.CloneRepositoryAsync(url, folder, msg => Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusText = $"Git: {msg}"));

            StatusText = "Клонирование завершено!";
            await LoadWorkspaceAsync(folder);
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка клонирования: {ex.Message}";
            CurrentState = AppState.Welcome;
        }
    }

    [RelayCommand]
    private async Task ConnectSshAsync()
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

        var dialog = new Views.SshConnectionDialog();
        var info = await dialog.ShowDialog<SshConnectionInfo>(desktop.MainWindow);

        if (info != null)
        {
            try
            {
                StatusText = "Подключение к SSH (может занять время)...";
                var provider = new SshFileSystemProvider(info);
                await Task.Run(provider.Connect);

                var wsRoot = new WorkspaceRoot
                {
                    Name = $"☁️ Сервер (SSH) {info.Host}",
                    Provider = provider
                };
                var rootNode = await WorkspaceService.BuildFolderTreeAsync(wsRoot, info.RootPath, $"☁️ Сервер (SSH) {info.Host}", FileNodeKind.WorkspaceRoot);
                wsRoot.RootNode = rootNode;

                WorkspaceRoots.Add(wsRoot);

                WorkspacePath = info.RootPath; // for legacy compat
                CurrentState = AppState.Editor;
                IsSidebarVisible = true;
                ExplorerTitle = "ГЛОБАЛЬНЫЙ WORKSPACE";
                RebuildFlatTree();
                StatusText = $"SSH подключен: {info.Host}";
            }
            catch (Exception ex)
            {
                StatusText = $"Ошибка SSH: {ex.Message}";
            }
        }
    }

#pragma warning disable CA1822
    [RelayCommand]
    private void ShowServerLoad()
    {
        var window = new Views.ServerLoadWindow();
        window.Show();
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
            await OpenFileAsync(node.FullPath, node.Root);
        }
        else
        {
            await ToggleNodeAsync(node);
        }
    }
    private void RebuildFlatTree()
    {
        FlatTree.Clear();
        foreach (var root in WorkspaceRoots)
        {
            AddFlatNodes(root.RootNode);
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

    private async Task ToggleNodeAsync(FileNode node)
    {
        if (!node.IsPopulated)
        {
            if (node.Root != null)
            {
                await WorkspaceService.FillChildrenAsync(node.Root, node, node.FullPath, node.Depth + 1);
            }
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
            while (index + 1 < FlatTree.Count && FlatTree[index + 1].Depth > node.Depth)
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

    private async Task OpenFileAsync(string path, WorkspaceRoot? root = null)
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

        try
        {
            var provider = root?.Provider ?? new LocalFileSystemProvider();
            var content = mode == FileDisplayMode.Binary ? string.Empty : await WorkspaceService.ReadFileAsync(provider, path);

            var tab = new EditorTab { Root = root, FilePath = path, Content = content, DisplayMode = mode };
            Tabs.Add(tab);
            await ActivateTabAsync(tab);
            StatusText = "Готов";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка открытия файла: {ex.Message}";
        }
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
                var provider = ActiveTab.Root?.Provider ?? new LocalFileSystemProvider();
                await WorkspaceService.SaveFileAsync(provider, ActiveTab.FilePath, ActiveTab.Content);
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
        if (tab == null)
        {
            return;
        }

        if (tab.IsModified)
        {
            if (Settings.AutoSave)
            {
                if (FileSaving != null)
                {
                    await FileSaving.Invoke();
                }

                var provider = tab.Root?.Provider ?? new LocalFileSystemProvider();
                await WorkspaceService.SaveFileAsync(provider, tab.FilePath, tab.Content);
            }
            else
            {
                var desktop = Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
                if (desktop?.MainWindow != null)
                {
                    var dialog = new Views.ConfirmDialog("Сохранение", $"Файл '{tab.FileName}' изменен.\nСохранить изменения перед закрытием?");
                    var shouldSave = await dialog.ShowDialog<bool>(desktop.MainWindow);
                    if (shouldSave)
                    {
                        if (FileSaving != null)
                        {
                            await FileSaving.Invoke();
                        }

                        var provider = tab.Root?.Provider ?? new LocalFileSystemProvider();
                        await WorkspaceService.SaveFileAsync(provider, tab.FilePath, tab.Content);
                    }
                }
            }
        }
        _ = Tabs.Remove(tab);
        if (Tabs.Count == 0)
        {
            ActiveTab = null;
            IsEditorVisible = false;
            Breadcrumbs = "";
            CursorPosition = "Ln 1, Col 1";
        }
        else
        {
            await ActivateTabAsync(Tabs.Last());
        }
        StatusText = "Готов";
    }

    [RelayCommand]
    private async Task SaveActiveFileAsync()
    {
        if (ActiveTab is null || ActiveTab.DisplayMode != FileDisplayMode.Text)
        {
            return;
        }

        try
        {
            if (FileSaving != null)
            {
                await FileSaving.Invoke();
            }

            var provider = ActiveTab.Root?.Provider ?? new LocalFileSystemProvider();
            await WorkspaceService.SaveFileAsync(provider, ActiveTab.FilePath, ActiveTab.Content);

            ActiveTab.IsModified = false;
            StatusText = $"✅ Сохранено ({DateTime.Now:HH:mm:ss})";

            _ = CrossCheckService.AnalyzeAsync(this);
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Ошибка сохранения: {ex.Message}";
        }
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

        var targetFile = ActiveTab?.FilePath ?? "";
        await CompileService.RunCompilationAsync(
            ProjectTypeDisplay,
            targetFile,
            WorkspacePath ?? "",
            output => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                CompilerOutput += output;
                if (CompilerOutput.Length > 20000)
                {
                    CompilerOutput = string.Concat("...", CompilerOutput.AsSpan(CompilerOutput.Length - 19997));
                }
            }));

        ParseCompilerErrors();
    }

    [RelayCommand]
    internal void StopCommand()
    {
        CompileService.KillCurrentProcess();
        StatusText = "Процесс принудительно остановлен";
    }
    [RelayCommand]
    private async Task CreateFileNodeAsync()
    {
        var parent = SelectedNode ?? WorkspaceRoots.FirstOrDefault()?.RootNode;
        if (parent == null || WorkspacePath == null)
        {
            return;
        }

        var targetDir = parent.Kind is FileNodeKind.Folder or FileNodeKind.Project or FileNodeKind.Solution
            ? parent.FullPath
            : Path.GetDirectoryName(parent.FullPath);

        if (targetDir == null)
        {
            return;
        }

        var desktop = Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
        if (desktop?.MainWindow == null)
        {
            return;
        }

        var dialog = new Views.InputDialog("Новый файл", "Введите имя файла:");
        var result = await dialog.ShowDialog<string?>(desktop.MainWindow);

        if (!string.IsNullOrWhiteSpace(result))
        {
            var newPath = Path.Combine(targetDir, result);
            try
            {
                var provider = parent.Root?.Provider ?? new LocalFileSystemProvider();
                await WorkspaceService.CreateFileAsync(provider, newPath);
                StatusText = $"✅ Файл {result} создан";
                await LoadWorkspaceAsync(WorkspacePath);
            }
            catch (Exception ex)
            {
                StatusText = $"❌ Ошибка: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private async Task CreateFolderNodeAsync()
    {
        var parent = SelectedNode ?? WorkspaceRoots.FirstOrDefault()?.RootNode;
        if (parent == null || WorkspacePath == null)
        {
            return;
        }

        var targetDir = parent.Kind is FileNodeKind.Folder or FileNodeKind.Project or FileNodeKind.Solution
            ? parent.FullPath
            : Path.GetDirectoryName(parent.FullPath);

        if (targetDir == null)
        {
            return;
        }

        var desktop = Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
        if (desktop?.MainWindow == null)
        {
            return;
        }

        var dialog = new Views.InputDialog("Новая папка", "Введите имя папки:");
        var result = await dialog.ShowDialog<string?>(desktop.MainWindow);

        if (!string.IsNullOrWhiteSpace(result))
        {
            var newPath = Path.Combine(targetDir, result);
            try
            {
                var provider = parent.Root?.Provider ?? new LocalFileSystemProvider();
                await WorkspaceService.CreateDirectoryAsync(provider, newPath);
                StatusText = $"✅ Папка {result} создана";
                await LoadWorkspaceAsync(WorkspacePath);
            }
            catch (Exception ex)
            {
                StatusText = $"❌ Ошибка: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private async Task RenameNodeAsync()
    {
        var node = SelectedNode;
        if (node == null || node.Kind == FileNodeKind.Solution || node.Kind == FileNodeKind.Project || WorkspacePath == null)
        {
            return;
        }

        var desktop = Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
        if (desktop?.MainWindow == null)
        {
            return;
        }

        var dialog = new Views.InputDialog("Переименование", "Введите новое имя:", node.Name);
        var result = await dialog.ShowDialog<string?>(desktop.MainWindow);

        if (!string.IsNullOrWhiteSpace(result) && result != node.Name)
        {
            var dir = Path.GetDirectoryName(node.FullPath);
            if (dir == null)
            {
                return;
            }

            var newPath = Path.Combine(dir, result);
            try
            {
                var provider = node.Root?.Provider ?? new LocalFileSystemProvider();
                await WorkspaceService.RenameAsync(provider, node.FullPath, newPath);
                var openTab = Tabs.FirstOrDefault(t => t.FilePath == node.FullPath);
                if (openTab != null)
                {
                    openTab.FilePath = newPath;
                    OnPropertyChanged(nameof(openTab.DisplayName));
                }

                StatusText = $"✅ {node.Name} переименован в {result}";
                await LoadWorkspaceAsync(WorkspacePath);
            }
            catch (Exception ex)
            {
                StatusText = $"❌ Ошибка: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private async Task DeleteNodeAsync()
    {
        var node = SelectedNode;
        if (node == null || node.Kind == FileNodeKind.Solution || node.Kind == FileNodeKind.Project || WorkspacePath == null)
        {
            return;
        }

        var desktop = Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
        if (desktop?.MainWindow == null)
        {
            return;
        }

        var dialog = new Views.ConfirmDialog("Удаление", $"Вы уверены, что хотите удалить '{node.Name}'?");
        var result = await dialog.ShowDialog<bool>(desktop.MainWindow);

        if (result)
        {
            try
            {
                var provider = node.Root?.Provider ?? new LocalFileSystemProvider();
                await WorkspaceService.DeleteAsync(provider, node.FullPath);
                var openTab = Tabs.FirstOrDefault(t => t.FilePath == node.FullPath);
                if (openTab != null)
                {
                    await CloseTabAsync(openTab);
                }

                StatusText = $"✅ {node.Name} удален";
                await LoadWorkspaceAsync(WorkspacePath);
            }
            catch (Exception ex)
            {
                StatusText = $"❌ Ошибка: {ex.Message}";
            }
        }
    }

    private void ParseCompilerErrors()
    {
        CompilerErrors.Clear();
        var lines = CompilerOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            try
            {
                var l = line.Trim();
                if (l.Contains(": error") || l.Contains(": warning") || l.Contains("error CS") || l.Contains("warning CS"))
                {
                    var severity = l.Contains("error") ? BuildErrorSeverity.Error : BuildErrorSeverity.Warning;
                    var file = "Unknown";
                    var lineNum = 0;
                    var message = l;
                    var colonIdx = l.IndexOf(':');
                    if (colonIdx > 0)
                    {
                        var prefix = l[..colonIdx];
                        if (prefix.Contains('('))
                        {
                            var parenIdx = prefix.IndexOf('(');
                            file = prefix[..parenIdx];
                            var numStr = prefix.Substring(parenIdx + 1, prefix.IndexOf(',') > 0 ? prefix.IndexOf(',') - parenIdx - 1 : prefix.IndexOf(')') - parenIdx - 1);
                            _ = int.TryParse(numStr, out lineNum);
                        }
                        else
                        {
                            var parts = prefix.Split(':');
                            file = parts[0];
                            if (parts.Length > 1)
                            {
                                _ = int.TryParse(parts[1], out lineNum);
                            }
                        }
                    }

                    var msgParts = l.Split("error", 2);
                    if (msgParts.Length < 2)
                    {
                        msgParts = l.Split("warning", 2);
                    }

                    if (msgParts.Length == 2)
                    {
                        message = msgParts[1].TrimStart(':', ' ', 's');
                        if (message.StartsWith("CS"))
                        {
                        }
                    }

                    CompilerErrors.Add(new BuildError
                    {
                        Severity = severity,
                        File = Path.GetFileName(file),
                        Line = lineNum,
                        Message = message.Trim()
                    });
                }
            }
            catch { }
        }

        if (CompilerErrors.Count == 0)
        {
            CompilerErrors.Add(new BuildError { Severity = BuildErrorSeverity.Info, Message = "Сборка завершена (ошибок нет)" });
        }
    }

    [RelayCommand]
    internal async Task BuildCommandAsync() => await RunCommandAsync();

    [RelayCommand]
    private async Task FormatCodeAsync()
    {
        if (ActiveTab == null || string.IsNullOrEmpty(ActiveTab.FilePath) || string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        IsBusy = true;
        BusyText = "Запуск движка форматирования...";
        try
        {
            var ext = Path.GetExtension(ActiveTab.FilePath).ToLowerInvariant();
            var formatted = false;
            if (ext == ".cs")
            {
                await SaveActiveFileAsync();
                StatusText = "Форматирование C#...";
                var proc = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"format \"{WorkspacePath}\" --include \"{ActiveTab.FilePath}\" --severity warn",
                        WorkingDirectory = WorkspacePath,
                        CreateNoWindow = true
                    }
                };
                _ = proc.Start();
                await proc.WaitForExitAsync();
                ActiveTab.Content = await File.ReadAllTextAsync(ActiveTab.FilePath);
                formatted = true;
            }
            else if (ext is ".c" or ".cpp" or ".h" or ".hpp")
            {
                if (!Settings.UseLocalClangFormat)
                {
                    StatusText = "Локальный clang-format отключен в настройках";
                    return;
                }

                await SaveActiveFileAsync();
                StatusText = "Форматирование C/C++...";
                BusyText = "Очистка кода (clang-format)...";
                var clangPath = string.IsNullOrWhiteSpace(Settings.ClangFormatPath) ? "clang-format" : Settings.ClangFormatPath;
                var proc = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = clangPath,
                        Arguments = $"-i \"{ActiveTab.FilePath}\"",
                        WorkingDirectory = WorkspacePath,
                        CreateNoWindow = true
                    }
                };
                try
                {
                    _ = proc.Start();
                    try
                    { proc.PriorityClass = System.Diagnostics.ProcessPriorityClass.BelowNormal; }
                    catch { }
                    await proc.WaitForExitAsync();
                    ActiveTab.Content = await File.ReadAllTextAsync(ActiveTab.FilePath);
                    formatted = true;
                }
                catch (Exception ex)
                {
                    StatusText = $"Ошибка clang-format: {ex.Message}";
                }
            }

            if (formatted)
            {
                var temp = ActiveTab;
                ActiveTab = null;
                ActiveTab = temp;
                StatusText = "Готов";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void RemoveComments()
    {
        if (ActiveTab == null || string.IsNullOrEmpty(ActiveTab.Content))
        {
            return;
        }
        var text = ActiveTab.Content;
        var blockComments = @"/\*[\s\S]*?\*/";
        var lineComments = @"//.*";
        var strings = @"""(?:\\.|[^""])*""";
        var verbatimStrings = @"@""(?:""""|[^""])*""";

        var pattern = $"{blockComments}|{lineComments}|{strings}|{verbatimStrings}";
        ActiveTab.Content = System.Text.RegularExpressions.Regex.Replace(text, pattern, me => me.Value.StartsWith("/*") || me.Value.StartsWith("//") ? "" : me.Value);
        ActiveTab.IsModified = true;
        var temp = ActiveTab;
        ActiveTab = null;
        ActiveTab = temp;
    }
    private void DetectProjectType(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (File.Exists(path))
        {
            ProjectTypeDisplay = path.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".hpp", StringComparison.OrdinalIgnoreCase) ? "C++ Project" : "C Project";
            return;
        }

        if (Directory.Exists(path))
        {
            var dir = new DirectoryInfo(path);
            var hasMakefile = dir.GetFiles("Makefile", SearchOption.TopDirectoryOnly).Length > 0;
            var cppCount = dir.GetFiles("*.cpp", SearchOption.AllDirectories).Length;
            var cCount = dir.GetFiles("*.c", SearchOption.AllDirectories).Length;

            ProjectTypeDisplay = hasMakefile ? "Makefile" : cppCount > cCount ? "C++ Project" : cCount > 0 ? "C Project" : "Unknown Project";
        }
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

    partial void OnSelectedBuildProfileChanged(string value)
    {
        if (string.IsNullOrEmpty(WorkspacePath))
            return;
        var dir = new DirectoryInfo(WorkspacePath);
        BuildTargets.Clear();

        if (value == "Проект C#")
        {
            BuildTargets.Add("Основной проект");
        }
        else if (value == "Makefile")
        {
            BuildTargets.Add("all");
        }
        else if (value == "C")
        {
            foreach (var f in dir.GetFiles("*.c", SearchOption.AllDirectories))
                BuildTargets.Add(f.Name);
        }
        else if (value == "C++")
        {
            foreach (var f in dir.GetFiles("*.cpp", SearchOption.AllDirectories))
                BuildTargets.Add(f.Name);
        }
        else if (value == "Python")
        {
            foreach (var f in dir.GetFiles("*.py", SearchOption.AllDirectories))
                BuildTargets.Add(f.Name);
        }
        else if (value == "Bash")
        {
            foreach (var f in dir.GetFiles("*.sh", SearchOption.AllDirectories))
                BuildTargets.Add(f.Name);
        }

        if (BuildTargets.Count > 0)
            SelectedBuildTarget = BuildTargets[0];
        else
            SelectedBuildTarget = null;
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
