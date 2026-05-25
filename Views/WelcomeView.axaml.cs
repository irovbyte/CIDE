using Avalonia.Controls;
using Avalonia.Input;
using CIDE.Helpers;

namespace CIDE.Views;

public partial class WelcomeView : UserControl
{
    public WelcomeView()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnPointerPressedAsync, Avalonia.Interactivity.RoutingStrategies.Bubble, true);
    }

    private async void OnPointerPressedAsync(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Control control)
        {
            if (control is Button || (control is Border b && b.Classes.Contains("card")))
            {
                await control.BounceClickAsync();
            }
            else if (control.Parent is Button btn)
            {
                await btn.BounceClickAsync();
            }
        }
    }
}
