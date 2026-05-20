using System.Text;
using Renci.SshNet;

namespace CIDE.Services;

public sealed partial class SshWorkspaceService(string host, string username, string password) : IDisposable
{
    private SftpClient? _sftpClient;
    private SshClient? _sshClient;
    public static string RemoteCacheDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CIDE", "remote_cache");

    public void Connect()
    {
        _sftpClient = new SftpClient(host, username, password);
        _sftpClient.Connect();

        _sshClient = new SshClient(host, username, password);
        _sshClient.Connect();
    }

    public void Disconnect()
    {
        _sftpClient?.Disconnect();
        _sshClient?.Disconnect();
    }

    public void Dispose()
    {
        _sftpClient?.Dispose();
        _sshClient?.Dispose();
    }

    public async Task<string> ReadRemoteFileAsync(string remotePath)
    {
        if (_sftpClient == null || !_sftpClient.IsConnected)
        {
            Connect();
        }
        using var ms = new MemoryStream();
        await Task.Run(() => _sftpClient!.DownloadFile(remotePath, ms));

        ms.Position = 0;
        using var reader = new StreamReader(ms, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    public async Task SaveRemoteFileAsync(string remotePath, string content)
    {
        if (_sftpClient == null || !_sftpClient.IsConnected)
        {
            Connect();
        }

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await Task.Run(() => _sftpClient!.UploadFile(ms, remotePath, true));
    }

    public string ExecuteCommand(string command)
    {
        if (_sshClient == null || !_sshClient.IsConnected)
        {
            Connect();
        }

        var cmd = _sshClient!.CreateCommand(command);
        return cmd.Execute();
    }
}
