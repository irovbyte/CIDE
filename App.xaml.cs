using Microsoft.Extensions.DependencyInjection;
namespace CIDE;
internal partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
