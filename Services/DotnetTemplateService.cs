using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CIDE.Services;

public class DotnetTemplateInfo
{
    public string Name { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string Language { get; set; } = "";
    public string Tags { get; set; } = "";
    public string DisplayName => $"{Name} ({ShortName})";
    public string Category => string.IsNullOrEmpty(Tags) ? "Other" : Tags.Split('/')[0];
}

public static class DotnetTemplateService
{
    public static async Task<List<DotnetTemplateInfo>> GetInstalledTemplatesAsync()
    {
        var templates = new List<DotnetTemplateInfo>();
        try
        {
            var tcs = new TaskCompletionSource<string>();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "new list",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            _ = process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var lines = output.Split('\n').Select(l => l.TrimEnd()).ToList();
            var dashLineIndex = lines.FindIndex(l => l.StartsWith("---", StringComparison.Ordinal) && l.Contains(" ---"));
            if (dashLineIndex >= 0 && dashLineIndex + 1 < lines.Count)
            {
                var dashLine = lines[dashLineIndex];
                var col1Start = 0;
                var col1End = dashLine.IndexOf(' ', col1Start);
                if (col1End == -1)
                { return templates; }
                var col2Start = dashLine.IndexOf('-', col1End);
                if (col2Start == -1)
                { return templates; }
                var col2End = dashLine.IndexOf(' ', col2Start);
                if (col2End == -1)
                { return templates; }

                var col3Start = dashLine.IndexOf('-', col2End);
                if (col3Start == -1)
                { return templates; }
                var col3End = dashLine.IndexOf(' ', col3Start);
                if (col3End == -1)
                { return templates; }

                var col4Start = dashLine.IndexOf('-', col3End);
                if (col4Start == -1)
                { return templates; }

                for (var i = dashLineIndex + 1; i < lines.Count; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line))
                    { continue; }
                    if (line.Length > col2Start && !string.IsNullOrWhiteSpace(line[..1]))
                    {
                        var name = line.Substring(col1Start, Math.Min(col1End - col1Start, line.Length - col1Start)).Trim();
                        var shortNameLength = Math.Min(col2End - col2Start, line.Length - col2Start);
                        var shortName = line.Substring(col2Start, shortNameLength).Trim();
                        var langLength = Math.Min(col3End - col3Start, line.Length - col3Start);
                        var lang = line.Substring(col3Start, langLength).Trim();
                        var tags = "";
                        if (line.Length > col4Start)
                        {
                            tags = line[col4Start..].Trim();
                        }
                        if (lang.Contains("[C#]") || string.IsNullOrEmpty(lang))
                        {
                            if (!string.IsNullOrEmpty(shortName) && !shortName.Contains(','))
                            {
                                templates.Add(new DotnetTemplateInfo
                                {
                                    Name = name,
                                    ShortName = shortName,
                                    Language = lang,
                                    Tags = tags
                                });
                            }
                        }
                    }
                }
            }
        }
        catch
        {
        }

        return [.. templates.OrderBy(t => t.Name)];
    }
}
