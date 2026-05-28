using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using CIDE.Models;
using CIDE.PageModels;

namespace CIDE.Views;

public partial class SidebarView : UserControl
{
    private int _staggerIndex;
    private readonly DispatcherTimer _staggerResetTimer;

    public SidebarView()
    {
        InitializeComponent();

        _staggerResetTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _staggerResetTimer.Tick += (s, e) =>
        {
            _staggerIndex = 0;
            _staggerResetTimer.Stop();
        };

        FileTreeView.ContainerPrepared += FileTreeView_ContainerPrepared;

        SizeChanged += (s, e) =>
        {
            var fullTb = this.FindControl<StackPanel>("FullToolbar");
            var compactTb = this.FindControl<Button>("CompactToolbar");

            if (fullTb != null && compactTb != null)
            {
                var isNarrow = e.NewSize.Width < 220;
                fullTb.IsVisible = !isNarrow;
                compactTb.IsVisible = isNarrow;
            }
        };
    }

    private void FileTreeView_ContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is TreeViewItem item)
        {
            var delay = TimeSpan.FromMilliseconds(_staggerIndex * 25);
            _staggerIndex++;
            _staggerResetTimer.Stop();
            _staggerResetTimer.Start();
            item.Opacity = 0;
            var translateTransform = new TranslateTransform(0, -10);
            item.RenderTransform = translateTransform;

            var opacityTransition = new DoubleTransition
            {
                Property = OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(200),
                Easing = new CubicEaseOut()
            };
            var transformTransition = new DoubleTransition
            {
                Property = TranslateTransform.YProperty,
                Duration = TimeSpan.FromMilliseconds(250),
                Easing = new CubicEaseOut()
            };

            _ = DispatcherTimer.RunOnce(() =>
            {
                item.Transitions ??= [];

                if (!item.Transitions.Contains(opacityTransition))
                {
                    item.Transitions.Add(opacityTransition);
                }

                translateTransform.Transitions ??= [];

                if (!translateTransform.Transitions.Contains(transformTransition))
                {
                    translateTransform.Transitions.Add(transformTransition);
                }

                item.Opacity = 1;
                translateTransform.Y = 0;
            }, delay);
        }
    }

    private void FileNode_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (sender is not Control { DataContext: FileNode node })
        {
            return;
        }

        if (DataContext is not SidebarViewModel vm)
        {
            return;
        }

        e.Handled = true;
        vm.SelectNodeCommand.Execute(node);
    }
}
