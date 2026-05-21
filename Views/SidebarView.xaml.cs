using CIDE.Models;
using CIDE.PageModels;
using Microsoft.Maui.Controls;

namespace CIDE.Views;

public partial class SidebarView : ContentView
{
    public SidebarView() => InitializeComponent();
    private void OnTreeSelectionChangedAsync(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection is { Count: > 0 } selection && selection[0] is FileNode node && BindingContext is MainPageModel model)
        {
            model.SelectNodeCommand.Execute(node);
        }
        if (sender is CollectionView cv)
        {
            cv.SelectedItem = null;
        }
    }
}
