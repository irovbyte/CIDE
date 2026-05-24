using Avalonia.Controls;
using Avalonia.Input;
using CIDE.Models;
using CIDE.PageModels;
namespace CIDE.Views;

public partial class SidebarView : UserControl
{
    public SidebarView() => InitializeComponent();
    private void FileNode_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && control.DataContext is FileNode node)
        {
            if (DataContext is MainPageModel vm)
            {
                var listBox = this.FindControl<ListBox>("FileTreeListBox");
                if (listBox != null)
                {
                    listBox.SelectedItem = node;
                }
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    vm.SelectNodeCommand.Execute(node);
                }
            }
        }
    }
}
