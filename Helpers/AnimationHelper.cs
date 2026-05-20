namespace CIDE.Helpers;
internal static class AnimationHelper
{
    public static async Task WaveClickAsync(this View clicked, params View[] neighbors)
    {
        var clickTask = ClickPhysicsAsync(clicked);
        var neighborTasks = neighbors.Select(async (n, index) =>
        {
            await Task.Delay(50 * (index + 1));
            await NudgeAsync(n);
        });
        await Task.WhenAll(new[] { clickTask }.Concat(neighborTasks));
    }
    private static async Task ClickPhysicsAsync(View view)
    {
        _ = await Task.WhenAll(view.ScaleToAsync(0.85, 100, Easing.CubicOut), view.FadeToAsync(0.8, 100));
        _ = await Task.WhenAll(view.ScaleToAsync(1.0, 300, Easing.SpringOut), view.FadeToAsync(1.0, 150));
    }
    private static async Task NudgeAsync(View view)
    {
        _ = await Task.WhenAll(view.TranslateToAsync(15, 0, 120, Easing.CubicOut), view.ScaleToAsync(0.95, 120, Easing.CubicOut));
        _ = await Task.WhenAll(view.TranslateToAsync(0, 0, 300, Easing.SpringOut), view.ScaleToAsync(1.0, 300, Easing.SpringOut));
    }
    public static async Task FadeInWithShiftAsync(this View view, uint duration = 500)
    {
        view.Opacity = 0;
        view.TranslationY = 30;
        view.IsVisible = true;
        _ = await Task.WhenAll(view.FadeToAsync(1, duration, Easing.CubicOut), view.TranslateToAsync(0, 0, duration, Easing.CubicOut));
    }
}
