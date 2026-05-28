using System.IO;
using CIDE.Models;

namespace CIDE.Services;

public static class IconThemeService
{
    public static string GetIconPath(string fileName, FileNodeKind kind, bool isExpanded)
    {
        var basePath = "avares://CIDE/Assets/Icons";

        if (kind is FileNodeKind.Folder or FileNodeKind.Project or FileNodeKind.Solution or FileNodeKind.WorkspaceRoot)
        {
            var folderName = Path.GetFileName(fileName).ToLowerInvariant();
            var specialFolderIcon = folderName switch
            {
                "src" => "src",
                "tests" => "tests",
                "images" => "images",
                "layout" => "layout",
                ".github" => ".github",
                ".vscode" => ".vscode",
                _ => "default"
            };

            return isExpanded ? $"{basePath}/folders/{specialFolderIcon}-open.svg" : $"{basePath}/folders/{specialFolderIcon}.svg";
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var name = Path.GetFileName(fileName).ToLowerInvariant();

        var exactIcon = name switch
        {
            "makefile" or "cmakelists.txt" => "cmake.svg",
            "dockerfile" => "docker.svg",
            ".gitignore" => "git.svg",
            "package.json" => "npm.svg",
            "readme.md" => "markdown.svg",
            ".editorconfig" => "editorconfig.png",
            _ => null
        };

        if (exactIcon != null)
        {
            return $"{basePath}/{exactIcon}";
        }

        var iconName = ext switch
        {
            ".c" => "c.png",
            ".h" => "c-h.png",
            ".cpp" or ".cc" or ".cxx" => "cpp.svg",
            ".hpp" => "cpp-h.png",
            ".cs" => "csharp.svg",
            ".csproj" => "dotnet.svg",
            ".sln" or ".slnx" => "visualstudio.svg",
            ".fs" => "fsharp.svg",
            ".html" or ".htm" => "html.svg",
            ".css" => "css.svg",
            ".scss" => "sass.svg",
            ".js" or ".mjs" => "javascript.svg",
            ".ts" => "typescript.svg",
            ".jsx" => "react.svg",
            ".tsx" => "react-alt.svg",
            ".vue" => "vue.svg",

            ".py" => "python.svg",
            ".rb" => "ruby.svg",
            ".php" => "php.svg",
            ".go" => "go.svg",
            ".rs" => "rust.svg",
            ".java" => "java.svg",
            ".sh" or ".bash" => "shell.png",
            ".ps1" => "powershell.png",
            ".bat" or ".cmd" => "exe.png",

            ".json" => "json.svg",
            ".xml" => "markup.png",
            ".xaml" or ".axaml" => "xaml.png",
            ".yaml" or ".yml" => "yaml.svg",
            ".toml" => "toml.png",
            ".sql" => "database.svg",
            ".md" => "markdown.svg",
            ".csv" or ".xls" => "excel.svg",

            ".png" or ".jpg" or ".jpeg" or ".svg" or ".ico" => "image.png",
            ".mp3" => "audio.png",
            ".mp4" => "video.png",
            ".zip" or ".tar" or ".gz" => "zip.svg",

            _ => "default.png"
        };

        return $"{basePath}/{iconName}";
    }
}
