using CommunityToolkit.Mvvm.ComponentModel;

namespace CIDE.Models;

public partial class GlobalSearchResult : ObservableObject
{
    [ObservableProperty]
    public partial string FilePath { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string DisplayName { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string Context { get; set; } = string.Empty;
    [ObservableProperty]
    public partial int LineNumber { get; set; }
}
