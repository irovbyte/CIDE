using CIDE.PageModels;
namespace CIDE.Pages;
internal partial class WelcomePage : ContentPage
{
    public WelcomePage(WelcomePageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
