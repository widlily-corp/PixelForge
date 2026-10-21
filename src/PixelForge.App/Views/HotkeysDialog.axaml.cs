using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace PixelForge.App.Views;

/// <summary>
/// Modal cheat sheet dialog presenting keyboard shortcuts and hotkeys for PixelForge.
/// </summary>
public partial class HotkeysDialog : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HotkeysDialog"/> class.
    /// </summary>
    public HotkeysDialog()
    {
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
