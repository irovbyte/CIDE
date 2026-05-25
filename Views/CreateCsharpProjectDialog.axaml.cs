using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CIDE.Services;

namespace CIDE.Views;

public class CsharpProjectResult
{
    public string ProjectName { get; set; } = "";
    public string SolutionName { get; set; } = "";
    public string BasePath { get; set; } = "";
    public bool SameFolder { get; set; }
    public string TemplateShortName { get; set; } = "";
}

public class TemplateViewModel(DotnetTemplateInfo original)
{
    public DotnetTemplateInfo Original { get; } = original;

    public string Name => Original.Name;
    public string ShortName => Original.ShortName;
    public string IconText => Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries) switch
    {
        { Length: 0 } => "C#",
        [var first, var second, ..] => $"{first[0]}{second[0]}".ToUpperInvariant(),
        [var first, ..] => first[..Math.Min(2, first.Length)].ToUpperInvariant(),
        _ => "C#"
    };

    public List<string> TagList
    {
        get
        {
            var list = new List<string>();
            if (!string.IsNullOrEmpty(Original.Language))
            {
                list.Add(Original.Language.Replace("[", "").Replace("]", "").Trim());
            }
            if (!string.IsNullOrEmpty(Original.Tags))
            {
                list.AddRange(Original.Tags.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()));
            }
            if (list.Count == 0)
            {
                list.Add("C#");
            }
            return [.. list.Distinct()];
        }
    }

    public string Description => $"Проект для создания {Name}";
}

public partial class CreateCsharpProjectDialog : Window
{
    public CreateCsharpProjectDialog()
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

        ProjectNameTextBox.TextChanged += (s, e) =>
        {
            if (!SameFolderCheckBox.IsChecked.GetValueOrDefault())
            {
                SolutionNameTextBox.Text = ProjectNameTextBox.Text;
            }
        };

        _ = LoadTemplatesAsync();
    }

    private async Task LoadTemplatesAsync()
    {
        var templates = await DotnetTemplateService.GetInstalledTemplatesAsync();
        var viewModels = templates.Select(t => new TemplateViewModel(t)).ToList();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            TemplatesListBox.ItemsSource = viewModels;
            LoadingSpinner.IsVisible = false;
            if (viewModels.Count > 0)
            {
                TemplatesListBox.SelectedIndex = 0;
            }
        });
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
        var projName = ProjectNameTextBox.Text?.Trim();
        var slnName = SolutionNameTextBox.Text?.Trim();
        var basePath = ProjectPathTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(projName) || string.IsNullOrEmpty(basePath) || TemplatesListBox.SelectedItem is not TemplateViewModel templateVm)
        {
            return;
        }

        if (string.IsNullOrEmpty(slnName))
        {
            slnName = projName;
        }

        Close(new CsharpProjectResult
        {
            ProjectName = projName,
            SolutionName = slnName,
            BasePath = basePath,
            SameFolder = SameFolderCheckBox.IsChecked ?? false,
            TemplateShortName = templateVm.ShortName
        });
    }
}
