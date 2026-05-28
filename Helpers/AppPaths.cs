using System;
using System.IO;

namespace CIDE.Helpers;

public static class AppPaths
{
    public static string RootDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CIDE");
    public static string ConfigDir => Path.Combine(RootDir, "Config");
    public static string SettingsFile => Path.Combine(ConfigDir, "settings.json");
    public static string EngineDir => Path.Combine(RootDir, "Engine");
    public static string QemuExePath => Path.Combine(EngineDir, "qemu-system-x86_64.exe");
    public static string CacheDir => Path.Combine(RootDir, "Cache");
    public static string UpdatesDir => Path.Combine(CacheDir, "Updates");

    public static string UserDataDir => Path.Combine(RootDir, "UserData");

    public static void EnsureDirectoriesExist()
    {
        _ = Directory.CreateDirectory(ConfigDir);
        _ = Directory.CreateDirectory(EngineDir);
        _ = Directory.CreateDirectory(CacheDir);
        _ = Directory.CreateDirectory(UpdatesDir);
        _ = Directory.CreateDirectory(UserDataDir);
    }
}
