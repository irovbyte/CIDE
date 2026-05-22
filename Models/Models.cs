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
    public bool IsPopulated { get; set; }
    public int Depth { get; set; }
    public string Glyph => Kind switch
    {
        FileNodeKind.Solution => "\uE810",
        FileNodeKind.Project => "\uE80F",
        FileNodeKind.Folder => IsExpanded ? "\uE974" : "\uE972",
        FileNodeKind.File => "\uE8A0",
        _ => " "
    };
    public string GlyphColor => Kind == FileNodeKind.Folder ? "#858585" : "#858585";
    public string IconText => Kind switch
    {
        FileNodeKind.Solution => "\uE71B",
        FileNodeKind.Project => "\uE7B8",
        FileNodeKind.Folder => IsExpanded ? "\uE8E5" : "\uE8B7",
        FileNodeKind.File => Path.GetExtension(Name).ToLowerInvariant() switch
        {
            ".cs" or ".cpp" or ".c" or ".h" or ".hpp" => "\uE943",
            ".csproj" or ".sln" or ".slnx" => "\uE713",
            ".xaml" => "\uE7C3",
            ".json" => "\uE7C3",
            _ => "\uE7C3"
        },
        _ => "\uE7C3"
    };
    public string IconColor => Kind switch
    {
        FileNodeKind.Solution => "#C586C0",
        FileNodeKind.Project => "#4EC9B0",
        FileNodeKind.Folder => "#E8C56D",
        FileNodeKind.File => Path.GetExtension(Name).ToLowerInvariant() switch
        {
            ".cs" => "#9CDCFE",
            ".csproj" => "#4EC9B0",
            ".xaml" or ".json" => "#CE9178",
            ".md" => "#D4D4D4",
            ".slnx" => "#C586C0",
            _ => "#858585"
        },
        _ => "#858585"
    };
    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(Glyph));
        OnPropertyChanged(nameof(IconText));
    }
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
    public FileDisplayMode DisplayMode { get; init; } = FileDisplayMode.Text;
    public string FileName => Path.GetFileName(FilePath);
    public string DisplayName => IsModified ? $"● {FileName}" : FileName;
    public string BackgroundBrush => IsActive ? "#1E1E1E" : "Transparent";
}
