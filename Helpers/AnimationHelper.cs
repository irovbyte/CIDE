using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using Avalonia.Threading;
namespace CIDE.Helpers;

public static class AnimationHelper
{
    private static readonly Easing t_springEase = new SplineEasing(0.175, 0.885, 0.32, 1.275);
    private static readonly Easing t_pressEase = new SplineEasing(0.25, 0.1, 0.25, 1.0);
    public static async Task BounceClickAsync(this Control control)
    {
        if (control.RenderTransform is not ScaleTransform st)
        {
            st = new ScaleTransform(1, 1);
            control.RenderTransform = st;
            control.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        }
        var animationDown = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(120),
            Easing = t_pressEase,
            FillMode = FillMode.Forward,
            Children = { new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(ScaleTransform.ScaleXProperty, 0.94d), new Setter(ScaleTransform.ScaleYProperty, 0.94d) } } }
        };
        var animationUp = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(350),
            Easing = t_springEase,
            FillMode = FillMode.Forward,
            Children = { new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(ScaleTransform.ScaleXProperty, 1.0d), new Setter(ScaleTransform.ScaleYProperty, 1.0d) } } }
        };
        try
        {
            await animationDown.RunAsync(st);
            await animationUp.RunAsync(st);
        }
        catch { }
    }
    public static async Task ShakeAsync(this Control control)
    {
        if (control.RenderTransform is not TranslateTransform tt)
        {
            tt = new TranslateTransform(0, 0);
            control.RenderTransform = tt;
        }
        var shake = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(400),
            Easing = new SineEaseInOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0.2d), Setters = { new Setter(TranslateTransform.XProperty, -5d) } },
                new KeyFrame { Cue = new Cue(0.4d), Setters = { new Setter(TranslateTransform.XProperty, 5d) } },
                new KeyFrame { Cue = new Cue(0.6d), Setters = { new Setter(TranslateTransform.XProperty, -5d) } },
                new KeyFrame { Cue = new Cue(0.8d), Setters = { new Setter(TranslateTransform.XProperty, 5d) } },
                new KeyFrame { Cue = new Cue(1.0d), Setters = { new Setter(TranslateTransform.XProperty, 0d) } },
            }
        };
        try
        { await shake.RunAsync(tt); }
        catch { }
    }
    public static async Task SlideAndFadeInAsync(this Control control, int delayMs = 0)
    {
        if (control.RenderTransform is not TranslateTransform tt)
        {
            tt = new TranslateTransform(0, -15);
            control.RenderTransform = tt;
        }
        else
        {
            tt.Y = -15;
        }
        control.Opacity = 0;
        var animTranslate = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(250),
            Delay = TimeSpan.FromMilliseconds(delayMs),
            Easing = t_springEase,
            FillMode = FillMode.Forward,
            Children = { new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(TranslateTransform.YProperty, 0d) } } }
        };
        var animOpacity = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(250),
            Delay = TimeSpan.FromMilliseconds(delayMs),
            FillMode = FillMode.Forward,
            Children = { new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(Visual.OpacityProperty, 1.0d) } } }
        };
        try
        {
            _ = animTranslate.RunAsync(tt);
            await animOpacity.RunAsync(control);
        }
        catch { }
    }
    private static async Task AnimateValueAsync(double start, double end, int durationMs, Easing easing, Action<double> setter)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < durationMs)
        {
            var progress = (double)sw.ElapsedMilliseconds / durationMs;
            var eased = easing.Ease(progress);
            var current = start + ((end - start) * eased);
            await Dispatcher.UIThread.InvokeAsync(() => setter(current));
            await Task.Delay(10);
        }
        await Dispatcher.UIThread.InvokeAsync(() => setter(end));
    }
    public static Task AnimateHeightAsync(this RowDefinition row, double targetHeight, int durationMs = 350)
    {
        var startHeight = row.Height.IsAbsolute ? row.Height.Value : 0;
        return AnimateValueAsync(startHeight, targetHeight, durationMs, new QuarticEaseInOut(),
            val => row.Height = new GridLength(val, GridUnitType.Pixel));
    }
    public static Task AnimateWidthAsync(this ColumnDefinition col, double targetWidth, int durationMs = 350)
    {
        var startWidth = col.Width.IsAbsolute ? col.Width.Value : 0;
        return AnimateValueAsync(startWidth, targetWidth, durationMs, new QuarticEaseInOut(),
            val => col.Width = new GridLength(val, GridUnitType.Pixel));
    }
    public static Task AnimateHeightAsync(this Control control, double targetHeight, int durationMs = 350)
    {
        var startHeight = double.IsNaN(control.Height) ? control.Bounds.Height : control.Height;
        return AnimateValueAsync(startHeight, targetHeight, durationMs, new QuarticEaseInOut(),
            val => control.Height = val);
    }
    public static Task AnimateWidthAsync(this Control control, double targetWidth, int durationMs = 350)
    {
        var startWidth = double.IsNaN(control.Width) ? control.Bounds.Width : control.Width;
        return AnimateValueAsync(startWidth, targetWidth, durationMs, new QuarticEaseInOut(),
            val => control.Width = val);
    }
}
