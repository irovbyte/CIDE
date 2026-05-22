using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CIDE.Models;

namespace CIDE.Services;

public static class WorkspaceService
{
    private static IStorageProvider? GetStorageProvider() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window }
            ? TopLevel.GetTopLevel(window)?.StorageProvider
            : null;

    public static async Task<string?> PickFolderAsync()
    {
        var provider = GetStorageProvider();
        if (provider == null)
        {
            return null;
        }

        var result = await Dispatcher.UIThread.InvokeAsync(() => provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Выберите папку проекта",
            AllowMultiple = false
        }));

        return result?.Count > 0 ? result[0].Path.LocalPath : null;
    }

    public static async Task<string?> PickSolutionAsync()
    {
        var provider = GetStorageProvider();
        if (provider == null)
        {
            return null;
        }

        var result = await Dispatcher.UIThread.InvokeAsync(() => provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите решение",
            AllowMultiple = false,
            FileTypeFilter = [
                new FilePickerFileType("Файлы решений")
                {
                    Patterns = [ "*.sln", "*.slnx" ]
                }
            ]
        }));

        if (result != null && result.Count > 0)
        {
            var path = result[0].Path.LocalPath;
            return path != null ? Path.GetDirectoryName(path) : null;
        }
        return null;
    }

    public static IFileSystemProvider CurrentProvider { get; set; } = new LocalFileSystemProvider();

    public static async Task<string> ReadFileAsync(string path) => await CurrentProvider.ReadFileAsync(path);
    public static async Task SaveFileAsync(string path, string content) => await CurrentProvider.SaveFileAsync(path, content);

    public static async Task<FileNode> BuildFolderTreeAsync(string rootPath)
    {
        var root = new FileNode { Name = Path.GetFileName(rootPath), FullPath = rootPath, Kind = FileNodeKind.Folder, IsExpanded = true, Depth = 0, IsPopulated = true };
        await FillChildrenAsync(root, rootPath, 1);
        return root;
    }

    public static async Task FillChildrenAsync(FileNode parent, string dirPath, int depth)
    {
        if (!await CurrentProvider.DirectoryExistsAsync(dirPath))
        {
            return;
        }

        parent.Children.Clear();
        var dirs = await CurrentProvider.GetDirectoriesAsync(dirPath);
        foreach (var sub in dirs)
        {
            var node = new FileNode { Name = sub.Name, FullPath = sub.FullPath, Kind = FileNodeKind.Folder, Depth = depth, IsPopulated = false };
            node.Children.Add(new FileNode { Name = "dummy", Kind = FileNodeKind.File });
            parent.Children.Add(node);
        }

        var files = await CurrentProvider.GetFilesAsync(dirPath);
        foreach (var f in files)
        {
            parent.Children.Add(new FileNode { Name = f.Name, FullPath = f.FullPath, Kind = FileNodeKind.File, Depth = depth });
        }

        parent.IsPopulated = true;
    }
}
