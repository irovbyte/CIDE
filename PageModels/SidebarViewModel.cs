using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CIDE.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CIDE.PageModels;

public partial class SidebarViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string ExplorerTitle { get; set; } = "ОБОЗРЕВАТЕЛЬ";

    [ObservableProperty]
    public partial FileNode? SelectedNode { get; set; }

    public ObservableCollection<WorkspaceRoot> WorkspaceRoots { get; } = [];
    public event Func<FileNode, Task>? NodeSelected;
    public event Action<string>? FileDeletedEvent;

    private readonly Dictionary<WorkspaceRoot, FileSystemWatcher> _watchers = [];

    public SidebarViewModel() => WorkspaceRoots.CollectionChanged += WorkspaceRoots_CollectionChanged;

    private void WorkspaceRoots_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (WorkspaceRoot oldRoot in e.OldItems)
            {
                if (_watchers.Remove(oldRoot, out var watcher))
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
            }
        }
        if (e.NewItems != null)
        {
            foreach (WorkspaceRoot newRoot in e.NewItems)
            {
                if (newRoot.Provider is LocalFileSystemProvider && Directory.Exists(newRoot.RootNode.FullPath))
                {
                    var watcher = new FileSystemWatcher(newRoot.RootNode.FullPath)
                    {
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = true,
                        NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite
                    };
                    watcher.Created += Watcher_Changed;
                    watcher.Deleted += Watcher_Changed;
                    watcher.Renamed += Watcher_Changed;
                    _watchers[newRoot] = watcher;
                }
            }
        }
    }

    private void Watcher_Changed(object sender, FileSystemEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            var watcher = (FileSystemWatcher)sender;
            var root = _watchers.FirstOrDefault(x => x.Value == watcher).Key;
            if (root == null)
            {
                return;
            }

            if (e.ChangeType == WatcherChangeTypes.Deleted)
            {
                FileDeletedEvent?.Invoke(e.FullPath);
            }

            var dirPath = Path.GetDirectoryName(e.FullPath);
            if (string.IsNullOrEmpty(dirPath))
            {
                return;
            }

            var parentNode = FindNodeByPath(root.RootNode, dirPath);
            if (parentNode != null && parentNode.IsExpanded && parentNode.IsPopulated)
            {
                await WorkspaceService.FillChildrenAsync(root, parentNode, dirPath, parentNode.Depth);
                if (root.Provider is LocalFileSystemProvider)
                {
                    var statusMap = await GitService.GetGitStatusAsync(root.RootNode.FullPath);
                    if (statusMap != null)
                    {
                        ApplyGitStatus(parentNode, statusMap);
                    }
                }
            }
        });
    }

    private static FileNode? FindNodeByPath(FileNode node, string path)
    {
        if (node.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase))
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            if (child.Kind != FileNodeKind.File)
            {
                var found = FindNodeByPath(child, path);
                if (found != null)
                {
                    return found;
                }
            }
        }
        return null;
    }

    public async Task UpdateGitStatusAsync()
    {
        foreach (var root in WorkspaceRoots)
        {
            if (root.Provider is LocalFileSystemProvider)
            {
                var statusMap = await GitService.GetGitStatusAsync(root.RootNode.FullPath);
                if (statusMap != null)
                {
                    ApplyGitStatus(root.RootNode, statusMap);
                }
            }
        }
    }

    private static void ApplyGitStatus(FileNode node, Dictionary<string, string> statusMap)
    {
        node.GitStatus = statusMap.TryGetValue(node.FullPath, out var status) ? status : "";

        foreach (var child in node.Children)
        {
            ApplyGitStatus(child, statusMap);
        }
    }

    [RelayCommand]
    private async Task SelectNodeAsync(FileNode? node)
    {
        SelectedNode = node;
        if (node != null && NodeSelected != null)
        {
            await NodeSelected.Invoke(node);
        }
    }

    [RelayCommand]
    private async Task CreateFileNodeAsync()
    {
        var parent = SelectedNode ?? WorkspaceRoots.FirstOrDefault()?.RootNode;
        if (parent == null)
        {
            return;
        }

        var targetDir = parent.Kind is FileNodeKind.Folder or FileNodeKind.Project or FileNodeKind.Solution or FileNodeKind.WorkspaceRoot
            ? parent.FullPath
            : Path.GetDirectoryName(parent.FullPath);

        if (targetDir == null)
        {
            return;
        }

        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desktop?.MainWindow == null)
        {
            return;
        }

        var dialog = new Views.InputDialog("Новый файл", "Введите имя файла:");
        var result = await dialog.ShowDialog<string?>(desktop.MainWindow);

        if (!string.IsNullOrWhiteSpace(result))
        {
            try
            {
                var newPath = Path.Combine(targetDir, result);
                if (!File.Exists(newPath))
                {
                    File.Create(newPath).Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка создания файла: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private async Task CreateFolderNodeAsync()
    {
        var parent = SelectedNode ?? WorkspaceRoots.FirstOrDefault()?.RootNode;
        if (parent == null)
        {
            return;
        }

        var targetDir = parent.Kind is FileNodeKind.Folder or FileNodeKind.Project or FileNodeKind.Solution or FileNodeKind.WorkspaceRoot
            ? parent.FullPath
            : Path.GetDirectoryName(parent.FullPath);

        if (targetDir == null)
        {
            return;
        }

        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desktop?.MainWindow == null)
        {
            return;
        }

        var dialog = new Views.InputDialog("Новая папка", "Введите имя папки:");
        var result = await dialog.ShowDialog<string?>(desktop.MainWindow);

        if (!string.IsNullOrWhiteSpace(result))
        {
            try
            {
                var newPath = Path.Combine(targetDir, result);
                if (!Directory.Exists(newPath))
                {
                    _ = Directory.CreateDirectory(newPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка создания папки: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private async Task RenameNodeAsync()
    {
        var node = SelectedNode;
        if (node == null || node.Kind == FileNodeKind.Solution || node.Kind == FileNodeKind.Project || node.Kind == FileNodeKind.WorkspaceRoot)
        {
            return;
        }

        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desktop?.MainWindow == null)
        {
            return;
        }

        var dialog = new Views.InputDialog("Переименовать", "Введите новое имя:", node.Name);
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
                if (node.Kind == FileNodeKind.Folder)
                {
                    Directory.Move(node.FullPath, newPath);
                }
                else
                {
                    File.Move(node.FullPath, newPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка переименования: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private async Task DeleteNodeAsync()
    {
        var node = SelectedNode;
        if (node == null || node.Kind == FileNodeKind.Solution || node.Kind == FileNodeKind.Project || node.Kind == FileNodeKind.WorkspaceRoot)
        {
            return;
        }

        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
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
                if (node.Kind == FileNodeKind.Folder)
                {
                    Directory.Delete(node.FullPath, true);
                }
                else
                {
                    File.Delete(node.FullPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка удаления: {ex.Message}");
            }
        }
    }
}
