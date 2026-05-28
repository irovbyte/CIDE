using System.Diagnostics;
using System.Text.RegularExpressions;
namespace CIDE.Services;

public static class GitService
{
    private static readonly char[] t_separators = ['\n', '\r'];
    public static async Task CloneRepositoryAsync(string url, string targetFolder, Action<string>? onProgress = null)
    {
        var gitPath = true ? "git" : "git";
        var startInfo = new ProcessStartInfo
        {
            FileName = gitPath,
            Arguments = $"clone \"{url}\" \"{targetFolder}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (s, e) => { if (e.Data != null) { onProgress?.Invoke(e.Data); } };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) { onProgress?.Invoke(e.Data); } };
        _ = process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Git clone failed with exit code {process.ExitCode}");
        }
    }
    public static async Task<Dictionary<string, string>> GetGitStatusAsync(string repoPath)
    {
        var statusMap = new Dictionary<string, string>();
        if (!Directory.Exists(Path.Combine(repoPath, ".git")))
        {
            return statusMap;
        }
        var gitPath = true ? "git" : "git";
        var startInfo = new ProcessStartInfo
        {
            FileName = gitPath,
            Arguments = "status --porcelain",
            WorkingDirectory = repoPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return statusMap;
            }
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var lines = output.Split(t_separators, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Length > 3)
                    {
                        var status = line[..2].Trim();
                        var file = line[3..].Trim();
                        if (file.StartsWith('\"') && file.EndsWith('\"'))
                        {
                            file = file[1..^1];
                        }
                        if (status == "??")
                        {
                            status = "U";
                        }
                        else if (status.Contains('M'))
                        {
                            status = "M";
                        }
                        file = file.Replace("/", "\\");
                        var fullPath = Path.Combine(repoPath, file);
                        statusMap[fullPath] = status;
                    }
                }
            }
        }
        catch
        {
        }
        return statusMap;
    }
    public static async Task<string> GetCurrentBranchAsync(string repoPath)
    {
        if (!Directory.Exists(Path.Combine(repoPath, ".git")))
        {
            return "";
        }
        var gitPath = true ? "git" : "git";
        var startInfo = new ProcessStartInfo
        {
            FileName = gitPath,
            Arguments = "rev-parse --abbrev-ref HEAD",
            WorkingDirectory = repoPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return "";
            }
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode == 0)
            {
                return output.Trim();
            }
        }
        catch
        {
        }
        return "";
    }
}
