using System.Collections.ObjectModel;
using System.Text.Json;
using CIDE.Models;
using CommunityToolkit.Maui.Storage;
namespace CIDE.Services;
public static class WorkspaceService
{
    public static ObservableCollection<RecentEntry> Recents { get; } = new();
    private static readonly string RecentsFile = Path.Combine(FileSystem.AppDataDirectory, "recents.json");
    public static async Task<string?> PickFolderAsync()
    {
        var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
        if (result.IsSuccessful)
        {
            AddRecent(result.Folder.Path, "folder");
            return result.Folder.Path;
        }
        return null;
    }
    public static async Task<string?> PickSolutionAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Выберите решение",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> {
                { DevicePlatform.WinUI, [".sln", ".slnx"] }
            })
        });
        if (result != null)
        {
            var dir = Path.GetDirectoryName(result.FullPath);
            AddRecent(result.FullPath, "solution");
            return dir;
        }
        return null;
    }
    public static async Task<string> ReadFileAsync(string path) => await File.ReadAllTextAsync(path);
    public static async Task SaveFileAsync(string path, string content) => await File.WriteAllTextAsync(path, content);
    public static void LoadRecents()
    {
        if (File.Exists(RecentsFile))
        {
            var list = JsonSerializer.Deserialize(File.ReadAllText(RecentsFile), CideJsonContext.Default.ListRecentEntry);
            if (list != null)
            {
                foreach (var r in list)
                {
                    Recents.Add(r);
                }
            }
        }
    }
    private static void AddRecent(string path, string type)
    {
        var ex = Recents.FirstOrDefault(r => r.Path == path);
        if (ex != null)
        {
            Recents.Remove(ex);
        }
        Recents.Insert(0, new RecentEntry { Path = path, Type = type, LastOpened = DateTime.Now });
        File.WriteAllText(RecentsFile, JsonSerializer.Serialize(Recents.ToList(), CideJsonContext.Default.ListRecentEntry));
    }
    public static FileNode BuildFolderTree(string rootPath)
    {
        var root = new FileNode { Name = Path.GetFileName(rootPath), FullPath = rootPath, Kind = FileNodeKind.Folder, IsExpanded = true, Depth = 0 };
        FillChildren(root, new DirectoryInfo(rootPath), 1);
        return root;
    }
    private static void FillChildren(FileNode parent, DirectoryInfo dir, int depth)
    {
        if (!dir.Exists)
            return;
        foreach (var sub in dir.GetDirectories().Where(d => !d.Name.StartsWith(".") && d.Name != "bin" && d.Name != "obj").OrderBy(d => d.Name))
        {
            var node = new FileNode { Name = sub.Name, FullPath = sub.FullName, Kind = FileNodeKind.Folder, Depth = depth };
            FillChildren(node, sub, depth + 1);
            parent.Children.Add(node);
        }
        foreach (var f in dir.GetFiles().OrderBy(f => f.Name))
            parent.Children.Add(new FileNode { Name = f.Name, FullPath = f.FullName, Kind = FileNodeKind.File, Depth = depth });
    }
}
