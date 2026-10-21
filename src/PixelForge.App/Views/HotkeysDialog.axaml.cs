using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PixelForge.Core.Localization;

namespace PixelForge.App.Views;

/// <summary>
/// Modal cheat sheet dialog presenting keyboard shortcuts and hotkeys for PixelForge.
/// </summary>
public partial class HotkeysDialog : Window
{
    public LocalizationService Loc => LocalizationService.Instance;

    /// <summary>
    /// Initializes a new instance of the <see cref="HotkeysDialog"/> class.
    /// </summary>
    public HotkeysDialog()
    {
        DataContext = this;
        InitializeComponent();

        CloseButton.Click += OnCloseClick;
        AddHandler(InputElement.KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }
}
