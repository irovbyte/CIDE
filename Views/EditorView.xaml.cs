using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Maui.Controls;

namespace CIDE.Views;

public partial class EditorView : ContentView
{
    public EditorView()
    {
        InitializeComponent();
        var blazorWebView = new BlazorWebView
        {
            HostPage = "index.html"
        };
        blazorWebView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(MainEditor)
        });
        EditorHost.Children.Add(blazorWebView);
    }
}
