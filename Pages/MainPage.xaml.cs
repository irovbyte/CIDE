using CIDE.Models;
using CIDE.PageModels;
using System.ComponentModel;
using Microsoft.AspNetCore.Components.WebView.Maui;

namespace CIDE.Pages;

internal sealed partial class MainPage : ContentPage
{
    private readonly MainPageModel _model;
    private bool _isOutputAnimPlaying;

    public MainPage(MainPageModel model)
    {
        InitializeComponent();
        _model = model;
        BindingContext = _model;

        _model.PropertyChanged += OnModelPropertyChanged;
        _model.FlatTree.CollectionChanged += OnFlatTreeChanged;
        ToolbarPanel.Opacity = 0;
        ToolbarPanel.TranslationY = -45;

        SidebarPanel.Opacity = 0;
        SidebarPanel.TranslationX = -260;

        EditorPanel.Opacity = 0;
        EditorPanel.TranslationX = 60;
        EditorPanel.Scale = 0.98;
    }
    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_model.IsOutputVisible))
        {
            if (!_isOutputAnimPlaying)
            {
                _ = AnimateTerminalToggleAsync(_model.IsOutputVisible);
            }
        }
    }
    private async Task AnimateTerminalToggleAsync(bool isVisible)
    {
        _isOutputAnimPlaying = true;

        if (isVisible)
        {
            OutputPanel.Opacity = 0;
            OutputPanel.TranslationY = 150;
            _ = await Task.WhenAll(
                EditorPanel.TranslateToAsync(0, -10, 120, Easing.CubicOut),
                EditorPanel.ScaleToAsync(0.99, 120, Easing.CubicOut)
            );
            _ = await Task.WhenAll(
                OutputPanel.FadeToAsync(1, 280, Easing.CubicOut),
                OutputPanel.TranslateToAsync(0, 0, 280, Easing.SpringOut),
                EditorPanel.TranslateToAsync(0, 4, 180, Easing.SpringOut),
                EditorPanel.ScaleToAsync(1.0, 180, Easing.SpringOut)
            );
            _ = await EditorPanel.TranslateToAsync(0, 0, 100, Easing.CubicIn);
        }
        else
        {
            _ = await Task.WhenAll(
                EditorPanel.TranslateToAsync(0, 12, 140, Easing.CubicIn),
                EditorPanel.ScaleToAsync(1.01, 140, Easing.CubicIn)
            );

            _ = await Task.WhenAll(
                EditorPanel.TranslateToAsync(0, 0, 240, Easing.SpringOut),
                EditorPanel.ScaleToAsync(1.0, 240, Easing.SpringOut)
            );
        }

        _isOutputAnimPlaying = false;
    }
    private void OnFlatTreeChanged(object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Add)
        {
            return;
        }

        _ = AnimateNewTreeItemsAsync(e.NewItems?.Count ?? 0);
    }

    private static async Task AnimateNewTreeItemsAsync(int count)
    {
        await Task.Delay(30);
        for (var i = 0; i < count; i++)
        {
            await Task.Delay(20);
        }
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = RunChainIntroAnimationAsync();
    }

    private async Task RunChainIntroAnimationAsync()
    {
        _ = await Task.WhenAll(
            ToolbarPanel.FadeToAsync(1, 350, Easing.CubicOut),
            ToolbarPanel.TranslateToAsync(0, 0, 350, Easing.CubicOut)
        );
        var btnBuild = ToolbarPanel.FindByName<View>("BtnBuild");
        var btnRun = ToolbarPanel.FindByName<View>("BtnRun");
        var btns = new View[] { btnBuild, btnRun }.Where(b => b != null).ToArray();
        foreach (var b in btns)
        {
            b.Opacity = 0;
            b.TranslationX = 12;
        }

        var btnTasks = btns.Select(async (b, idx) =>
        {
            await Task.Delay(idx * 50);
            _ = await Task.WhenAll(
                b.FadeToAsync(1, 250, Easing.CubicOut),
                b.TranslateToAsync(0, 0, 250, Easing.CubicOut)
            );
        });
        var sidebarTask = Task.WhenAll(
            SidebarPanel.FadeToAsync(1, 450, Easing.CubicOut),
            SidebarPanel.TranslateToAsync(0, 0, 450, Easing.CubicOut)
        );

        await Task.WhenAll(Task.WhenAll(btnTasks), sidebarTask);
        _ = await Task.WhenAll(
            EditorPanel.FadeToAsync(1, 300, Easing.CubicOut),
            EditorPanel.TranslateToAsync(18, 0, 180, Easing.CubicOut),
            EditorPanel.ScaleToAsync(0.99, 180, Easing.CubicOut)
        );
        _ = await Task.WhenAll(
            EditorPanel.TranslateToAsync(0, 0, 350, Easing.SpringOut),
            EditorPanel.ScaleToAsync(1.0, 350, Easing.SpringOut)
        );
    }
}
