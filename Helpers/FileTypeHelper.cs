using System.Text;
namespace CIDE.Helpers;

public enum FileDisplayMode
{
    Text,
    Binary,
    Executable,
}
public static class FileTypeHelper
{
    private static readonly HashSet<string> t_executableExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".msi", ".msix", ".appx",
        };
    private static readonly HashSet<string> t_binaryExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".o", ".obj", ".a", ".lib", ".so", ".dylib", ".dll", ".pdb",
            ".zip", ".tar", ".gz", ".bz2", ".xz", ".zst", ".7z", ".rar", ".pak",
            ".db", ".sqlite", ".sqlite3", ".mdb", ".accdb",
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".webp", ".tiff", ".tga", ".psd",
            ".mp3", ".mp4", ".avi", ".mov", ".mkv", ".flac", ".wav", ".ogg", ".m4a",
            ".woff", ".woff2", ".ttf", ".eot", ".otf",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".class", ".pyc", ".pyo", ".pyd",
            ".bin", ".dat", ".raw", ".img",
        };
    public static FileDisplayMode GetDisplayMode(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext switch
        {
            _ when t_executableExtensions.Contains(ext) => FileDisplayMode.Executable,
            _ when t_binaryExtensions.Contains(ext) => FileDisplayMode.Binary,
            _ => IsBinaryFile(filePath) ? FileDisplayMode.Binary : FileDisplayMode.Text
        };
    }
    public static string GetMonacoLanguage(string filePath)
    {
        var name = Path.GetFileName(filePath).ToLowerInvariant();
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return (ext, name) switch
        {
            (_, "makefile" or "gnumakefile") => "makefile",
            (_, "dockerfile" or "containerfile") => "dockerfile",
            (".c", _) => "c",
            (".cpp" or ".cc" or ".cxx" or ".c++", _) => "cpp",
            (".h" or ".hpp" or ".hh" or ".hxx", _) => "cpp",
            (".cs", _) => "csharp",
            (".csproj" or ".slnx" or ".props" or ".targets", _) => "xml",
            (".razor" or ".cshtml", _) => "razor",
            (".html" or ".htm", _) => "html",
            (".css", _) => "css",
            (".scss", _) => "scss",
            (".less", _) => "less",
            (".js" or ".mjs" or ".cjs" or ".jsx", _) => "javascript",
            (".ts" or ".tsx", _) => "typescript",
            (".json" or ".jsonc", _) => "json",
            (".xml" or ".xaml" or ".config" or ".resx" or ".svg", _) => "xml",
            (".yaml" or ".yml", _) => "yaml",
            (".toml", _) => "ini",
            (".sql", _) => "sql",
            (".md" or ".markdown" or ".mdx", _) => "markdown",
            (".py" or ".pyw", _) => "python",
            (".sh" or ".bash" or ".zsh" or ".fish", _) => "shell",
            (".bat" or ".cmd", _) => "bat",
            (".ps1" or ".psm1" or ".psd1", _) => "powershell",
            (".lua", _) => "lua",
            (".rb", _) => "ruby",
            (".php", _) => "php",
            (".r" or ".rmd", _) => "r",
            (".go", _) => "go",
            (".rs", _) => "rust",
            (".java", _) => "java",
            (".kt" or ".kts", _) => "kotlin",
            (".swift", _) => "swift",
            (".mk" or ".make", _) => "makefile",
            (".txt" or ".log" or ".ini" or ".env" or ".gitignore" or ".gitattributes" or ".editorconfig", _) => "plaintext",
            _ => "plaintext"
        };
    }
    public static string GetFileSize(string filePath)
    {
        try
        {
            var bytes = new FileInfo(filePath).Length;
            return bytes switch
            {
                < 1024L => $"{bytes} Б",
                < 1024L * 1024 => $"{bytes / 1024.0:F1} КБ",
                < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} МБ",
                _ => $"{bytes / (1024.0 * 1024 * 1024):F2} ГБ",
            };
        }
        catch { return ""; }
    }
    private static bool IsBinaryFile(string filePath)
    {
        try
        {
            using var stream = new FileStream(
                filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var length = (int)Math.Min(8192, stream.Length);
            if (length == 0)
            {
                return false;
            }
            var buffer = new byte[length];
            var read = stream.Read(buffer, 0, length);
            return HasBinarySignature(buffer, read) || HasHighBinaryRatio(buffer, read);
        }
        catch
        {
            return true;
        }
    }
    private static bool HasBinarySignature(byte[] buf, int len) =>
        buf.AsSpan(0, len) is
            [0x4D, 0x5A, ..] or
            [0x7F, 0x45, 0x4C, 0x46, ..] or
            [0x89, 0x50, 0x4E, 0x47, _, _, _, _, ..] or
            [0x50, 0x4B, ..] or
            [0x25, 0x50, 0x44, 0x46, ..];
    private static bool HasHighBinaryRatio(byte[] buf, int len)
    {
        var nullBytes = 0;
        var controlBytes = 0;
        foreach (var b in buf.AsSpan(0, len))
        {
            if (b == 0x00)
            {
                nullBytes++;
            }
            else if (b is < 0x08 or (>= 0x0E and < 0x20))
            {
                controlBytes++;
            }
        }
        return (nullBytes > 0 && nullBytes * 100 / len >= 1) ||
               (controlBytes * 100 / len >= 10);
    }
}
