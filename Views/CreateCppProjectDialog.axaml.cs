using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CIDE.Services;
namespace CIDE.Views;

public class CppProjectResult
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsCpp { get; set; }
}
public partial class CreateCppProjectDialog : Window
{
    public CreateCppProjectDialog()
    {
        InitializeComponent();
        ProjectPathTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CIDE_Projects");
        BrowseButton.Click += BrowseButtonClickAsync;
        CancelButton.Click += (s, e) => Close();
        CreateButton.Click += CreateButton_Click;
        var titleBar = this.FindControl<Border>("TitleBarBorder");
        titleBar?.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    BeginMoveDrag(e);
                }
            };
    }
    private async void BrowseButtonClickAsync(object? sender, RoutedEventArgs e)
    {
        var provider = StorageProvider;
        var result = await provider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "Выберите папку для проекта",
            AllowMultiple = false
        });
        if (result != null && result.Count > 0)
        {
            ProjectPathTextBox.Text = result[0].Path.LocalPath;
        }
    }
    private void CreateButton_Click(object? sender, RoutedEventArgs e)
    {
        var name = ProjectNameTextBox.Text?.Trim();
        var basePath = ProjectPathTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(basePath))
        {
            return;
        }
        var fullPath = Path.Combine(basePath, name);
        var isCpp = LanguageComboBox.SelectedIndex == 1;
        Close(new CppProjectResult { Name = name, FullPath = fullPath, IsCpp = isCpp });
    }
}
