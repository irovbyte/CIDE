using Avalonia.Controls;
using Avalonia.Interactivity;
using CIDE.Models;
using CIDE.PageModels;

namespace CIDE.Views;

public partial class SshConnectionDialog : Window
{
    private readonly SshConnectionViewModel _viewModel;

    public SshConnectionDialog()
    {
        InitializeComponent();
        _viewModel = new SshConnectionViewModel();
        DataContext = _viewModel;
        var titleBar = this.FindControl<Avalonia.Controls.Border>("TitleBarBorder");
        if (titleBar != null)
        {
            titleBar.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    BeginMoveDrag(e);
                }
            };
        }
    }

    private void Connect_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedConnection != null)
        {
            _viewModel.SaveConnections();
            Close(_viewModel.SelectedConnection);
        }
    }

    private void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedConnection != null)
        {
            _viewModel.DeleteConnectionCommand.Execute(_viewModel.SelectedConnection);
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
