using System.Text.RegularExpressions;
#pragma warning disable SYSLIB1045
using CIDE.Models;
namespace CIDE.Services;

public static class CrossCheckService
{
    private static HashSet<string>? t_cachedServerEndpoints;
    public static void InvalidateCache() => t_cachedServerEndpoints = null;
    public static Task AnalyzeAsync(MainPageModel model)
    {
        _ = Task.Run(async () =>
        {
            if (model.Sidebar.WorkspaceRoots.Count < 2)
            {
                return;
            }
            var localRoot = model.Sidebar.WorkspaceRoots.FirstOrDefault(r => r.Name.Contains("Локально") || r.Provider is LocalFileSystemProvider);
            var serverRoot = model.Sidebar.WorkspaceRoots.FirstOrDefault(r => r.Provider is SshFileSystemProvider);
            if (localRoot == null || serverRoot == null)
            {
                return;
            }
            var clientEndpoints = new List<string>();
            foreach (var tab in model.Tabs)
            {
                if (tab.Root == localRoot && tab.FilePath.EndsWith(".cs"))
                {
                    var text = tab.Content;
                    var matches = Regex.Matches(text, @"HttpClient\.GetAsync\(""?(/api/[^""\)]+)""?\)", RegexOptions.None, TimeSpan.FromSeconds(1));
                    foreach (Match match in matches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            clientEndpoints.Add(match.Groups[1].Value);
                        }
                    }
                }
            }
            if (clientEndpoints.Count == 0)
            {
                return;
            }
            if (t_cachedServerEndpoints == null)
            {
                t_cachedServerEndpoints = [];
                var serverProvider = serverRoot.Provider;
                var serverFiles = await GetCsFilesAsync(serverProvider, serverRoot.RootNode.FullPath);
                foreach (var file in serverFiles)
                {
                    var content = await serverProvider.ReadFileAsync(file);
                    var matchesMapGet = Regex.Matches(content, @"MapGet\(""?(/api/[^""\)]+)""?", RegexOptions.None, TimeSpan.FromSeconds(1));
                    foreach (Match match in matchesMapGet)
                    {
                        if (match.Groups.Count > 1)
                        {
                            _ = t_cachedServerEndpoints.Add(match.Groups[1].Value);
                        }
                    }
                    var matchesHttpGet = Regex.Matches(content, @"\[HttpGet\(""?(/api/[^""\)]+)""?\)\]", RegexOptions.None, TimeSpan.FromSeconds(1));
                    foreach (Match match in matchesHttpGet)
                    {
                        if (match.Groups.Count > 1)
                        {
                            _ = t_cachedServerEndpoints.Add(match.Groups[1].Value);
                        }
                    }
                }
            }
            var serverEndpoints = t_cachedServerEndpoints;
            var newErrors = new List<BuildError>();
            foreach (var ep in clientEndpoints)
            {
                if (!serverEndpoints.Contains(ep))
                {
                    newErrors.Add(new BuildError
                    {
                        Severity = BuildErrorSeverity.Warning,
                        Message = $"Cross-Check Analyzer: Эндпоинт {ep} вызывается на клиенте, но не найден на сервере!",
                        File = "CrossCheck",
                        Line = 0
                    });
                }
            }
            if (newErrors.Count > 0)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var oldErrors = model.CompilerErrors.Where(e => e.File == "CrossCheck").ToList();
                    foreach (var old in oldErrors)
                    {
                        _ = model.CompilerErrors.Remove(old);
                    }
                    foreach (var err in newErrors)
                    {
                        model.CompilerErrors.Add(err);
                    }
                });
            }
        });
        return Task.CompletedTask;
    }
    private static async Task<List<string>> GetCsFilesAsync(IFileSystemProvider provider, string path)
    {
        var files = new List<string>();
        try
        {
            var items = await provider.GetFilesAsync(path);
            foreach (var item in items)
            {
                if (item.Name.EndsWith(".cs"))
                {
                    files.Add(item.FullPath);
                }
            }
            var dirs = await provider.GetDirectoriesAsync(path);
            foreach (var dir in dirs)
            {
                if (!dir.Name.Contains(".git") && !dir.Name.Contains("bin") && !dir.Name.Contains("obj"))
                {
                    files.AddRange(await GetCsFilesAsync(provider, dir.FullPath));
                }
            }
        }
        catch { }
        return files;
    }
}
