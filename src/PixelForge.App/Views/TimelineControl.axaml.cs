using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using PixelForge.App.ViewModels;
using PixelForge.Core.Models;

namespace PixelForge.App.Views;

public partial class TimelineControl : UserControl
{
    public TimelineControl()
    {
        InitializeComponent();
    }

    private void OnFrameCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is AnimationFrame frame && DataContext is TimelineViewModel vm)
        {
            int index = vm.Frames.IndexOf(frame);
            if (index >= 0)
            {
                vm.SelectFrame(index);
                UpdateCardHighlights();
            }
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is TimelineViewModel vm)
        {
            vm.Canvas.FrameChanged += _ => UpdateCardHighlights();
            UpdateCardHighlights();
        }
    }

    private void UpdateCardHighlights()
    {
        if (DataContext is not TimelineViewModel vm)
        {
            return;
        }

        var control = this.FindControl<ItemsControl>("FramesItemsControl");
        if (control == null)
        {
            return;
        }

        int activeIdx = vm.CurrentFrameIndex;
        for (int i = 0; i < control.ItemCount; i++)
        {
            var container = control.ContainerFromIndex(i);
            if (container != null)
            {
                var card = container as Border ?? (container as ContentPresenter)?.Child as Border;
                if (card != null)
                {
                    if (i == activeIdx)
                    {
                        if (!card.Classes.Contains("active"))
                        {
                            card.Classes.Add("active");
                        }
                    }
                    else
                    {
                        card.Classes.Remove("active");
                    }
                }
            }
        }
    }
}
