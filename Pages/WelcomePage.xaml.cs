namespace CIDE.Pages;

internal sealed partial class WelcomePage : ContentPage
{
    public WelcomePage(WelcomePageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await AnimationHelper.FadeInWithShiftAsync(CardFolder, 600);
        await AnimationHelper.FadeInWithShiftAsync(CardSolution, 800);
    }
    private async void OnCardFolderTappedAsync(object? sender, TappedEventArgs e) =>
        await AnimationHelper.WaveClickAsync(CardFolder);
    private async void OnCardSolutionTappedAsync(object? sender, TappedEventArgs e) =>
        await AnimationHelper.WaveClickAsync(CardSolution);
}
