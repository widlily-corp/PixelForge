using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PixelForge.Core.Localization;

namespace PixelForge.App.Views;

/// <summary>
/// Result of the NewCanvasDialog containing chosen canvas dimensions.
/// </summary>
public sealed record NewCanvasResult(int Width, int Height);

public partial class NewCanvasDialog : Window
{
    public LocalizationService Loc => LocalizationService.Instance;

    public NewCanvasResult? Result { get; private set; }

    public NewCanvasDialog()
    {
        DataContext = this;
        InitializeComponent();

        Preset32Button.Click += (_, _) => SetPreset(32, 32);
        Preset64Button.Click += (_, _) => SetPreset(64, 64);
        Preset128Button.Click += (_, _) => SetPreset(128, 128);
        Preset512Button.Click += (_, _) => SetPreset(512, 512);

        CancelButton.Click += OnCancelClick;
        CreateButton.Click += OnCreateClick;
    }

    private void SetPreset(int width, int height)
    {
        WidthInput.Value = width;
        HeightInput.Value = height;
        ErrorText.Text = string.Empty;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close(null);
    }

    private void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        int width = (int)Math.Round(WidthInput.Value ?? 32);
        int height = (int)Math.Round(HeightInput.Value ?? 32);

        if (width < 1 || width > 4096 || height < 1 || height > 4096)
        {
            ErrorText.Text = Loc["Dlg_NewCanvasError"];
            return;
        }

        Result = new NewCanvasResult(width, height);
        Close(Result);
    }
}
