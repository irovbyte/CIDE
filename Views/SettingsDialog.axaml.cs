using Avalonia.Controls;
using CIDE.Services;

namespace CIDE.Views;

public partial class SettingsDialog : Window
{
    public SettingsDialog()
    {
        InitializeComponent();
        var settings = SettingsService.Instance;
        MinimapCheckBox.IsChecked = settings.ShowMinimap;
        WhitespacesCheckBox.IsChecked = settings.ShowWhitespaces;
        BracketsCheckBox.IsChecked = settings.HighlightBrackets;
        AutoSaveCheckBox.IsChecked = settings.AutoSave;

        MinimapCheckBox.IsCheckedChanged += (s, e) => settings.ShowMinimap = MinimapCheckBox.IsChecked ?? false;
        WhitespacesCheckBox.IsCheckedChanged += (s, e) => settings.ShowWhitespaces = WhitespacesCheckBox.IsChecked ?? false;
        BracketsCheckBox.IsCheckedChanged += (s, e) => settings.HighlightBrackets = BracketsCheckBox.IsChecked ?? false;
        AutoSaveCheckBox.IsCheckedChanged += (s, e) => settings.AutoSave = AutoSaveCheckBox.IsChecked ?? false;

        UseLocalClangFormatCheckBox.IsChecked = settings.UseLocalClangFormat;
        UseLocalClangFormatCheckBox.IsCheckedChanged += (s, e) => settings.UseLocalClangFormat = UseLocalClangFormatCheckBox.IsChecked ?? false;
        ClangFormatPathTextBox.Text = settings.ClangFormatPath;
        ClangFormatPathTextBox.TextChanged += (s, e) => settings.ClangFormatPath = ClangFormatPathTextBox.Text ?? "";

        CloseButton.Click += (s, e) => Close();
        var titleBar = this.FindControl<Border>("TitleBarBorder");
        titleBar?.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    BeginMoveDrag(e);
                }
            };
    }
}
