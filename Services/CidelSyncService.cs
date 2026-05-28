using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Renci.SshNet;

namespace CIDE.Services;

public static class CidelSyncService
{
    private static SshClient? GetClient()
    {
        var conn = CidelEngineService.Instance.LocalEngineConnection;
        var method = new PasswordAuthenticationMethod(conn.Username, conn.Password);
        var connectionInfo = new ConnectionInfo(conn.Host, conn.Port, conn.Username, method);
        return new SshClient(connectionInfo);
    }

    private static SftpClient? GetSftpClient()
    {
        var conn = CidelEngineService.Instance.LocalEngineConnection;
        var method = new PasswordAuthenticationMethod(conn.Username, conn.Password);
        var connectionInfo = new ConnectionInfo(conn.Host, conn.Port, conn.Username, method);
        return new SftpClient(connectionInfo);
    }

    private static void RunSshCommand(string command)
    {
        try
        {
            using var client = GetClient();
            if (client == null)
            {
                return;
            }

            client.Connect();
            _ = client.RunCommand(command);
        }
        catch { }
    }

    public static async Task PushToCidelAsync(string localWindowsPath, string content, string projectName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        if (!CidelEngineService.Instance.IsEngineRunning)
        {
            return;
        }

        await Task.Run(() =>
        {
            try
            {
                using var sftp = GetSftpClient();
                if (sftp == null)
                {
                    return;
                }

                sftp.Connect();
                var fileName = Path.GetFileName(localWindowsPath);
                var remoteDir = $"/home/cide/shadow/{projectName}";
                if (!CidelEngineService.Instance.IsEngineRunning)
                {
                    return;
                }

                RunSshCommand($"mkdir -p {remoteDir}");

                var remotePath = $"{remoteDir}/{fileName}";
                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
                sftp.UploadFile(ms, remotePath, true);
            }
            catch (Exception)
            {
            }
        });
    }

    public static async Task<string> GetCacheSizeAsync()
    {
        return !CidelEngineService.Instance.IsEngineRunning
            ? "0 KB"
            : await Task.Run(() =>
        {
            try
            {
                using var client = GetClient();
                if (client == null)
                {
                    return "0 KB";
                }

                client.Connect();
                _ = client.RunCommand("mkdir -p /home/cide/shadow");
                var cmd = client.RunCommand("du -sh /home/cide/shadow | awk '{print $1}'");
                return string.IsNullOrWhiteSpace(cmd.Result) ? "0 KB" : cmd.Result.Trim();
            }
            catch
            {
                return "0 KB";
            }
        });
    }

    public static async Task ClearCacheAsync()
    {
        if (!CidelEngineService.Instance.IsEngineRunning)
        {
            return;
        }

        await Task.Run(() =>
        {
            try
            {
                using var client = GetClient();
                if (client == null)
                {
                    return;
                }

                client.Connect();
                _ = client.RunCommand("rm -rf /home/cide/shadow/*");
            }
            catch { }
        });
    }
}
