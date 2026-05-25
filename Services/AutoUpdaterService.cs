using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CIDE.Services;

public class GitHubRelease
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";
    [JsonPropertyName("assets")]
    public GitHubAsset[] Assets { get; set; } = [];
}

public class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("browser_download_url")]
    public string DownloadUrl { get; set; } = "";
}

[JsonSerializable(typeof(GitHubRelease))]
internal sealed partial class UpdateJsonContext : JsonSerializerContext
{
}

public static class AutoUpdaterService
{
    private const string RepoUrl = "https://api.github.com/repos/irovbyte/CIDE/releases/latest";
    private static readonly HttpClient t_httpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "CIDE-AutoUpdater");
        return client;
    }

    public static async Task CheckForUpdatesAsync(Action<string> onProgress)
    {
        try
        {
            onProgress("Проверка обновлений...");
            var release = await t_httpClient.GetFromJsonAsync(RepoUrl, UpdateJsonContext.Default.GitHubRelease);

            if (release != null && release.Id != SettingsService.Instance.LastUpdateId)
            {
                onProgress($"Найдена новая версия! Скачивание...");
                var downloadUrl = "";
                foreach (var asset in release.Assets)
                {
                    if (asset.Name.Equals("CIDE.exe", StringComparison.OrdinalIgnoreCase) && OperatingSystem.IsWindows())
                    {
                        downloadUrl = asset.DownloadUrl;
                    }
                    else if (asset.Name.Equals("CIDE", StringComparison.OrdinalIgnoreCase) && OperatingSystem.IsLinux())
                    {
                        downloadUrl = asset.DownloadUrl;
                    }
                }

                if (!string.IsNullOrEmpty(downloadUrl))
                {
                    onProgress("Скачивание обновления...");
                    using var stream = await t_httpClient.GetStreamAsync(downloadUrl);
                    var currentExe = Process.GetCurrentProcess().MainModule?.FileName;
                    if (string.IsNullOrEmpty(currentExe))
                    {
                        return;
                    }

                    var backupExe = currentExe + ".old";
                    if (File.Exists(backupExe))
                    {
                        File.Delete(backupExe);
                    }
                    File.Move(currentExe, backupExe);
                    using (var fs = new FileStream(currentExe, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await stream.CopyToAsync(fs);
                    }

                    if (OperatingSystem.IsLinux())
                    {
                        File.SetUnixFileMode(currentExe,
                            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                    }

                    onProgress("Обновление установлено. Перезапуск...");
                    await Task.Delay(1000);
                    _ = Process.Start(new ProcessStartInfo
                    {
                        FileName = currentExe,
                        UseShellExecute = true
                    });
                    SettingsService.Instance.LastUpdateId = release.Id;
                    SettingsService.Instance.Save();
                    Environment.Exit(0);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update check failed: {ex.Message}");
        }
    }
}
