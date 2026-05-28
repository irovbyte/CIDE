using System.Text.Json;
using System.Text.Json.Serialization;
using CIDE.Models;
using CommunityToolkit.Mvvm.ComponentModel;
namespace CIDE.Services;

[JsonSerializable(typeof(SettingsService))]
[JsonSerializable(typeof(SshConnectionInfo))]
[JsonSerializable(typeof(List<SshConnectionInfo>))]
[JsonSerializable(typeof(WorkspaceSession))]
[JsonSerializable(typeof(Dictionary<string, WorkspaceSession>))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext
{
}
public class WorkspaceSession
{
    public List<string> OpenedFiles { get; set; } = [];
    public string ActiveFile { get; set; } = "";
}
public partial class SettingsService : ObservableObject
{
    [ObservableProperty] public partial bool IsCidelEngineInstalled { get; set; } = false;
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
    public Dictionary<string, WorkspaceSession> WorkspaceSessions { get; set; } = [];
    public SettingsService()
    {
    }
    private static SettingsService Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var json = File.ReadAllText(AppPaths.SettingsFile);
                var settings = JsonSerializer.Deserialize<SettingsService>(json, t_jsonOptions);
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"РћС€РёР±РєР° Р·Р°РіСЂСѓР·РєРё РЅР°СЃС‚СЂРѕРµРє: {ex.Message}");
        }
        return new SettingsService();
    }
    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, t_jsonOptions);
            var tempPath = AppPaths.SettingsFile + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, AppPaths.SettingsFile, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"РћС€РёР±РєР° СЃРѕС…СЂР°РЅРµРЅРёСЏ РЅР°СЃС‚СЂРѕРµРє: {ex.Message}");
        }
    }
}
