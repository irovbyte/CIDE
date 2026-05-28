using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaEdit;
using CIDE.Helpers;
using CIDE.Models;
using CIDE.PageModels;

namespace CIDE;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, DropAsync);
        var outputTabControl = this.FindControl<TabControl>("OutputTabControl");
        var terminalControlsPanel = this.FindControl<StackPanel>("TerminalControlsPanel");
        var profileComboBox = this.FindControl<ComboBox>("ProfileComboBox");
        var newTermBtn = this.FindControl<Button>("NewTermBtn");
        var clearBtn = this.FindControl<Button>("ClearBtn");
        var terminalView = this.FindControl<Views.TerminalView>("MainTerminalView");
        var outputEditor = this.FindControl<TextEditor>("OutputEditor");
        if (outputEditor != null)
        {
            outputEditor.Options.EnableHyperlinks = false;
            outputEditor.TextArea.Caret.CaretBrush = Avalonia.Media.Brushes.Transparent;
        }

        if (outputTabControl != null && terminalControlsPanel != null)
        {
            outputTabControl.SelectionChanged += (s, e) =>
                terminalControlsPanel.IsVisible = outputTabControl.SelectedIndex == 0;
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

        newTermBtn?.Click += (s, e) =>
        {
            var content = (profileComboBox?.SelectedItem as ComboBoxItem)?.Content?.ToString();
            terminalView?.StartProcess(content);
        };

        clearBtn?.Click += (s, e) =>
        {
            var content = (profileComboBox?.SelectedItem as ComboBoxItem)?.Content?.ToString();
        };

        if (App.Services != null)
        {
            var model = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<MainPageModel>(App.Services);
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
                    var editor = this.FindControl<TextEditor>("OutputEditor");
                    if (editor != null)
                    {
                        var doc = editor.Document;
                        var newText = model.CompilerOutput;
                        if (string.IsNullOrEmpty(newText) || newText.Length < doc.TextLength)
                        {
                            doc.Text = newText ?? string.Empty;
                        }
                        else
                        {
                            var addedPart = newText[doc.TextLength..];
                            if (addedPart.Length > 0)
                            {
                                doc.Insert(doc.TextLength, addedPart);
                            }
                        }
                        Dispatcher.UIThread.Post(() => editor.ScrollToEnd(), DispatcherPriority.Render);
                    }
                }
                else if (e.PropertyName == nameof(model.IsCommandPaletteVisible))
                {
                    if (model.IsCommandPaletteVisible)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            var tb = this.FindControl<TextBox>("CommandPaletteSearchBox");
                            _ = (tb?.Focus());
                        }, DispatcherPriority.Loaded);
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

            KeyDown += (s, e) =>
            {
                if (e.Key == Key.F11 && DataContext is MainPageModel vm)
                {
                    vm.IsZenMode = !vm.IsZenMode;
                    e.Handled = true;
                }
            };
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (DataContext is MainPageModel model)
        {
            foreach (var root in model.Sidebar.WorkspaceRoots)
            {
                if (root.Provider is IDisposable d)
                {
                    d.Dispose();
                }
            }
        }
    }

    [Obsolete]
    private async void DropAsync(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles();
            if (files != null && DataContext is MainPageModel vm)
            {
                foreach (var file in files)
                {
                    if (file is Avalonia.Platform.Storage.IStorageFile storageFile)
                    {
                        var path = storageFile.Path.LocalPath;
                        if (File.Exists(path))
                        {
                            await vm.OpenFileAsync(path);
                        }
                    }
                }
            }
        }
    }

    private async void OnErrorDoubleTappedAsync(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainPageModel vm)
        {
            return;
        }

        var listBox = this.FindControl<ListBox>("ErrorListBox");
        if (listBox?.SelectedItem is not BuildError error)
        {
            return;
        }

        if (!error.HasLocation)
        {
            return;
        }

        var matchingTab = vm.AllTabs.FirstOrDefault(t =>
            Path.GetFileName(t.FilePath)
                .Equals(error.File, StringComparison.OrdinalIgnoreCase));

        if (matchingTab == null)
        {
            if (!string.IsNullOrEmpty(vm.WorkspacePath) &&
                Directory.Exists(vm.WorkspacePath))
            {
                var found = Directory.GetFiles(
                    vm.WorkspacePath, error.File,
                    SearchOption.AllDirectories).FirstOrDefault();

                if (found != null)
                {
                    await vm.OpenFileAsync(found);
                }
                else
                {
                    return;
                }
            }
            else
            {
                return;
            }
        }
        else
        {
            await vm.ActivateTabAsync(matchingTab);
        }
        if (this.FindControl<Views.EditorView>("EditorViewControl") is { } editorView)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var editor = editorView.GetActiveEditor();
                if (editor?.Document != null)
                {
                    var targetLine = Math.Max(1, Math.Min(error.Line, editor.Document.LineCount));
                    editor.ScrollToLine(targetLine);
                    editor.TextArea.Caret.Line = targetLine;
                    editor.TextArea.Caret.Column = 1;
                    _ = editor.Focus();
                }
            }, DispatcherPriority.Loaded);
        }
    }
}
