using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Avalonia;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
namespace CIDE.Models;

public enum FileNodeKind { Solution, Project, Folder, File, WorkspaceRoot }

public class WorkspaceRoot
{
    public string Name { get; set; } = "";
    public IFileSystemProvider Provider { get; set; } = null!;
    public FileNode RootNode { get; set; } = null!;
}

public class WorkspaceConfig
{
    public List<WorkspaceRootConfig> Roots { get; set; } = [];
}

public class WorkspaceRootConfig
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "Local"; // Local, SSH, WSL
    public string Path { get; set; } = "";
    // Для SSH
    public string? Host { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public partial class FileNode : ObservableObject
{
    public WorkspaceRoot? Root { get; set; }
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public FileNodeKind Kind { get; set; }
    public ObservableCollection<FileNode> Children { get; set; } = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGitModified))]
    [NotifyPropertyChangedFor(nameof(IsGitUntracked))]
    public partial string GitStatus { get; set; } = "";

    public bool IsGitModified => GitStatus == "M";
    public bool IsGitUntracked => GitStatus == "U";
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }
    public bool IsPopulated { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DepthLevels))]
    public partial int Depth { get; set; }
    public IEnumerable<int> DepthLevels => Enumerable.Range(0, Depth);
    public string Glyph => Kind == FileNodeKind.File ? " " : "\uE76C";
    public static string GlyphColor => "#707080";
    public string IconText => Kind switch
    {
        FileNodeKind.WorkspaceRoot => "\uE71B", // Or any suitable icon for root
        FileNodeKind.Solution => "\uE71B",
        FileNodeKind.Project => "\uE7B8",
        FileNodeKind.Folder => IsExpanded ? "\uE8E5" : "\uE8B7",
        FileNodeKind.File => Path.GetExtension(Name).ToLowerInvariant() switch
        {
            ".cs" or ".cpp" or ".c" or ".h" or ".hpp" => "\uE943",
            ".csproj" or ".sln" or ".slnx" => "\uE713",
            ".xaml" or ".json" => "\uE7C3",
            _ => "\uE7C3"
        },
        _ => "\uE7C3"
    };
    public string IconColor => Kind switch
    {
        FileNodeKind.WorkspaceRoot => "#FFFFFF", // White for root
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
        OnPropertyChanged(nameof(IconText));
    }
}
public partial class EditorTab : ObservableObject
{
    public WorkspaceRoot? Root { get; set; }
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
    public bool IsBinary => DisplayMode == FileDisplayMode.Binary;
    public bool IsText => DisplayMode == FileDisplayMode.Text;
    public string FileName => Path.GetFileName(FilePath);
    public string DisplayName => IsModified ? $"● {FileName}" : FileName;
    public string BackgroundBrush => IsActive ? "#1E1E1E" : "Transparent";
    [JsonIgnore]
    public TextDocument? Document { get; set; }
    public Vector SavedScrollOffset { get; set; }
    public int SavedCaretOffset { get; set; }
}
public enum BuildErrorSeverity { Error, Warning, Info }
public class BuildError
{
    public string Message { get; set; } = "";
    public string File { get; set; } = "";
    public int Line { get; set; }
    public string Code { get; set; } = "";
    public BuildErrorSeverity Severity { get; set; }
    public string DisplayText => $"[{Severity}] {(string.IsNullOrEmpty(Code) ? "" : Code + ": ")}{Message} in {File} (Line {Line})";
    public string Color => Severity switch
    {
        BuildErrorSeverity.Error => "#FF5555",
        BuildErrorSeverity.Warning => "#FFB86C",
        BuildErrorSeverity.Info => throw new NotImplementedException(),
        _ => "#8BE9FD"
    };
}
