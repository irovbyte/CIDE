using System;
using Avalonia.Controls;
using Avalonia.Threading;
using CIDE.Helpers;

namespace CIDE;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
            profileComboBox.SelectionChanged += (s, e) =>
            {
                if (profileComboBox.SelectedItem is ComboBoxItem item)
                {
                    terminalView?.StartProcess(item.Content?.ToString());
                }
            };
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

            model.PropertyChanged += async (s, e) =>
            {
                if (e.PropertyName == nameof(model.IsOutputVisible))
                {
                    var grid = this.FindControl<Grid>("EditorPanel");
                    if (grid != null)
                    {
                        var splitterRow = grid.RowDefinitions[1];
                        var terminalRow = grid.RowDefinitions[2];

                        if (model.IsOutputVisible)
                        {
                            _ = splitterRow.AnimateHeightAsync(8);
                            await terminalRow.AnimateHeightAsync(250);
                        }
                        else
                        {
                            _ = splitterRow.AnimateHeightAsync(0);
                            await terminalRow.AnimateHeightAsync(0);
                        }
                    }
                }
                else if (e.PropertyName == nameof(model.IsSidebarVisible))
                {
                    var mainGrid = this.FindControl<Grid>("MainAreaGrid");
                    if (mainGrid != null)
                    {
                        var sidebarCol = mainGrid.ColumnDefinitions[0];
                        var splitterCol = mainGrid.ColumnDefinitions[1];

                        if (model.IsSidebarVisible)
                        {
                            _ = splitterCol.AnimateWidthAsync(8);
                            await sidebarCol.AnimateWidthAsync(250);
                        }
                        else
                        {
                            _ = splitterCol.AnimateWidthAsync(0);
                            await sidebarCol.AnimateWidthAsync(0);
                        }
                    }
                }
                else if (e.PropertyName == nameof(model.CompilerOutput))
                {
                    var sv = this.FindControl<ScrollViewer>("OutputScrollViewer");
                    if (sv != null)
                    {
                        bool isNearBottom = sv.Offset.Y >= (sv.Extent.Height - sv.Viewport.Height - 20);
                        if (isNearBottom || sv.Extent.Height == 0)
                        {
                            Dispatcher.UIThread.Post(() => sv.ScrollToEnd(), DispatcherPriority.Loaded);
                        }
                    }
                }
                else if (e.PropertyName == nameof(model.IsZenMode))
                {
                    if (model.IsZenMode)
                    {
                        model.IsSidebarVisible = false;
                        model.IsOutputVisible = false;
                    }
                    else
                    {
                        model.IsSidebarVisible = true;
                    }
                }
            };
            this.KeyDown += (s, e) =>
            {
                if (e.Key == Avalonia.Input.Key.F11 && DataContext is PageModels.MainPageModel vm)
                {
                    vm.IsZenMode = !vm.IsZenMode;
                    e.Handled = true;
                }
            };
        }
    }

    protected override void OnClosed(System.EventArgs e)
    {
        base.OnClosed(e);
        if (CIDE.Services.WorkspaceService.CurrentProvider is System.IDisposable d)
        {
            d.Dispose();
        }
    }
}
