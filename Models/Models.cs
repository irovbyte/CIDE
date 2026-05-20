using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
namespace CIDE.Models;
public enum FileNodeKind { Solution, Project, Folder, File }
public partial class FileNode : ObservableObject
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public FileNodeKind Kind { get; set; }
    public ObservableCollection<FileNode> Children { get; set; } = [];
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }
    public int Depth { get; set; }
    public string Glyph => Kind switch
    {
        FileNodeKind.Solution => "⬡",
        FileNodeKind.Project => "◈",
        FileNodeKind.Folder => IsExpanded ? "▾" : "▸",
        FileNodeKind.File => "",
        _ => ""
    };
    private static readonly Dictionary<FileNodeKind, string> t_glyphColors = new()
    {
        { FileNodeKind.Solution, "#C586C0" },
        { FileNodeKind.Project, "#4EC9B0" },
        { FileNodeKind.Folder, "#E8C56D" }
    };
    public string GlyphColor => t_glyphColors.GetValueOrDefault(Kind, "Transparent");
    public string IconText => Kind != FileNodeKind.File ? "" : Path.GetExtension(Name).ToLowerInvariant() switch
    {
        ".cs" => "C#",
        ".csproj" => "⚙",
        ".xaml" => "⊡",
        ".json" => "{}",
        ".xml" => "</>",
        ".md" => "M↓",
        ".txt" => "T",
        ".slnx" => "◈",
        _ => "·"
    };
    public string IconColor => Kind != FileNodeKind.File ? "#858585" : Path.GetExtension(Name).ToLowerInvariant() switch
    {
        ".cs" => "#9CDCFE",
        ".csproj" => "#4EC9B0",
        ".xaml" or ".json" => "#CE9178",
        ".md" => "#D4D4D4",
        ".slnx" => "#C586C0",
        _ => "#858585"
    };
    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(Glyph));
}
public partial class EditorTab : ObservableObject
{
    public string FilePath { get; set; } = "";
    [ObservableProperty]
    public partial string Content { get; set; } = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    public partial bool IsModified { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundBrush))]
    public partial bool IsActive { get; set; }
    public string FileName => Path.GetFileName(FilePath);
    public string DisplayName => IsModified ? $"● {FileName}" : FileName;
    public string BackgroundBrush => IsActive ? "#1E1E1E" : "Transparent";
}
public class RecentEntry
{
    public string Path { get; set; } = "";
    public string Type { get; set; } = "folder";
    public DateTime LastOpened { get; set; } = DateTime.Now;
    public string Name => System.IO.Path.GetFileName(Path.TrimEnd('\\', '/'));
    public string Icon => Type == "solution" ? "⬡" : "▸";
}
[JsonSerializable(typeof(List<RecentEntry>))]
[JsonSerializable(typeof(string))]
internal sealed partial class CideJsonContext : JsonSerializerContext
{
}
