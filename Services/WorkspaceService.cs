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

    public static async Task<string> ReadFileAsync(IFileSystemProvider provider, string path) => await provider.ReadFileAsync(path);
    public static async Task SaveFileAsync(IFileSystemProvider provider, string path, string content) => await provider.SaveFileAsync(path, content);
    public static async Task CreateFileAsync(IFileSystemProvider provider, string path) => await provider.CreateFileAsync(path);
    public static async Task CreateDirectoryAsync(IFileSystemProvider provider, string path) => await provider.CreateDirectoryAsync(path);
    public static async Task RenameAsync(IFileSystemProvider provider, string oldPath, string newPath) => await provider.RenameAsync(oldPath, newPath);
    public static async Task DeleteAsync(IFileSystemProvider provider, string path)
    {
        var isDir = await provider.DirectoryExistsAsync(path);
        await provider.DeleteAsync(path, isDir);
    }

    public static async Task<FileNode> BuildFolderTreeAsync(WorkspaceRoot rootModel, string rootPath, string? displayName = null, FileNodeKind kind = FileNodeKind.Folder)
    {
        var root = new FileNode
        {
            Root = rootModel,
            Name = displayName ?? Path.GetFileName(rootPath),
            FullPath = rootPath,
            Kind = kind,
            IsExpanded = true,
            Depth = 0,
            IsPopulated = true
        };
        await FillChildrenAsync(rootModel, root, rootPath, 1);
        return root;
    }

    public static async Task FillChildrenAsync(WorkspaceRoot rootModel, FileNode parent, string dirPath, int depth)
    {
        if (!await rootModel.Provider.DirectoryExistsAsync(dirPath))
        {
            return;
        }

        parent.Children.Clear();
        var dirs = await rootModel.Provider.GetDirectoriesAsync(dirPath);
        foreach (var sub in dirs)
        {
            var node = new FileNode { Root = rootModel, Name = sub.Name, FullPath = sub.FullPath, Kind = FileNodeKind.Folder, Depth = depth, IsPopulated = false };
            node.Children.Add(new FileNode { Root = rootModel, Name = "dummy", Kind = FileNodeKind.File });
            parent.Children.Add(node);
        }

        var files = await rootModel.Provider.GetFilesAsync(dirPath);
        foreach (var f in files)
        {
            parent.Children.Add(new FileNode { Root = rootModel, Name = f.Name, FullPath = f.FullPath, Kind = FileNodeKind.File, Depth = depth });
        }

        parent.IsPopulated = true;
    }
}
