using System.Text.RegularExpressions;
#pragma warning disable SYSLIB1045

using CIDE.Models;

namespace CIDE.Services;

public static class CrossCheckService
{
    public static Task AnalyzeAsync(MainPageModel model)
    {
        _ = Task.Run(async () =>
        {
            // Только если у нас есть хотя бы 2 корня (локальный и серверный)
            if (model.WorkspaceRoots.Count < 2)
            {
                return;
            }

            var localRoot = model.WorkspaceRoots.FirstOrDefault(r => r.Name.Contains("Локально") || r.Provider is LocalFileSystemProvider);
            var serverRoot = model.WorkspaceRoots.FirstOrDefault(r => r.Provider is SshFileSystemProvider);

            if (localRoot == null || serverRoot == null)
            {
                return;
            }

            // Собираем все HttpClient.GetAsync запросы из локальных файлов (.cs)
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

            // Читаем все .cs файлы на сервере, чтобы найти endpoints (app.MapGet, [HttpGet])
            var serverEndpoints = new HashSet<string>();
            var serverProvider = serverRoot.Provider;
            var serverFiles = await GetCsFilesAsync(serverProvider, serverRoot.RootNode.FullPath);

            foreach (var file in serverFiles)
            {
                var content = await serverProvider.ReadFileAsync(file);
                // Ищем app.MapGet("/api/..."
                var matchesMapGet = Regex.Matches(content, @"MapGet\(""?(/api/[^""\)]+)""?", RegexOptions.None, TimeSpan.FromSeconds(1));
                foreach (Match match in matchesMapGet)
                {
                    if (match.Groups.Count > 1)
                    {
                        _ = serverEndpoints.Add(match.Groups[1].Value);
                    }
                }

                // Ищем [HttpGet("/api/...")]
                var matchesHttpGet = Regex.Matches(content, @"\[HttpGet\(""?(/api/[^""\)]+)""?\)\]", RegexOptions.None, TimeSpan.FromSeconds(1));
                foreach (Match match in matchesHttpGet)
                {
                    if (match.Groups.Count > 1)
                    {
                        _ = serverEndpoints.Add(match.Groups[1].Value);
                    }
                }
            }

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
                    // Очищаем старые предупреждения CrossCheck
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
