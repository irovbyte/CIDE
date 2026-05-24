namespace CIDE.Models;

public class SshConnectionInfo
{
    public string Name { get; set; } = "New Connection";
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "root";
    public bool UsePassword { get; set; }
    public string Password { get; set; } = "";
    public string KeyFilePath { get; set; } = "";
    public string RootPath { get; set; } = "/";
}
