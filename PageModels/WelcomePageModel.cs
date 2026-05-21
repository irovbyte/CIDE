using System.Collections.ObjectModel;
using CIDE.Models;
using CIDE.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CIDE.PageModels;

internal sealed partial class WelcomePageModel : ObservableObject
{
    public static ObservableCollection<RecentEntry> Recents => WorkspaceService.Recents;
    [ObservableProperty]
    public partial string ToolchainStatus { get; set; } = "Проверка компиляторов...";

    public WelcomePageModel()
    {
        WorkspaceService.LoadRecents();
        _ = CheckToolchainAsync();
    }

    private async Task CheckToolchainAsync() =>
        await ToolchainService.InstallMinGWAsync(msg => MainThread.BeginInvokeOnMainThread(() => ToolchainStatus = msg));

    [RelayCommand]
    public static async Task OpenFolderAsync()
    {
        var path = await WorkspaceService.PickFolderAsync();
        if (path != null)
        {
            await GoToWorkspaceAsync(path);
        }
    }

    [RelayCommand]
    public static async Task OpenSolutionAsync()
    {
        var path = await WorkspaceService.PickSolutionAsync();
        if (path != null)
        {
            await GoToWorkspaceAsync(path);
        }
    }

    [RelayCommand]
    public static async Task OpenRecentAsync(RecentEntry? entry)
    {
        if (entry != null && (Directory.Exists(entry.Path) || File.Exists(entry.Path)))
        {
            await GoToWorkspaceAsync(entry.Path);
        }
    }

    private static async Task GoToWorkspaceAsync(string path) =>
        await Shell.Current.GoToAsync($"//MainPage?path={Uri.EscapeDataString(path)}");
}
