using CommunityToolkit.Mvvm.ComponentModel;

namespace CIDE.Models;

public partial class CommandPaletteItem : ObservableObject
{
    [ObservableProperty]
    public partial string FileName { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string FullPath { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string DisplayPath { get; set; } = string.Empty;
}
