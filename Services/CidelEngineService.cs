#pragma warning disable CA1822
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using CIDE.Models;

namespace CIDE.Services;

public class CidelEngineService
{
    public static CidelEngineService Instance => field ??= new CidelEngineService();

    public SshConnectionInfo LocalEngineConnection { get; } = new SshConnectionInfo
    {
        Name = "CIDEL Engine",
        Host = "127.0.0.1",
        Port = 2222,
        Username = "root",
        UsePassword = true,
        Password = "cide",
        RootPath = "/"
    };

    public bool IsEngineRunning { get; private set; }
    public SshFileSystemProvider? EngineFileSystem { get; private set; }
    private Process? _qemuProcess;

    private CidelEngineService() { }

    public async Task CheckAndInstallEngineAsync(Action<string> onProgress)
    {
        if (SettingsService.Instance.IsCidelEngineInstalled && File.Exists(AppPaths.QemuExePath))
        {
            return;
        }

        try
        {
            if (!Directory.Exists(AppPaths.EngineDir))
            {
                _ = Directory.CreateDirectory(AppPaths.EngineDir);
            }

            onProgress("Скачивание ядра CIDEL OS...");
            var zipPath = Path.Combine(AppPaths.CacheDir, "cidel_engine.zip");

            using (var client = new HttpClient())
            {
                var response = await client.GetAsync("https://github.com/irovbyte/CIDE/releases/download/engine/cidel_engine.zip", HttpCompletionOption.ResponseHeadersRead);
                _ = response.EnsureSuccessStatusCode();

                using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                await response.Content.CopyToAsync(fs);
            }

            onProgress("Установка ядра системы...");
            ZipFile.ExtractToDirectory(zipPath, AppPaths.EngineDir, true);
            File.Delete(zipPath);

            SettingsService.Instance.IsCidelEngineInstalled = true;
            SettingsService.Instance.Save();
            onProgress("Ядро успешно установлено!");
        }
        catch (Exception ex)
        {
            onProgress($"Ошибка установки ядра: {ex.Message}");
        }
    }

    private async Task<bool> WaitForEngineReadyAsync(int timeoutSeconds = 15)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds)
        {
            try
            {
                var success = await Task.Run(() =>
                {
                    using var client = new System.Net.Sockets.TcpClient();
                    var result = client.BeginConnect("127.0.0.1", 2222, null, null);
                    var connected = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                    if (connected)
                    {
                        try
                        { client.EndConnect(result); }
                        catch { }
                        return true;
                    }
                    return false;
                });
                if (success)
                {
                    return true;
                }
            }
            catch { }
            await Task.Delay(500);
        }
        return false;
    }

    public async Task StartEngineAsync()
    {
        if (IsEngineRunning)
        {
            return;
        }

        if (!File.Exists(AppPaths.QemuExePath))
        {
            throw new FileNotFoundException("QEMU executable not found.", AppPaths.QemuExePath);
        }

        var info = new ProcessStartInfo
        {
            FileName = AppPaths.QemuExePath,
            Arguments = "-m 512M -smp 2 -netdev user,id=n1,hostfwd=tcp::2222-:22 -device virtio-net,netdev=n1 -nographic -drive file=rootfs.qcow2,format=qcow2",
            WorkingDirectory = AppPaths.EngineDir,
            CreateNoWindow = true,
            UseShellExecute = false
        };

        _qemuProcess = Process.Start(info);

        AppDomain.CurrentDomain.ProcessExit += (s, e) =>
        {
            try
            {
                if (_qemuProcess != null && !_qemuProcess.HasExited)
                {
                    _qemuProcess.Kill();
                }
            }
            catch { }
        };
        var isReady = await WaitForEngineReadyAsync();
        if (!isReady)
        {
            Debug.WriteLine("Тайм-аут ожидания загрузки CIDEL Engine (SSH порт 2222 недоступен).");
            IsEngineRunning = false;
            return;
        }

        try
        {
            EngineFileSystem = new SshFileSystemProvider(LocalEngineConnection);
            EngineFileSystem.Connect();
            IsEngineRunning = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Ошибка подключения SshFileSystemProvider: " + ex.Message);
            IsEngineRunning = false;
        }
    }
}
