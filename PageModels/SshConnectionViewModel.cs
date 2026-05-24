using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CIDE.Models;
using CIDE.Services;
using System.Linq;

namespace CIDE.PageModels;

public partial class SshConnectionViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<SshConnectionInfo> _connections = [];

    [ObservableProperty]
    private SshConnectionInfo? _selectedConnection;

    [ObservableProperty]
    private bool _isPasswordVisible;

    public SshConnectionViewModel()
    {
        LoadConnections();
    }

    private void LoadConnections()
    {
        Connections = new ObservableCollection<SshConnectionInfo>(SettingsService.Instance.SavedSshConnections);
        if (Connections.Any())
        {
            SelectedConnection = Connections.First();
        }
        else
        {
            CreateNewConnection();
        }
    }

    partial void OnSelectedConnectionChanged(SshConnectionInfo? value)
    {
        if (value != null)
        {
            IsPasswordVisible = value.UsePassword;
        }
    }

    [RelayCommand]
    private void CreateNewConnection()
    {
        var newConn = new SshConnectionInfo { Name = "New Connection" };
        Connections.Add(newConn);
        SelectedConnection = newConn;
    }

    [RelayCommand]
    private void DeleteConnection(SshConnectionInfo? info)
    {
        if (info == null)
            return;
        Connections.Remove(info);
        if (SelectedConnection == info)
        {
            SelectedConnection = Connections.FirstOrDefault();
        }
        SaveConnections();
    }

    public void SaveConnections()
    {
        SettingsService.Instance.SavedSshConnections = [.. Connections];
        SettingsService.Instance.Save();
    }
}
