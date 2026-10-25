using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PixelForge.App.ViewModels;

namespace PixelForge.App.Views;

public partial class SettingsWindow : Window
{
    private SettingsViewModel? _subscribedVm;

    public SettingsWindow()
    {
        InitializeComponent();
        AddHandler(InputElement.KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_subscribedVm != null)
        {
            _subscribedVm.RequestClose -= OnRequestClose;
            _subscribedVm = null;
        }

        if (DataContext is SettingsViewModel vm)
        {
            _subscribedVm = vm;
            _subscribedVm.RequestClose += OnRequestClose;
        }
    }

    private void OnRequestClose(object? sender, EventArgs e)
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
