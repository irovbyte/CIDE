using Avalonia.Controls;

namespace CIDE;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var closeBtn = this.FindControl<Button>("CloseBtn");
        var maxBtn = this.FindControl<Button>("MaximizeBtn");
        var minBtn = this.FindControl<Button>("MinimizeBtn");
        closeBtn?.Click += (_, _) => Close();
        maxBtn?.Click += (_, _) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        minBtn?.Click += (_, _) => WindowState = WindowState.Minimized;

        if (App.Services != null)
        {
            DataContext = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainPageModel>(App.Services);
        }
    }

    protected override void OnPointerPressed(Avalonia.Input.PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        if (pos.Y <= 35 && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }
}
