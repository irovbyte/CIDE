using Avalonia.Controls;
using Avalonia.Input;

namespace CIDE;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Smart Terminal Logic
        var outputTabControl = this.FindControl<TabControl>("OutputTabControl");
        var terminalControlsPanel = this.FindControl<StackPanel>("TerminalControlsPanel");
        var profileComboBox = this.FindControl<ComboBox>("ProfileComboBox");
        var newTermBtn = this.FindControl<Button>("NewTermBtn");
        var clearBtn = this.FindControl<Button>("ClearBtn");
        var terminalView = this.FindControl<Views.TerminalView>("MainTerminalView");

        if (outputTabControl != null && terminalControlsPanel != null)
        {
            outputTabControl.SelectionChanged += (s, e) =>
            {
                terminalControlsPanel.IsVisible = outputTabControl.SelectedIndex == 0;
            };
        }

        if (profileComboBox != null)
        {
            profileComboBox.SelectedIndex = 0;
            profileComboBox.SelectionChanged += (s, e) => terminalView?.StartProcess(profileComboBox.SelectedItem?.ToString());
        }

        if (newTermBtn != null)
        {
            newTermBtn.Click += (s, e) => terminalView?.StartProcess(profileComboBox?.SelectedItem?.ToString());
        }

        if (clearBtn != null)
        {
            clearBtn.Click += (s, e) => terminalView?.StartProcess(profileComboBox?.SelectedItem?.ToString());
        }

        if (App.Services != null)
        {
            var model = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<PageModels.MainPageModel>(App.Services);
            DataContext = model;
            
            model.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(model.IsOutputVisible))
                {
                    var grid = this.FindControl<Grid>("EditorPanel");
                    if (grid != null)
                    {
                        grid.RowDefinitions[2].Height = model.IsOutputVisible ? new GridLength(250) : new GridLength(0);
                        grid.RowDefinitions[1].Height = model.IsOutputVisible ? new GridLength(8) : new GridLength(0);
                    }
                }
            };
        }
    }

}
