using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using PixelForge.App.ViewModels;

namespace PixelForge.App.Views;

public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += OnWindowLoaded;
        KeyDown += OnWindowKeyDown;

        // Menu handlers
        MenuNew.Click += OnNewProjectClick;
        QuickNewButton.Click += OnNewProjectClick;
        MenuResizeCanvas.Click += OnResizeCanvasClick;
        QuickResizeButton.Click += OnResizeCanvasClick;
        StatusDimensionsButton.Click += OnResizeCanvasClick;
        MenuExit.Click += (_, _) => Close();
        MenuAbout.Click += OnAboutClick;

        MenuToggleTheme.Click += OnToggleThemeClick;
        QuickThemeButton.Click += OnToggleThemeClick;

        // Tool buttons setup
        ToolPencilButton.Click += (_, _) => SelectTool(CanvasToolType.Pencil);
        ToolEraserButton.Click += (_, _) => SelectTool(CanvasToolType.Eraser);
        ToolFillButton.Click += (_, _) => SelectTool(CanvasToolType.Fill);
        ToolEyedropperButton.Click += (_, _) => SelectTool(CanvasToolType.Eyedropper);
        ToolLineButton.Click += (_, _) => SelectTool(CanvasToolType.Line);
        ToolRectangleButton.Click += (_, _) => SelectTool(CanvasToolType.Rectangle);
        ToolEllipseButton.Click += (_, _) => SelectTool(CanvasToolType.Ellipse);
        ToolSelectionButton.Click += (_, _) => SelectTool(CanvasToolType.Selection);
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.ToolViewModel.PropertyChanged += OnToolViewModelPropertyChanged;
            UpdateActiveToolHighlight();
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (ViewModel != null)
        {
            ViewModel.ToolViewModel.PropertyChanged += OnToolViewModelPropertyChanged;
            UpdateActiveToolHighlight();
        }
    }

    private void OnToolViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ToolViewModel.ActiveTool))
        {
            UpdateActiveToolHighlight();
        }
    }

    private void SelectTool(CanvasToolType tool)
    {
        if (ViewModel != null)
        {
            ViewModel.ToolViewModel.SelectTool(tool);
        }
    }

    private IBrush GetBrush(string resourceKey, IBrush fallback)
    {
        if (this.TryFindResource(resourceKey, out var res) && res is IBrush b)
        {
            return b;
        }

        if (Application.Current != null && Application.Current.TryFindResource(resourceKey, out var appRes) && appRes is IBrush ab)
        {
            return ab;
        }

        return fallback;
    }

    private void UpdateActiveToolHighlight()
    {
        if (ViewModel == null)
        {
            return;
        }

        var activeTool = ViewModel.ToolViewModel.ActiveTool;
        var accentBrush = GetBrush("AccentPrimaryBrush", Brushes.DodgerBlue);
        var mutedTextBrush = GetBrush("TextSecondaryBrush", Brushes.SlateGray);
        var activeBackground = new SolidColorBrush(Color.FromArgb(40, 0, 210, 255));

        SetToolButtonVisuals(ToolPencilButton, activeTool == CanvasToolType.Pencil, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolEraserButton, activeTool == CanvasToolType.Eraser, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolFillButton, activeTool == CanvasToolType.Fill, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolEyedropperButton, activeTool == CanvasToolType.Eyedropper, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolLineButton, activeTool == CanvasToolType.Line, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolRectangleButton, activeTool == CanvasToolType.Rectangle, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolEllipseButton, activeTool == CanvasToolType.Ellipse, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolSelectionButton, activeTool == CanvasToolType.Selection, accentBrush, activeBackground, mutedTextBrush);
    }

    private static void SetToolButtonVisuals(Button btn, bool isActive, IBrush accentBrush, IBrush activeBackground, IBrush mutedBrush)
    {
        btn.Classes.Set("active", isActive);

        if (btn.Content is PathIcon icon)
        {
            icon.Foreground = isActive ? accentBrush : mutedBrush;
        }

        if (isActive)
        {
            btn.BorderBrush = accentBrush;
            btn.BorderThickness = new Thickness(1.5);
            btn.Background = activeBackground;
        }
        else
        {
            btn.BorderBrush = Brushes.Transparent;
            btn.BorderThickness = new Thickness(1.5);
            btn.Background = Brushes.Transparent;
        }
    }

    private async void OnNewProjectClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new NewCanvasDialog();
        var result = await dialog.ShowDialog<NewCanvasResult?>(this);
        if (result != null && ViewModel != null)
        {
            ViewModel.NewProject(result.Width, result.Height);
        }
    }

    private async void OnResizeCanvasClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        var currentW = ViewModel.CanvasViewModel.CanvasWidth;
        var currentH = ViewModel.CanvasViewModel.CanvasHeight;
        var dialog = new ResizeCanvasDialog(currentW, currentH);
        var result = await dialog.ShowDialog<ResizeCanvasResult?>(this);
        if (result != null)
        {
            ViewModel.ResizeCanvas(result.Width, result.Height, result.Anchor, result.Mode);
        }
    }

    private void OnToggleThemeClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ThemeService.ToggleTheme();
    }

    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var version = ViewModel != null ? "0.1.0" : "0.1.0";
        var aboutWindow = new Window
        {
            Title = $"О программе — PixelForge v{version}",
            Width = 420,
            Height = 290,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = GetBrush("SurfaceBaseBrush", new SolidColorBrush(Color.Parse("#1A1D27"))),
            Content = new Border
            {
                Padding = new Thickness(24),
                Child = new StackPanel
                {
                    Spacing = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "PixelForge",
                            FontSize = 24,
                            FontWeight = FontWeight.Bold,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Foreground = GetBrush("TextPrimaryBrush", Brushes.White)
                        },
                        new TextBlock
                        {
                            Text = $"Версия {version}",
                            FontSize = 14,
                            FontWeight = FontWeight.SemiBold,
                            Foreground = GetBrush("AccentPrimaryBrush", Brushes.DodgerBlue),
                            HorizontalAlignment = HorizontalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = "Современный и быстрый редактор пиксель-арта на .NET 8 и Avalonia UI с поддержкой SkiaSharp, слоев и симметрии.",
                            FontSize = 12,
                            Foreground = GetBrush("TextSecondaryBrush", Brushes.LightGray),
                            TextWrapping = TextWrapping.Wrap,
                            TextAlignment = TextAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = "Copyright © 2026 widlily-corp. Лицензия Apache-2.0.",
                            FontSize = 11,
                            Foreground = GetBrush("TextMutedBrush", Brushes.Gray),
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    }
                }
            }
        };

        await aboutWindow.ShowDialog(this);
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Don't trigger tool shortcuts if user is typing in a TextBox
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is TextBox)
        {
            return;
        }

        if (ViewModel == null)
        {
            return;
        }

        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        if ((ctrl && alt && e.Key == Key.C) || (ctrl && e.Key == Key.R))
        {
            e.Handled = true;
            OnResizeCanvasClick(this, new RoutedEventArgs());
            return;
        }

        if (ctrl)
        {
            switch (e.Key)
            {
                case Key.N:
                    e.Handled = true;
                    OnNewProjectClick(this, new RoutedEventArgs());
                    break;
                case Key.O:
                    e.Handled = true;
                    await ViewModel.OpenProjectAsync();
                    break;
                case Key.S:
                    e.Handled = true;
                    if (shift)
                    {
                        await ViewModel.SaveProjectAsAsync();
                    }
                    else
                    {
                        await ViewModel.SaveProjectAsync();
                    }
                    break;
                case Key.Z:
                    e.Handled = true;
                    if (shift)
                    {
                        ViewModel.HistoryViewModel.Redo();
                    }
                    else
                    {
                        ViewModel.HistoryViewModel.Undo();
                    }
                    break;
                case Key.Y:
                    e.Handled = true;
                    ViewModel.HistoryViewModel.Redo();
                    break;
                case Key.C:
                    e.Handled = true;
                    await ViewModel.CopyAsync();
                    break;
                case Key.X:
                    e.Handled = true;
                    await ViewModel.CutAsync();
                    break;
                case Key.V:
                    e.Handled = true;
                    await ViewModel.PasteAsync();
                    break;
                case Key.G:
                    e.Handled = true;
                    ViewModel.CanvasViewModel.ToggleGrid();
                    break;
                case Key.OemPlus:
                case Key.Add:
                    e.Handled = true;
                    ViewModel.CanvasViewModel.ZoomIn();
                    break;
                case Key.OemMinus:
                case Key.Subtract:
                    e.Handled = true;
                    ViewModel.CanvasViewModel.ZoomOut();
                    break;
            }
        }
        else
        {
            switch (e.Key)
            {
                case Key.B:
                    e.Handled = true;
                    SelectTool(CanvasToolType.Pencil);
                    break;
                case Key.E:
                    e.Handled = true;
                    SelectTool(CanvasToolType.Eraser);
                    break;
                case Key.G:
                    e.Handled = true;
                    SelectTool(CanvasToolType.Fill);
                    break;
                case Key.I:
                    e.Handled = true;
                    SelectTool(CanvasToolType.Eyedropper);
                    break;
                case Key.L:
                    e.Handled = true;
                    SelectTool(CanvasToolType.Line);
                    break;
                case Key.R:
                case Key.U:
                    e.Handled = true;
                    SelectTool(CanvasToolType.Rectangle);
                    break;
                case Key.C:
                    e.Handled = true;
                    SelectTool(CanvasToolType.Ellipse);
                    break;
                case Key.S:
                case Key.M:
                    e.Handled = true;
                    SelectTool(CanvasToolType.Selection);
                    break;
                case Key.X:
                    e.Handled = true;
                    ViewModel.PaletteViewModel.SwapColors();
                    break;
                case Key.OemOpenBrackets:
                    e.Handled = true;
                    ViewModel.ToolViewModel.DecreaseBrushSize();
                    break;
                case Key.OemCloseBrackets:
                    e.Handled = true;
                    ViewModel.ToolViewModel.IncreaseBrushSize();
                    break;
            }
        }
    }
}
