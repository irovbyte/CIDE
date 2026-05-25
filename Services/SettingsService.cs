using System.Text.Json;
using System.Text.Json.Serialization;
using CIDE.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CIDE.Services;

[JsonSerializable(typeof(SettingsService))]
[JsonSerializable(typeof(SshConnectionInfo))]
[JsonSerializable(typeof(List<SshConnectionInfo>))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext
{
}

public partial class SettingsService : ObservableObject
{
    private static readonly string t_settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
    private static readonly JsonSerializerOptions t_jsonOptions = new() { WriteIndented = true, TypeInfoResolver = SettingsJsonContext.Default };

    public static SettingsService Instance => field ??= Load();

    [ObservableProperty]
    public partial bool ShowMinimap { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowWhitespaces { get; set; } = false;

    [ObservableProperty]
    public partial bool HighlightBrackets { get; set; } = true;

    [ObservableProperty]
    public partial bool AutoSave { get; set; } = true;

    [ObservableProperty]
    public partial bool UseLocalClangFormat { get; set; } = true;

    [ObservableProperty]
    public partial string ClangFormatPath { get; set; } = "";
    [ObservableProperty]
    public partial long LastUpdateId { get; set; } = 0;
    public List<string> RecentWorkspaces { get; set; } = [];
    public List<SshConnectionInfo> SavedSshConnections { get; set; } = [];

    public SettingsService()
    {
    }

    private static SettingsService Load()
    {
        try
        {
            if (File.Exists(t_settingsFilePath))
            {
                var json = File.ReadAllText(t_settingsFilePath);
                var settings = JsonSerializer.Deserialize<SettingsService>(json, t_jsonOptions);
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка загрузки настроек: {ex.Message}");
        }
        return new SettingsService();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, t_jsonOptions);
            var tempPath = t_settingsFilePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, t_settingsFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка сохранения настроек: {ex.Message}");
        }
    }
}
