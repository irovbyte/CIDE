using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace CIDE.Views;

public partial class InputDialog : Window
{
    public string? Result { get; private set; }

    public InputDialog()
    {
        InitializeComponent();
    }

    public InputDialog(string title, string message, string defaultText = "") : this()
    {
        TitleBlock.Text = title;
        MessageBlock.Text = message;
        InputBox.Text = defaultText;
        this.Opened += (s, e) =>
        {
            InputBox.Focus();
            if (!string.IsNullOrEmpty(defaultText))
            {
                InputBox.SelectAll();
            }
        };
    }

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        Result = InputBox.Text?.Trim();
        Close(Result);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close(null);
    }

    private void InputBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Ok_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.Escape)
        {
            Cancel_Click(this, new RoutedEventArgs());
        }
    }
}
