using System.Diagnostics;
using System.IO.Compression;

namespace CIDE.Services;

public static class ToolchainService
{
    public static string ToolchainDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CIDE", "toolchains");
    public static string MinGWDir => Path.Combine(ToolchainDir, "mingw64");
    public static string GppPath => Path.Combine(MinGWDir, "bin", "g++.exe");

    public static string GitDir => Path.Combine(ToolchainDir, "git");
    public static string GitPath => Path.Combine(GitDir, "cmd", "git.exe"); // For MinGit it's often cmd/git.exe

    private static readonly HttpClient t_httpClient = new();

    public static bool IsGccInstalled => File.Exists(GppPath);
    public static bool IsGitInstalled => File.Exists(GitPath);

    public static async Task InstallMinGWAsync(Action<string> onProgress)
    {
        if (IsGccInstalled)
        {
            onProgress("Компилятор уже установлен.");
            return;
        }

        _ = Directory.CreateDirectory(ToolchainDir);
        var zipPath = Path.Combine(ToolchainDir, "mingw.zip");

        onProgress("Скачивание компилятора MinGW (C/C++)...");
        var downloadUrl = "https://github.com/brechtsanders/winlibs_mingw/releases/download/13.2.0-16.0.6-11.0.1-msvcrt-r2/winlibs-x86_64-posix-seh-gcc-13.2.0-mingw-w64msvcrt-11.0.1-r2.zip";

        try
        {
            var response = await t_httpClient.GetAsync(downloadUrl);
            _ = response.EnsureSuccessStatusCode();
            using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs);
        }
        catch (Exception ex)
        {
            onProgress($"Ошибка скачивания: {ex.Message}");
            return;
        }

        try
        {
            onProgress("Распаковка компилятора (это займет пару минут)...");
            await Task.Run(() =>
            {
                if (Directory.Exists(MinGWDir))
                {
                    Directory.Delete(MinGWDir, true);
                }

                ZipFile.ExtractToDirectory(zipPath, ToolchainDir);
            });

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            onProgress("Установка компилятора завершена!");
        }
        catch (Exception ex)
        {
            onProgress($"Ошибка распаковки: {ex.Message}");
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
        }
    }

    public static async Task InstallGitAsync(Action<string> onProgress)
    {
        if (IsGitInstalled)
        {
            onProgress("Git уже установлен.");
            return;
        }

        _ = Directory.CreateDirectory(ToolchainDir);
        var zipPath = Path.Combine(ToolchainDir, "git.zip");

        onProgress("Скачивание Portable Git...");
        var downloadUrl = "https://github.com/git-for-windows/git/releases/download/v2.44.0.windows.1/MinGit-2.44.0-64-bit.zip";

        try
        {
            var response = await t_httpClient.GetAsync(downloadUrl);
            _ = response.EnsureSuccessStatusCode();
            using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs);
        }
        catch (Exception ex)
        {
            onProgress($"Ошибка скачивания Git: {ex.Message}");
            return;
        }

        try
        {
            onProgress("Распаковка Git...");
            await Task.Run(() =>
            {
                if (Directory.Exists(GitDir))
                {
                    Directory.Delete(GitDir, true);
                }
                ZipFile.ExtractToDirectory(zipPath, GitDir);
            });

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            onProgress("Установка Git завершена!");
        }
        catch (Exception ex)
        {
            onProgress($"Ошибка распаковки Git: {ex.Message}");
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
        }
    }
}
