using CommunityToolkit.Mvvm.ComponentModel;

namespace CIDE.Services;

public partial class SettingsService : ObservableObject
{
    private static SettingsService? t_instance;
    public static SettingsService Instance => t_instance ??= new SettingsService();

    [ObservableProperty]
    public partial bool ShowMinimap { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowWhitespaces { get; set; } = false;

    [ObservableProperty]
    public partial bool HighlightBrackets { get; set; } = true;

    [ObservableProperty]
    public partial bool AutoSave { get; set; } = true;
    
    [ObservableProperty]
    public partial long LastUpdateId { get; set; } = 0;
    
    public List<string> RecentWorkspaces { get; set; } = [];

    private SettingsService()
    {
    }

#pragma warning disable CA1822
    public void Save()
    {
    }
#pragma warning restore CA1822
}
