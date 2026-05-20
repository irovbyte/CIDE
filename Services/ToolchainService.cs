using System.Diagnostics;
using System.IO.Compression;

namespace CIDE.Services;

public static class ToolchainService
{
    public static string ToolchainDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CIDE", "toolchains");
    public static string MinGWDir => Path.Combine(ToolchainDir, "mingw64");
    public static string GppPath => Path.Combine(MinGWDir, "bin", "g++.exe");

    public static bool IsGccInstalled => File.Exists(GppPath);

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

        using (var client = new HttpClient())
        {
            try
            {
                var response = await client.GetAsync(downloadUrl);
                _ = response.EnsureSuccessStatusCode();
                using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
            }
            catch (Exception ex)
            {
                onProgress($"Ошибка скачивания: {ex.Message}");
                return;
            }
        }

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
}
