using System.Collections.ObjectModel;
using System.Text.Json;
using CIDE.Models;
using Window = Microsoft.UI.Xaml.Window;

namespace CIDE.Services;

public static class WorkspaceService
{
    public static ObservableCollection<RecentEntry> Recents { get; } = [];

    private static readonly string t_recentsFile = Path.Combine(FileSystem.AppDataDirectory, "recents.json");
    private static IntPtr GetParentWindowHandle()
    {
        if (Application.Current is not App app)
        {
            return IntPtr.Zero;
        }
        var platformView = app.Windows.Count > 0 ? app.Windows[0]?.Handler?.PlatformView : null;
        return platformView is Window window
            ? WinRT.Interop.WindowNative.GetWindowHandle(window)
            : IntPtr.Zero;
    }
    public static async Task<string?> PickFolderAsync()
    {
        var path = await MainThread.InvokeOnMainThreadAsync(() => Win32FileDialog.ShowFolderPicker(GetParentWindowHandle()));
        if (!string.IsNullOrEmpty(path))
        {
            await AddRecentAsync(path, "folder");
            return path;
        }
        return null;
    }
    public static async Task<string?> PickSolutionAsync()
    {
        var path = await MainThread.InvokeOnMainThreadAsync(() => Win32FileDialog.ShowFilePicker(GetParentWindowHandle(), "Выберите решение", "Файлы решений (*.sln, *.slnx)", "*.sln;*.slnx"));
        if (!string.IsNullOrEmpty(path))
        {
            await AddRecentAsync(path, "solution");
            return Path.GetDirectoryName(path);
        }
        return null;
    }

    public static async Task<string> ReadFileAsync(string path) => await File.ReadAllTextAsync(path);
    public static async Task SaveFileAsync(string path, string content) => await File.WriteAllTextAsync(path, content);

    public static void LoadRecents()
    {
        if (File.Exists(t_recentsFile))
        {
            var list = JsonSerializer.Deserialize(File.ReadAllText(t_recentsFile), CideJsonContext.Default.ListRecentEntry);
            if (list != null)
            {
                foreach (var r in list)
                {
                    Recents.Add(r);
                }
            }
        }
    }
    private static async Task AddRecentAsync(string path, string type)
    {
        var existing = Recents.FirstOrDefault(r => r.Path == path);
        if (existing is not null)
        {
            _ = Recents.Remove(existing);
        }
        Recents.Insert(0, new RecentEntry { Path = path, Type = type, LastOpened = DateTime.Now });
        var json = JsonSerializer.Serialize([.. Recents], CideJsonContext.Default.ListRecentEntry);
        await File.WriteAllTextAsync(t_recentsFile, json);
    }

    public static FileNode BuildFolderTree(string rootPath)
    {
        var root = new FileNode { Name = Path.GetFileName(rootPath), FullPath = rootPath, Kind = FileNodeKind.Folder, IsExpanded = true, Depth = 0, IsPopulated = true };
        FillChildren(root, new DirectoryInfo(rootPath), 1);
        return root;
    }

    public static void FillChildren(FileNode parent, DirectoryInfo dir, int depth)
    {
        if (!dir.Exists)
        {
            return;
        }

        parent.Children.Clear();
        foreach (var sub in dir.GetDirectories().Where(d => !d.Name.StartsWith('.') && d.Name != "bin" && d.Name != "obj").OrderBy(d => d.Name))
        {
            var node = new FileNode { Name = sub.Name, FullPath = sub.FullName, Kind = FileNodeKind.Folder, Depth = depth, IsPopulated = false };
            node.Children.Add(new FileNode { Name = "dummy", Kind = FileNodeKind.File });
            parent.Children.Add(node);
        }

        foreach (var f in dir.GetFiles().OrderBy(f => f.Name))
        {
            parent.Children.Add(new FileNode { Name = f.Name, FullPath = f.FullName, Kind = FileNodeKind.File, Depth = depth });
        }

        parent.IsPopulated = true;
    }
}
