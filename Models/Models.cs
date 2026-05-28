using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Avalonia;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
namespace CIDE.Models;

public enum BuildProfileType { SingleFileC, SingleFileCpp, Makefile, DotNetProject, Python, Bash, Unknown }
public class BuildProfile
{
    public string DisplayName { get; set; } = "";
    public BuildProfileType Type { get; set; }
    public string TargetPath { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public override string ToString() => DisplayName;
}
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
    public string Type { get; set; } = "Local";
    public string Path { get; set; } = "";
    public string? Host { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}
public partial class FileNode : ObservableObject
{
    public WorkspaceRoot? Root { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconPath))]
    public partial string Name { get; set; } = "";
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
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GlyphColor))]
    public partial bool IsSelected { get; set; }
    public bool IsPopulated { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DepthLevels))]
    public partial int Depth { get; set; }
    public IEnumerable<int> DepthLevels => Enumerable.Range(0, Depth);
    public string Glyph => Kind == FileNodeKind.File ? " " : "\uE76C";
    public string GlyphColor => IsSelected ? "#BA68C8" : "#707080";
    public string IconPath => IconThemeService.GetIconPath(Name, Kind, IsExpanded);
    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(IconPath));
        if (value && !IsPopulated && Root != null)
        {
            _ = WorkspaceService.FillChildrenAsync(Root, this, FullPath, Depth + 1);
        }
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
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    public partial bool IsDeleted { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundBrush))]
    public partial bool IsActive { get; set; }
    public FileDisplayMode DisplayMode { get; init; } = FileDisplayMode.Text;
    public bool IsBinary => DisplayMode == FileDisplayMode.Binary;
    public bool IsText => DisplayMode == FileDisplayMode.Text;
    public string FileName => Path.GetFileName(FilePath);
    public string DisplayName => IsModified ? $"● {FileName}" : FileName;
    public string BackgroundBrush => IsActive ? "#1E1E1E" : "Transparent";
    public string IconPath => IconThemeService.GetIconPath(FileName, FileNodeKind.File, false);
    [ObservableProperty]
    public partial string Encoding { get; set; } = "UTF-8";
    [ObservableProperty]
    public partial string LineEndings { get; set; } = "LF";
    [ObservableProperty]
    public partial string IndentDescription { get; set; } = "Spaces: 4";
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
    public string SeverityIcon => Severity switch
    {
        BuildErrorSeverity.Error => "✖",
        BuildErrorSeverity.Warning => "⚠",
        BuildErrorSeverity.Info => "✔",
        _ => "ℹ"
    };
    public bool HasLocation => !string.IsNullOrEmpty(File) && File != "Unknown" && Line > 0;
    public string LocationText => HasLocation ? $"{File}:{Line}" : "";
    public string DisplayText => Severity == BuildErrorSeverity.Info
        ? Message
        : $"{(string.IsNullOrEmpty(Code) ? "" : Code + ": ")}{Message}";
    public string Color => Severity switch
    {
        BuildErrorSeverity.Error => "#FF5555",
        BuildErrorSeverity.Warning => "#FFB86C",
        BuildErrorSeverity.Info => "#50FA7B",
        _ => "#8BE9FD"
    };
}
