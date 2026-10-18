using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PixelForge.App.Views;

/// <summary>
/// Result of the NewCanvasDialog containing chosen canvas dimensions.
/// </summary>
public sealed record NewCanvasResult(int Width, int Height);

public partial class NewCanvasDialog : Window
{
    public NewCanvasResult? Result { get; private set; }

    public NewCanvasDialog()
    {
        InitializeComponent();

        Preset32Button.Click += (_, _) => SetPreset(32, 32);
        Preset64Button.Click += (_, _) => SetPreset(64, 64);
        Preset128Button.Click += (_, _) => SetPreset(128, 128);

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

        if (width < 16 || width > 256 || height < 16 || height > 256)
        {
            ErrorText.Text = "Размеры холста должны быть от 16 до 256 пикселей.";
            return;
        }

        Result = new NewCanvasResult(width, height);
        Close(Result);
    }
}
