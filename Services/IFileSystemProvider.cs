using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CIDE.Models;
namespace CIDE.Services;

public interface IFileSystemProvider
{
    public Task<string> ReadFileAsync(string path);
    public Task SaveFileAsync(string path, string content);
    public Task<bool> DirectoryExistsAsync(string path);
    public Task<bool> FileExistsAsync(string path);
    public Task<IEnumerable<FileNodeData>> GetDirectoriesAsync(string path);
    public Task<IEnumerable<FileNodeData>> GetFilesAsync(string path);
    public Task CreateFileAsync(string path);
    public Task CreateDirectoryAsync(string path);
    public Task DeleteAsync(string path, bool isDirectory);
    public Task RenameAsync(string oldPath, string newPath);
}
public class FileNodeData
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
}
public class LocalFileSystemProvider : IFileSystemProvider
{
    public Task<string> ReadFileAsync(string path) => File.ReadAllTextAsync(path);
    public Task SaveFileAsync(string path, string content) => File.WriteAllTextAsync(path, content);
    public Task<bool> DirectoryExistsAsync(string path) => Task.FromResult(Directory.Exists(path));
    public Task<bool> FileExistsAsync(string path) => Task.FromResult(File.Exists(path));
    public Task<IEnumerable<FileNodeData>> GetDirectoriesAsync(string path)
    {
        var dir = new DirectoryInfo(path);
        if (!dir.Exists)
        {
            return Task.FromResult<IEnumerable<FileNodeData>>([]);
        }
        var dirs = dir.GetDirectories()
            .Where(d => !d.Name.StartsWith('.') && d.Name != "bin" && d.Name != "obj")
            .OrderBy(d => d.Name)
            .Select(d => new FileNodeData { Name = d.Name, FullPath = d.FullName });
        return Task.FromResult(dirs);
    }
    public Task<IEnumerable<FileNodeData>> GetFilesAsync(string path)
    {
        var dir = new DirectoryInfo(path);
        if (!dir.Exists)
        {
            return Task.FromResult<IEnumerable<FileNodeData>>([]);
        }
        var files = dir.GetFiles()
            .OrderBy(f => f.Name)
            .Select(f => new FileNodeData { Name = f.Name, FullPath = f.FullName });
        return Task.FromResult(files);
    }
    public Task CreateFileAsync(string path)
    {
        File.Create(path).Dispose();
        return Task.CompletedTask;
    }
    public Task CreateDirectoryAsync(string path)
    {
        _ = Directory.CreateDirectory(path);
        return Task.CompletedTask;
    }
    public Task DeleteAsync(string path, bool isDirectory)
    {
        if (isDirectory)
        {
            Directory.Delete(path, true);
        }
        else
        {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }
    public Task RenameAsync(string oldPath, string newPath)
    {
        if (Directory.Exists(oldPath))
        {
            Directory.Move(oldPath, newPath);
        }
        else if (File.Exists(oldPath))
        {
            File.Move(oldPath, newPath);
        }
        return Task.CompletedTask;
    }
}
