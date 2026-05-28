using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CIDE.Models;
using Renci.SshNet;
using Renci.SshNet.Sftp;
namespace CIDE.Services;

public class SshFileSystemProvider(SshConnectionInfo connectionInfo) : IFileSystemProvider, IDisposable
{
    private readonly SshConnectionInfo _connectionInfo = connectionInfo;
    private SshClient? _sshClient;
    private SftpClient? _sftpClient;
    private bool _disposed;
    public void Connect()
    {
        _sftpClient?.Dispose();
        _sshClient?.Dispose();
        AuthenticationMethod authMethod;
        if (_connectionInfo.UsePassword)
        {
            authMethod = new PasswordAuthenticationMethod(_connectionInfo.Username, _connectionInfo.Password);
        }
        else
        {
            var keyPath = _connectionInfo.KeyFilePath;
            if (string.IsNullOrWhiteSpace(keyPath))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var rsa = Path.Combine(home, ".ssh", "id_rsa");
                var ed25519 = Path.Combine(home, ".ssh", "id_ed25519");
                keyPath = File.Exists(ed25519) ? ed25519 : rsa;
            }
            if (File.Exists(keyPath))
            {
                var keyFile = new PrivateKeyFile(keyPath);
                authMethod = new PrivateKeyAuthenticationMethod(_connectionInfo.Username, keyFile);
            }
            else
            {
                throw new InvalidOperationException($"Ключ не найден: {keyPath}");
            }
        }
        var connectionInfo = new ConnectionInfo(
            _connectionInfo.Host,
            _connectionInfo.Port,
            _connectionInfo.Username,
            authMethod
        );
        _sshClient = new SshClient(connectionInfo);
        _sshClient.Connect();
        _sftpClient = new SftpClient(connectionInfo);
        _sftpClient.Connect();
    }
    private async Task EnsureConnectedAsync()
    {
        if (_sftpClient == null || !_sftpClient.IsConnected)
        {
            await Task.Run(Connect);
        }
    }
    public SshClient? GetSshClient() => _sshClient;
    public async Task<string> ReadFileAsync(string path)
    {
        await EnsureConnectedAsync();
        return await Task.Run(() =>
        {
            using var stream = new MemoryStream();
            _sftpClient!.DownloadFile(path, stream);
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });
    }
    public async Task SaveFileAsync(string path, string content)
    {
        await EnsureConnectedAsync();
        await Task.Run(() =>
        {
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, leaveOpen: true);
            writer.Write(content);
            writer.Flush();
            stream.Position = 0;
            _sftpClient!.UploadFile(stream, path, canOverride: true);
        });
    }
    public async Task<bool> DirectoryExistsAsync(string path)
    {
        await EnsureConnectedAsync();
        return await Task.Run(() =>
        {
            try
            {
                return _sftpClient!.Exists(path) && _sftpClient.GetAttributes(path).IsDirectory;
            }
            catch
            {
                return false;
            }
        });
    }
    public async Task<bool> FileExistsAsync(string path)
    {
        await EnsureConnectedAsync();
        return await Task.Run(() =>
        {
            try
            {
                return _sftpClient!.Exists(path) && _sftpClient.GetAttributes(path).IsRegularFile;
            }
            catch
            {
                return false;
            }
        });
    }
    public async Task<IEnumerable<FileNodeData>> GetDirectoriesAsync(string path)
    {
        await EnsureConnectedAsync();
        return await Task.Run(() =>
        {
            if (!_sftpClient!.Exists(path))
            {
                return [];
            }
            var files = _sftpClient.ListDirectory(path);
            return files
                .Where(f => f.IsDirectory && f.Name != "." && f.Name != ".." && !f.Name.StartsWith('.'))
                .OrderBy(f => f.Name)
                .Select(f => new FileNodeData { Name = f.Name, FullPath = f.FullName });
        });
    }
    public async Task<IEnumerable<FileNodeData>> GetFilesAsync(string path)
    {
        await EnsureConnectedAsync();
        return await Task.Run(() =>
        {
            if (!_sftpClient!.Exists(path))
            {
                return [];
            }
            var files = _sftpClient.ListDirectory(path);
            return files
                .Where(f => f.IsRegularFile)
                .OrderBy(f => f.Name)
                .Select(f => new FileNodeData { Name = f.Name, FullPath = f.FullName });
        });
    }
    public async Task CreateFileAsync(string path)
    {
        await EnsureConnectedAsync();
        await Task.Run(() => _sftpClient!.Create(path).Dispose());
    }
    public async Task CreateDirectoryAsync(string path)
    {
        await EnsureConnectedAsync();
        await Task.Run(() => _sftpClient!.CreateDirectory(path));
    }
    public async Task DeleteAsync(string path, bool isDirectory)
    {
        await EnsureConnectedAsync();
        await Task.Run(() =>
        {
            if (isDirectory)
            {
                _ = _sshClient!.RunCommand($"rm -rf \"{path}\"");
            }
            else
            {
                _sftpClient!.DeleteFile(path);
            }
        });
    }
    public async Task RenameAsync(string oldPath, string newPath)
    {
        await EnsureConnectedAsync();
        await Task.Run(() => _sftpClient!.RenameFile(oldPath, newPath));
    }
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _sftpClient?.Dispose();
        _sshClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}
