using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace CIDE.Views;

public partial class ConfirmDialog : Window
{
    public bool Result { get; private set; }

    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public ConfirmDialog(string title, string message) : this()
    {
        TitleBlock.Text = title;
        MessageBlock.Text = message;
        this.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Ok_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Cancel_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        };
    }

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        Result = true;
        Close(Result);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Result = false;
        Close(Result);
    }
}
