using System;
using System.ComponentModel;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using PixelForge.App.ViewModels;
using PixelForge.Core.Localization;
using PixelForge.Core.Models;

namespace PixelForge.App.Views;

public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += OnWindowLoaded;
        SizeChanged += OnMainWindowSizeChanged;
        AddHandler(InputElement.KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);

        // Menu handlers
        MenuNew.Click += OnNewProjectClick;
        QuickNewButton.Click += OnNewProjectClick;
        MenuResizeCanvas.Click += OnResizeCanvasClick;
        QuickResizeButton.Click += OnResizeCanvasClick;
        StatusDimensionsButton.Click += OnResizeCanvasClick;
        MenuExit.Click += (_, _) => Close();
        MenuAbout.Click += OnAboutClick;
        MenuHotkeys.Click += OnHotkeysClick;
        MenuSettingsFile.Click += OnSettingsClick;
        MenuSettingsEdit.Click += OnSettingsClick;

        MenuToggleTheme.Click += OnToggleThemeClick;
        QuickThemeButton.Click += OnToggleThemeClick;

        MenuLangRu.Click += (_, _) => ViewModel?.SetLanguage(AppLanguage.Russian);
        MenuLangEn.Click += (_, _) => ViewModel?.SetLanguage(AppLanguage.English);
        MenuRecentFiles.SubmenuOpened += (_, _) => RebuildRecentMenu();

        // Drag & Drop
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnWindowDragOver);
        AddHandler(DragDrop.DropEvent, OnWindowDrop);

        // Tool buttons setup
        ToolPencilButton.Click += (_, _) => SelectTool(CanvasToolType.Pencil);
        ToolEraserButton.Click += (_, _) => SelectTool(CanvasToolType.Eraser);
        ToolDitherButton.Click += (_, _) => SelectTool(CanvasToolType.DitherBrush);
        ToolPatternBrushButton.Click += (_, _) => SelectTool(CanvasToolType.PatternBrush);
        ToolShadeBrushButton.Click += (_, _) => SelectTool(CanvasToolType.ShadeBrush);
        ToolSprayBrushButton.Click += (_, _) => SelectTool(CanvasToolType.SprayBrush);
        ToolFillButton.Click += (_, _) => SelectTool(CanvasToolType.Fill);
        ToolMagicWandButton.Click += (_, _) => SelectTool(CanvasToolType.MagicWand);
        ToolGradientButton.Click += (_, _) => SelectTool(CanvasToolType.Gradient);
        ToolEyedropperButton.Click += (_, _) => SelectTool(CanvasToolType.Eyedropper);
        ToolLineButton.Click += (_, _) => SelectTool(CanvasToolType.Line);
        ToolRectangleButton.Click += (_, _) => SelectTool(CanvasToolType.Rectangle);
        ToolEllipseButton.Click += (_, _) => SelectTool(CanvasToolType.Ellipse);
        ToolSelectionButton.Click += (_, _) => SelectTool(CanvasToolType.Selection);
    }

    private MainViewModel? _subscribedMainVm;

    private void SubscribeToViewModel(MainViewModel? vm)
    {
        if (_subscribedMainVm != null)
        {
            _subscribedMainVm.ToolViewModel.PropertyChanged -= OnToolViewModelPropertyChanged;
            _subscribedMainVm.ShowHotkeysRequested -= OnShowHotkeysRequested;
            _subscribedMainVm.ShowColorRampRequested -= OnShowColorRampRequested;
            _subscribedMainVm.ShowSettingsRequested -= OnShowSettingsRequested;
            _subscribedMainVm = null;
        }

        if (vm != null)
        {
            _subscribedMainVm = vm;
            _subscribedMainVm.ToolViewModel.PropertyChanged += OnToolViewModelPropertyChanged;
            _subscribedMainVm.ShowHotkeysRequested += OnShowHotkeysRequested;
            _subscribedMainVm.ShowColorRampRequested += OnShowColorRampRequested;
            _subscribedMainVm.ShowSettingsRequested += OnShowSettingsRequested;
            UpdateActiveToolHighlight();
        }
    }

    private void OnShowHotkeysRequested(object? sender, EventArgs e)
    {
        OnHotkeysClick(this, new RoutedEventArgs());
    }

    private void OnShowColorRampRequested(object? sender, EventArgs e)
    {
        OnColorRampClick(this, new RoutedEventArgs());
    }

    private void OnShowSettingsRequested(object? sender, EventArgs e)
    {
        OnSettingsClick(this, new RoutedEventArgs());
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        SubscribeToViewModel(ViewModel);
        MainCanvasControl.Focus();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        SubscribeToViewModel(ViewModel);
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
        var activeBackground = GetBrush("AccentGlowBrush", new SolidColorBrush(Color.FromArgb(40, 0, 210, 255)));

        SetToolButtonVisuals(ToolPencilButton, activeTool == CanvasToolType.Pencil, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolEraserButton, activeTool == CanvasToolType.Eraser, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolDitherButton, activeTool == CanvasToolType.DitherBrush, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolPatternBrushButton, activeTool == CanvasToolType.PatternBrush, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolShadeBrushButton, activeTool == CanvasToolType.ShadeBrush, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolSprayBrushButton, activeTool == CanvasToolType.SprayBrush, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolFillButton, activeTool == CanvasToolType.Fill, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolMagicWandButton, activeTool == CanvasToolType.MagicWand, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolGradientButton, activeTool == CanvasToolType.Gradient, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolEyedropperButton, activeTool == CanvasToolType.Eyedropper, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolLineButton, activeTool == CanvasToolType.Line, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolRectangleButton, activeTool == CanvasToolType.Rectangle, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolEllipseButton, activeTool == CanvasToolType.Ellipse, accentBrush, activeBackground, mutedTextBrush);
        SetToolButtonVisuals(ToolSelectionButton, activeTool == CanvasToolType.Selection, accentBrush, activeBackground, mutedTextBrush);

        if (ActiveToolIcon != null)
        {
            string iconKey = ToolToIconGeometryConverter.GetIconResourceKey(activeTool);
            if (this.TryFindResource(iconKey, out var res) && res is Geometry geom)
            {
                ActiveToolIcon.Data = geom;
            }
            else if (Application.Current != null && Application.Current.TryFindResource(iconKey, out var appRes) && appRes is Geometry appGeom)
            {
                ActiveToolIcon.Data = appGeom;
            }
        }
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
        UpdateActiveToolHighlight();
    }

    private void RebuildRecentMenu()
    {
        if (ViewModel == null) return;
        MenuRecentFiles.Items.Clear();

        if (ViewModel.RecentProjects.Count == 0)
        {
            var emptyItem = new MenuItem
            {
                Header = ViewModel.Loc["Menu_NoRecentFiles"],
                IsEnabled = false
            };
            MenuRecentFiles.Items.Add(emptyItem);
            return;
        }

        foreach (var path in ViewModel.RecentProjects)
        {
            var item = new MenuItem
            {
                Header = Path.GetFileName(path)
            };
            ToolTip.SetTip(item, path);
            var capturedPath = path;
            item.Click += async (_, _) =>
            {
                if (ViewModel != null)
                {
                    await ViewModel.OpenRecentProjectAsync(capturedPath);
                }
            };
            MenuRecentFiles.Items.Add(item);
        }

        MenuRecentFiles.Items.Add(new Separator());
        var clearItem = new MenuItem
        {
            Header = ViewModel.Loc["Menu_ClearRecentFiles"]
        };
        clearItem.Click += (_, _) => ViewModel.ClearRecentProjects();
        MenuRecentFiles.Items.Add(clearItem);
    }

    private void OnWindowDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void OnWindowDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(DataFormats.Files) || ViewModel == null)
        {
            return;
        }

        var files = e.Data.GetFiles();
        if (files == null)
        {
            return;
        }

        foreach (var file in files)
        {
            var path = file.Path?.LocalPath ?? file.TryGetLocalPath();
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".pxproj")
            {
                await ViewModel.OpenProjectFromFileAsync(path);
                break;
            }
            else if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp")
            {
                ViewModel.CanvasViewModel.ReferenceOverlay.LoadFromFile(path);
                ViewModel.StatusMessage = ViewModel.Loc.Format("Msg_ReferenceLoaded", Path.GetFileName(path));
                break;
            }
        }
    }

    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var version = PixelForge.Core.AppVersionInfo.CurrentVersion;
        var loc = ViewModel?.Loc ?? LocalizationService.Instance;
        var aboutWindow = new Window
        {
            Title = string.Format(loc["Dlg_AboutTitle"], version),
            Width = 440,
            Height = 300,
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
                            FontSize = 26,
                            FontWeight = FontWeight.Bold,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Foreground = GetBrush("TextPrimaryBrush", Brushes.White)
                        },
                        new TextBlock
                        {
                            Text = $"v{version}",
                            FontSize = 14,
                            FontWeight = FontWeight.SemiBold,
                            Foreground = GetBrush("AccentPrimaryBrush", Brushes.DodgerBlue),
                            HorizontalAlignment = HorizontalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = loc["Dlg_AboutDescription"],
                            FontSize = 12,
                            Foreground = GetBrush("TextSecondaryBrush", Brushes.LightGray),
                            TextWrapping = TextWrapping.Wrap,
                            TextAlignment = TextAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = loc["Dlg_AboutCopyright"],
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

    private async void OnHotkeysClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new HotkeysDialog();
        await dialog.ShowDialog(this);
    }

    private async void OnColorRampClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        var dialog = new ColorRampDialog(ViewModel.PaletteViewModel, ViewModel.ToolViewModel);
        await dialog.ShowDialog(this);
    }

    private async void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        await OpenSettingsDialogAsync();
    }

    private bool _isSettingsDialogOpen;

    private async Task OpenSettingsDialogAsync()
    {
        if (ViewModel == null || _isSettingsDialogOpen) return;
        _isSettingsDialogOpen = true;

        try
        {
            var vm = new SettingsViewModel(ViewModel.AppSettingsService);
            var dialog = new SettingsWindow
            {
                DataContext = vm
            };

            vm.SettingsApplied += (_, _) =>
            {
                ViewModel.ApplySettings(vm.CreateSnapshot());
            };

            await dialog.ShowDialog(this);
        }
        finally
        {
            _isSettingsDialogOpen = false;
        }
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

    private void OnMainWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (MouseHintsBadge != null)
        {
            MouseHintsBadge.IsVisible = e.NewSize.Width >= 1200;
        }
    }

    private void OnLayerNameDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control c && c.DataContext is Layer layer)
        {
            layer.IsRenaming = true;
            e.Handled = true;
        }
    }

    private void OnLayerNameTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is Layer layer)
        {
            layer.IsRenaming = false;
        }
    }

    private void OnLayerNameTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape)
        {
            if (sender is TextBox tb && tb.DataContext is Layer layer)
            {
                layer.IsRenaming = false;
                e.Handled = true;
            }
        }
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Don't trigger shortcuts if user is typing in a TextBox, NumericUpDown or editable text element
        var focused = FocusManager?.GetFocusedElement();
        if (focused is Visual v)
        {
            var current = v;
            while (current != null)
            {
                if (current is TextBox or AutoCompleteBox or NumericUpDown)
                {
                    return;
                }
                current = current.GetVisualParent();
            }
        }

        if (ViewModel == null)
        {
            return;
        }

        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        // Escape: Deselect active selection or cancel active drag
        if (!ctrl && !alt && !shift && (e.Key == Key.Escape || e.PhysicalKey == PhysicalKey.Escape))
        {
            if (ViewModel.CanvasViewModel.HasActiveSelection)
            {
                e.Handled = true;
                ViewModel.Deselect();
                return;
            }
        }

        // F1: Hotkeys cheat sheet dialog
        if (!ctrl && !alt && !shift && (e.Key == Key.F1 || e.PhysicalKey == PhysicalKey.F1))
        {
            e.Handled = true;
            OnHotkeysClick(this, new RoutedEventArgs());
            return;
        }

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

        // Shift + X: Toggle Symmetry X
        if (!ctrl && !alt && shift && MatchesKey(e, Key.X, PhysicalKey.X, 'ч'))
        {
            e.Handled = true;
            ViewModel.CanvasViewModel.ToggleSymmetryX();
            return;
        }

        // Shift + Y: Toggle Symmetry Y
        if (!ctrl && !alt && shift && MatchesKey(e, Key.Y, PhysicalKey.Y, 'н'))
        {
            e.Handled = true;
            ViewModel.CanvasViewModel.ToggleSymmetryY();
            return;
        }

        // Shift + L: Toggle Active Layer Visibility
        if (!ctrl && !alt && shift && MatchesKey(e, Key.L, PhysicalKey.L, 'д'))
        {
            e.Handled = true;
            ViewModel.LayerViewModel.ToggleVisibility(null);
            return;
        }

        // Alt + G: Cycle Tile Grid size
        if (alt && !ctrl && !shift && MatchesKey(e, Key.G, PhysicalKey.G, 'п'))
        {
            e.Handled = true;
            ViewModel.CanvasViewModel.CycleTileGridSize();
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

        // Ctrl + Shift + N: New Layer
        if (ctrl && shift && MatchesKey(e, Key.N, PhysicalKey.N, 'т'))
        {
            e.Handled = true;
            ViewModel.LayerViewModel.AddLayer();
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
            else if (MatchesKey(e, Key.E, PhysicalKey.E, 'у'))
            {
                e.Handled = true;
                if (ViewModel.LayerViewModel.CanMergeDown)
                {
                    ViewModel.LayerViewModel.MergeDown();
                }
            }
            else if (MatchesKey(e, Key.J, PhysicalKey.J, 'о'))
            {
                e.Handled = true;
                ViewModel.LayerViewModel.DuplicateLayer();
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
            else if (MatchesKey(e, Key.OemComma, PhysicalKey.Comma, 'б') || e.Key == Key.OemComma || e.KeySymbol == ",")
            {
                e.Handled = true;
                await OpenSettingsDialogAsync();
            }
            else if (MatchesKey(e, Key.G, PhysicalKey.G, 'п'))
            {
                e.Handled = true;
                if (shift)
                {
                    ViewModel.CanvasViewModel.ToggleTileGrid();
                }
                else
                {
                    ViewModel.CanvasViewModel.ToggleGrid();
                }
            }
            else if (MatchesKey(e, Key.D1, PhysicalKey.Digit1, '1') || e.Key == Key.NumPad1 || e.PhysicalKey == PhysicalKey.NumPad1)
            {
                e.Handled = true;
                ViewModel.CanvasViewModel.Zoom = 1.0f;
                ViewModel.StatusMessage = ViewModel.Loc.Format("Msg_ZoomSet", 100);
            }
            else if (MatchesKey(e, Key.D2, PhysicalKey.Digit2, '2') || e.Key == Key.NumPad2 || e.PhysicalKey == PhysicalKey.NumPad2)
            {
                e.Handled = true;
                ViewModel.CanvasViewModel.Zoom = 2.0f;
                ViewModel.StatusMessage = ViewModel.Loc.Format("Msg_ZoomSet", 200);
            }
            else if (MatchesKey(e, Key.D3, PhysicalKey.Digit3, '3') || e.Key == Key.NumPad3 || e.PhysicalKey == PhysicalKey.NumPad3)
            {
                e.Handled = true;
                ViewModel.CanvasViewModel.Zoom = 4.0f;
                ViewModel.StatusMessage = ViewModel.Loc.Format("Msg_ZoomSet", 400);
            }
            else if (MatchesKey(e, Key.D4, PhysicalKey.Digit4, '4') || e.Key == Key.NumPad4 || e.PhysicalKey == PhysicalKey.NumPad4)
            {
                e.Handled = true;
                ViewModel.CanvasViewModel.Zoom = 8.0f;
                ViewModel.StatusMessage = ViewModel.Loc.Format("Msg_ZoomSet", 800);
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
                if (shift)
                {
                    ViewModel.FlipActiveLayerHorizontal();
                }
                else
                {
                    SelectTool(CanvasToolType.ShadeBrush);
                }
            }
            else if (MatchesKey(e, Key.A, PhysicalKey.A, 'ф'))
            {
                e.Handled = true;
                SelectTool(CanvasToolType.SprayBrush);
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
            else if (MatchesKey(e, Key.OemComma, PhysicalKey.Comma, 'б') || e.KeySymbol == "," || e.KeySymbol == "<")
            {
                e.Handled = true;
                ViewModel.PaletteViewModel.SelectPreviousColor();
            }
            else if (MatchesKey(e, Key.OemPeriod, PhysicalKey.Period, 'ю') || e.KeySymbol == "." || e.KeySymbol == ">")
            {
                e.Handled = true;
                ViewModel.PaletteViewModel.SelectNextColor();
            }
            else if (!shift && (MatchesKey(e, Key.D1, PhysicalKey.Digit1, '1') || e.Key == Key.NumPad1))
            {
                e.Handled = true;
                ViewModel.ToolViewModel.SetBrushSize(1);
            }
            else if (!shift && (MatchesKey(e, Key.D2, PhysicalKey.Digit2, '2') || e.Key == Key.NumPad2))
            {
                e.Handled = true;
                ViewModel.ToolViewModel.SetBrushSize(2);
            }
            else if (!shift && (MatchesKey(e, Key.D3, PhysicalKey.Digit3, '3') || e.Key == Key.NumPad3))
            {
                e.Handled = true;
                ViewModel.ToolViewModel.SetBrushSize(3);
            }
            else if (!shift && (MatchesKey(e, Key.D4, PhysicalKey.Digit4, '4') || e.Key == Key.NumPad4))
            {
                e.Handled = true;
                ViewModel.ToolViewModel.SetBrushSize(4);
            }
            else if (!shift && (MatchesKey(e, Key.D5, PhysicalKey.Digit5, '5') || e.Key == Key.NumPad5))
            {
                e.Handled = true;
                ViewModel.ToolViewModel.SetBrushSize(5);
            }
            else if (!shift && (MatchesKey(e, Key.D8, PhysicalKey.Digit8, '8') || e.Key == Key.NumPad8))
            {
                e.Handled = true;
                ViewModel.ToolViewModel.SetBrushSize(8);
            }
            else if (e.Key == Key.Delete || e.PhysicalKey == PhysicalKey.Delete || e.Key == Key.Back || e.PhysicalKey == PhysicalKey.Backspace)
            {
                e.Handled = true;
                ViewModel.DeleteSelectionOrClearLayer();
            }
        }
    }
}

/// <summary>
/// Value converter mapping <see cref="CanvasToolType"/> to the corresponding vector icon geometry.
/// </summary>
public class ToolToIconGeometryConverter : Avalonia.Data.Converters.IValueConverter
{
    /// <summary>
    /// Gets the singleton instance of <see cref="ToolToIconGeometryConverter"/>.
    /// </summary>
    public static readonly ToolToIconGeometryConverter Instance = new();

    /// <summary>
    /// Converts a <see cref="CanvasToolType"/> into a Skia / Avalonia <see cref="Geometry"/>.
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is CanvasToolType toolType)
        {
            string key = GetIconResourceKey(toolType);
            if (Application.Current != null && Application.Current.TryFindResource(key, out var res) && res is Geometry geom)
            {
                return geom;
            }
        }
        return null;
    }

    /// <summary>
    /// Gets the resource key of the vector icon for the specified tool type.
    /// </summary>
    public static string GetIconResourceKey(CanvasToolType tool) => tool switch
    {
        CanvasToolType.Pencil => "IconPencil",
        CanvasToolType.Eraser => "IconEraser",
        CanvasToolType.DitherBrush => "IconDither",
        CanvasToolType.PatternBrush => "IconPatternBrush",
        CanvasToolType.ShadeBrush => "IconShadeBrush",
        CanvasToolType.SprayBrush => "IconSprayBrush",
        CanvasToolType.Fill => "IconFill",
        CanvasToolType.MagicWand => "IconMagicWand",
        CanvasToolType.Gradient => "IconGradient",
        CanvasToolType.Eyedropper => "IconEyedropper",
        CanvasToolType.Line => "IconLine",
        CanvasToolType.Rectangle => "IconRectangle",
        CanvasToolType.Ellipse => "IconEllipse",
        CanvasToolType.Selection => "IconSelection",
        _ => "IconPencil"
    };

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}

