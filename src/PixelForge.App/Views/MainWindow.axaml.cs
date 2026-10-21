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
        AddHandler(InputElement.KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);

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
        ToolDitherButton.Click += (_, _) => SelectTool(CanvasToolType.DitherBrush);
        ToolPatternBrushButton.Click += (_, _) => SelectTool(CanvasToolType.PatternBrush);
        ToolFillButton.Click += (_, _) => SelectTool(CanvasToolType.Fill);
        ToolMagicWandButton.Click += (_, _) => SelectTool(CanvasToolType.MagicWand);
        ToolGradientButton.Click += (_, _) => SelectTool(CanvasToolType.Gradient);
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
        MainCanvasControl.Focus();
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
        SetToolButtonVisuals(ToolDitherButton, activeTool == CanvasToolType.DitherBrush, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolPatternBrushButton, activeTool == CanvasToolType.PatternBrush, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolFillButton, activeTool == CanvasToolType.Fill, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolMagicWandButton, activeTool == CanvasToolType.MagicWand, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolGradientButton, activeTool == CanvasToolType.Gradient, accentBrush, activeBackground, mutedTextBrush);
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
        var version = "0.1.1";
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

    private static bool MatchesKey(KeyEventArgs e, Key key, PhysicalKey physicalKey, char? ruChar = null)
    {
        if (e.Key == key || e.PhysicalKey == physicalKey)
        {
            return true;
        }

        if (ruChar.HasValue && !string.IsNullOrEmpty(e.KeySymbol))
        {
            if (char.ToLowerInvariant(e.KeySymbol[0]) == ruChar.Value)
            {
                return true;
            }
        }

        return false;
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Don't trigger shortcuts if user is typing in a TextBox or editable text element
        var focused = FocusManager?.GetFocusedElement();
        if (focused is TextBox or AutoCompleteBox)
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

        // F10: Seamless Tiled Mode toggle
        if (!ctrl && !alt && !shift && (e.Key == Key.F10 || e.PhysicalKey == PhysicalKey.F10))
        {
            e.Handled = true;
            ViewModel.ToggleTiledMode();
            return;
        }

        // Shift + P: Toggle Pixel Perfect
        if (!ctrl && !alt && shift && MatchesKey(e, Key.P, PhysicalKey.P, 'з'))
        {
            e.Handled = true;
            ViewModel.ToolViewModel.TogglePixelPerfect();
            return;
        }

        // Do not intercept Space key so canvas panning can handle it cleanly
        if (!ctrl && !alt && !shift && (e.Key == Key.Space || e.PhysicalKey == PhysicalKey.Space))
        {
            return;
        }

        // Ctrl + Alt combinations (Canvas Resize)
        if (ctrl && alt && (MatchesKey(e, Key.C, PhysicalKey.C, 'с') || MatchesKey(e, Key.R, PhysicalKey.R, 'к')))
        {
            e.Handled = true;
            OnResizeCanvasClick(this, new RoutedEventArgs());
            return;
        }

        if (ctrl)
        {
            if (MatchesKey(e, Key.N, PhysicalKey.N, 'т'))
            {
                e.Handled = true;
                OnNewProjectClick(this, new RoutedEventArgs());
            }
            else if (MatchesKey(e, Key.O, PhysicalKey.O, 'щ'))
            {
                e.Handled = true;
                await ViewModel.OpenProjectAsync();
            }
            else if (MatchesKey(e, Key.S, PhysicalKey.S, 'ы'))
            {
                e.Handled = true;
                if (shift)
                {
                    await ViewModel.SaveProjectAsAsync();
                }
                else
                {
                    await ViewModel.SaveProjectAsync();
                }
            }
            else if (MatchesKey(e, Key.Z, PhysicalKey.Z, 'я'))
            {
                e.Handled = true;
                if (shift)
                {
                    ViewModel.HistoryViewModel.Redo();
                }
                else
                {
                    ViewModel.HistoryViewModel.Undo();
                }
                MainCanvasControl?.MarkCompositeDirty();
            }
            else if (MatchesKey(e, Key.Y, PhysicalKey.Y, 'н'))
            {
                e.Handled = true;
                ViewModel.HistoryViewModel.Redo();
                MainCanvasControl?.MarkCompositeDirty();
            }
            else if (MatchesKey(e, Key.C, PhysicalKey.C, 'с'))
            {
                e.Handled = true;
                await ViewModel.CopyAsync();
            }
            else if (MatchesKey(e, Key.X, PhysicalKey.X, 'ч'))
            {
                e.Handled = true;
                await ViewModel.CutAsync();
            }
            else if (MatchesKey(e, Key.V, PhysicalKey.V, 'м'))
            {
                e.Handled = true;
                await ViewModel.PasteAsync();
            }
            else if (MatchesKey(e, Key.A, PhysicalKey.A, 'ф'))
            {
                e.Handled = true;
                ViewModel.SelectAll();
            }
            else if (MatchesKey(e, Key.D, PhysicalKey.D, 'в'))
            {
                e.Handled = true;
                ViewModel.Deselect();
            }
            else if (MatchesKey(e, Key.G, PhysicalKey.G, 'п'))
            {
                e.Handled = true;
                ViewModel.CanvasViewModel.ToggleGrid();
            }
            else if (MatchesKey(e, Key.OemPlus, PhysicalKey.Equal) || e.Key == Key.Add || e.PhysicalKey == PhysicalKey.NumPadAdd || e.KeySymbol == "+")
            {
                e.Handled = true;
                ViewModel.CanvasViewModel.ZoomIn();
            }
            else if (MatchesKey(e, Key.OemMinus, PhysicalKey.Minus) || e.Key == Key.Subtract || e.PhysicalKey == PhysicalKey.NumPadSubtract || e.KeySymbol == "-")
            {
                e.Handled = true;
                ViewModel.CanvasViewModel.ZoomOut();
            }
            else if (MatchesKey(e, Key.D0, PhysicalKey.Digit0) || e.Key == Key.NumPad0 || e.PhysicalKey == PhysicalKey.NumPad0)
            {
                e.Handled = true;
                ViewModel.CanvasViewModel.ResetZoom();
            }
        }
        else if (!alt)
        {
            if (MatchesKey(e, Key.B, PhysicalKey.B, 'и'))
            {
                e.Handled = true;
                SelectTool(CanvasToolType.Pencil);
            }
            else if (MatchesKey(e, Key.E, PhysicalKey.E, 'у'))
            {
                e.Handled = true;
                SelectTool(CanvasToolType.Eraser);
            }
            else if (MatchesKey(e, Key.D, PhysicalKey.D, 'в'))
            {
                e.Handled = true;
                SelectTool(CanvasToolType.DitherBrush);
            }
            else if (MatchesKey(e, Key.P, PhysicalKey.P, 'з'))
            {
                e.Handled = true;
                SelectTool(CanvasToolType.PatternBrush);
            }
            else if (MatchesKey(e, Key.F, PhysicalKey.F, 'а') || MatchesKey(e, Key.G, PhysicalKey.G, 'п'))
            {
                e.Handled = true;
                SelectTool(CanvasToolType.Fill);
            }
            else if (MatchesKey(e, Key.W, PhysicalKey.W, 'ц'))
            {
                e.Handled = true;
                SelectTool(CanvasToolType.MagicWand);
            }
            else if (MatchesKey(e, Key.T, PhysicalKey.T, 'е'))
            {
                e.Handled = true;
                SelectTool(CanvasToolType.Gradient);
            }
            else if (MatchesKey(e, Key.I, PhysicalKey.I, 'ш'))
            {
                e.Handled = true;
                SelectTool(CanvasToolType.Eyedropper);
            }
            else if (MatchesKey(e, Key.L, PhysicalKey.L, 'д'))
            {
                e.Handled = true;
                SelectTool(CanvasToolType.Line);
            }
            else if (MatchesKey(e, Key.R, PhysicalKey.R, 'к') || MatchesKey(e, Key.U, PhysicalKey.U, 'г'))
            {
                e.Handled = true;
                SelectTool(CanvasToolType.Rectangle);
            }
            else if (MatchesKey(e, Key.C, PhysicalKey.C, 'с'))
            {
                e.Handled = true;
                SelectTool(CanvasToolType.Ellipse);
            }
            else if (MatchesKey(e, Key.S, PhysicalKey.S, 'ы') || MatchesKey(e, Key.M, PhysicalKey.M, 'ь'))
            {
                e.Handled = true;
                SelectTool(CanvasToolType.Selection);
            }
            else if (MatchesKey(e, Key.X, PhysicalKey.X, 'ч'))
            {
                e.Handled = true;
                ViewModel.PaletteViewModel.SwapColors();
            }
            else if (MatchesKey(e, Key.H, PhysicalKey.H, 'р'))
            {
                e.Handled = true;
                ViewModel.FlipActiveLayerHorizontal();
            }
            else if (MatchesKey(e, Key.V, PhysicalKey.V, 'м'))
            {
                e.Handled = true;
                ViewModel.FlipActiveLayerVertical();
            }
            else if (MatchesKey(e, Key.OemOpenBrackets, PhysicalKey.BracketLeft, 'х'))
            {
                e.Handled = true;
                ViewModel.ToolViewModel.DecreaseBrushSize();
            }
            else if (MatchesKey(e, Key.OemCloseBrackets, PhysicalKey.BracketRight, 'ъ'))
            {
                e.Handled = true;
                ViewModel.ToolViewModel.IncreaseBrushSize();
            }
            else if (e.Key == Key.Delete || e.PhysicalKey == PhysicalKey.Delete || e.Key == Key.Back || e.PhysicalKey == PhysicalKey.Backspace)
            {
                e.Handled = true;
                ViewModel.DeleteSelectionOrClearLayer();
            }
        }
    }
}
