using PixelForge.Core.History;
using PixelForge.Core.Models;
using SkiaSharp;

namespace PixelForge.Core.Tools;

/// <summary>
/// Clipboard data structure storing copied or cut pixel regions.
/// </summary>
public readonly record struct SelectionClipboardData(int Width, int Height, PixelColor[] Pixels);

/// <summary>
/// Selection tool providing rectangular region selection, cut, copy, paste, floating pixel movement, and deletion.
/// </summary>
public class SelectionTool : ITool
{
    private static SelectionClipboardData? _clipboard;

    private bool _isSelecting;
    private bool _isMoving;
    private int _startX;
    private int _startY;
    private int _currentX;
    private int _currentY;
    private int _dragStartX;
    private int _dragStartY;
    private int _initialOffsetX;
    private int _initialOffsetY;

    public string Name => "Selection";
    public string Shortcut => "M";
    public string IconKey => "IconSelection";

    /// <summary>
    /// Gets the current shared clipboard content.
    /// </summary>
    public static SelectionClipboardData? Clipboard
    {
        get => _clipboard;
        set => _clipboard = value;
    }

    public void OnPointerDown(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (ctx.Selection.IsActive && ctx.Selection.Contains(x, y))
        {
            if (!ctx.Selection.IsFloating)
            {
                Lift(ctx);
            }

            _isMoving = true;
            _dragStartX = x;
            _dragStartY = y;
            _initialOffsetX = ctx.Selection.OffsetX;
            _initialOffsetY = ctx.Selection.OffsetY;
            return;
        }

        if (ctx.Selection.IsFloating)
        {
            Stamp(ctx);
        }

        ctx.Selection.Clear();
        _isSelecting = true;
        _isMoving = false;
        _startX = x;
        _startY = y;
        _currentX = x;
        _currentY = y;
    }

    public void OnPointerMove(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (_isMoving)
        {
            int dx = x - _dragStartX;
            int dy = y - _dragStartY;
            ctx.Selection.OffsetX = _initialOffsetX + dx;
            ctx.Selection.OffsetY = _initialOffsetY + dy;
            return;
        }

        if (_isSelecting)
        {
            _currentX = x;
            _currentY = y;
        }
    }

    public void OnPointerUp(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (_isMoving)
        {
            int dx = x - _dragStartX;
            int dy = y - _dragStartY;
            ctx.Selection.OffsetX = _initialOffsetX + dx;
            ctx.Selection.OffsetY = _initialOffsetY + dy;
            _isMoving = false;
            return;
        }

        if (_isSelecting)
        {
            _currentX = x;
            _currentY = y;

            int minX = Math.Clamp(Math.Min(_startX, _currentX), 0, ctx.Canvas.Width - 1);
            int maxX = Math.Clamp(Math.Max(_startX, _currentX), 0, ctx.Canvas.Width - 1);
            int minY = Math.Clamp(Math.Min(_startY, _currentY), 0, ctx.Canvas.Height - 1);
            int maxY = Math.Clamp(Math.Max(_startY, _currentY), 0, ctx.Canvas.Height - 1);

            int width = maxX - minX + 1;
            int height = maxY - minY + 1;

            if (width > 0 && height > 0)
            {
                var rect = new SKRectI(minX, minY, minX + width, minY + height);
                ctx.Selection.Select(rect);
            }
            else
            {
                ctx.Selection.Clear();
            }

            _isSelecting = false;
        }
    }

    public IReadOnlyList<(int X, int Y, PixelColor Color)> GetPreviewPixels(ToolContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (_isSelecting)
        {
            var list = new List<(int X, int Y, PixelColor Color)>();
            int minX = Math.Min(_startX, _currentX);
            int maxX = Math.Max(_startX, _currentX);
            int minY = Math.Min(_startY, _currentY);
            int maxY = Math.Max(_startY, _currentY);

            for (int x = minX; x <= maxX; x++)
            {
                list.Add((x, minY, PixelColor.Cyan));
                if (minY != maxY)
                {
                    list.Add((x, maxY, PixelColor.Cyan));
                }
            }

            for (int y = minY + 1; y < maxY; y++)
            {
                list.Add((minX, y, PixelColor.Cyan));
                if (minX != maxX)
                {
                    list.Add((maxX, y, PixelColor.Cyan));
                }
            }

            return list;
        }

        if (ctx.Selection.IsActive && ctx.Selection.IsFloating && ctx.Selection.Pixels.Length > 0)
        {
            var list = new List<(int X, int Y, PixelColor Color)>(ctx.Selection.Pixels.Length);
            int w = ctx.Selection.Bounds.Width;
            int h = ctx.Selection.Bounds.Height;
            int destX = ctx.Selection.Bounds.Left + ctx.Selection.OffsetX;
            int destY = ctx.Selection.Bounds.Top + ctx.Selection.OffsetY;

            for (int y = 0; y < h; y++)
            {
                int py = destY + y;
                for (int x = 0; x < w; x++)
                {
                    int px = destX + x;
                    var color = ctx.Selection.Pixels[y * w + x];
                    if (color.A > 0)
                    {
                        list.Add((px, py, color));
                    }
                }
            }

            return list;
        }

        return Array.Empty<(int X, int Y, PixelColor Color)>();
    }

    /// <summary>
    /// Copies selected pixels to the clipboard.
    /// </summary>
    public void Copy(ToolContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (!ctx.Selection.IsActive || ctx.Selection.Bounds.Width <= 0 || ctx.Selection.Bounds.Height <= 0)
        {
            return;
        }

        int w = ctx.Selection.Bounds.Width;
        int h = ctx.Selection.Bounds.Height;
        var copy = new PixelColor[w * h];

        if (ctx.Selection.IsFloating && ctx.Selection.Pixels.Length == w * h)
        {
            Array.Copy(ctx.Selection.Pixels, copy, copy.Length);
        }
        else
        {
            for (int y = 0; y < h; y++)
            {
                int srcY = ctx.Selection.Bounds.Top + y;
                for (int x = 0; x < w; x++)
                {
                    int srcX = ctx.Selection.Bounds.Left + x;
                    copy[y * w + x] = ctx.ActiveLayer.GetPixel(srcX, srcY);
                }
            }
        }

        Clipboard = new SelectionClipboardData(w, h, copy);
    }

    /// <summary>
    /// Copies selected pixels to clipboard and removes them from the active layer.
    /// </summary>
    public void Cut(ToolContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (!ctx.Selection.IsActive)
        {
            return;
        }

        Copy(ctx);
        Delete(ctx);
    }

    /// <summary>
    /// Pastes pixel buffer from clipboard into a floating selection.
    /// </summary>
    public void Paste(ToolContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (Clipboard == null || Clipboard.Value.Width <= 0 || Clipboard.Value.Height <= 0)
        {
            return;
        }

        if (ctx.Selection.IsFloating)
        {
            Stamp(ctx);
        }

        var data = Clipboard.Value;
        int startX = Math.Max(0, (ctx.Canvas.Width - data.Width) / 2);
        int startY = Math.Max(0, (ctx.Canvas.Height - data.Height) / 2);

        var pixels = new PixelColor[data.Pixels.Length];
        Array.Copy(data.Pixels, pixels, pixels.Length);

        ctx.Selection.Select(new SKRectI(startX, startY, startX + data.Width, startY + data.Height), pixels);
        ctx.Selection.IsFloating = true;
    }

    /// <summary>
    /// Clears selected pixels to transparent and records undo deltas.
    /// </summary>
    public void Delete(ToolContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (!ctx.Selection.IsActive)
        {
            return;
        }

        if (ctx.Selection.IsFloating)
        {
            ctx.Selection.Clear();
            return;
        }

        int w = ctx.Selection.Bounds.Width;
        int h = ctx.Selection.Bounds.Height;
        var deltas = new List<PixelDelta>();

        for (int y = 0; y < h; y++)
        {
            int py = ctx.Selection.Bounds.Top + y;
            for (int x = 0; x < w; x++)
            {
                int px = ctx.Selection.Bounds.Left + x;
                var oldColor = ctx.ActiveLayer.GetPixel(px, py);
                if (oldColor != PixelColor.Transparent)
                {
                    deltas.Add(new PixelDelta(px, py, oldColor, PixelColor.Transparent));
                    ctx.ActiveLayer.SetPixel(px, py, PixelColor.Transparent);
                }
            }
        }

        if (deltas.Count > 0)
        {
            var command = new DrawPixelsCommand(ctx.ActiveLayer, deltas.ToArray(), "Delete Selection");
            ctx.History.ExecuteCommand(command);
        }

        ctx.Selection.Clear();
    }

    /// <summary>
    /// Lifts selected pixels from the active layer into the floating selection and records undo deltas.
    /// </summary>
    public void Lift(ToolContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (!ctx.Selection.IsActive || ctx.Selection.IsFloating)
        {
            return;
        }

        int w = ctx.Selection.Bounds.Width;
        int h = ctx.Selection.Bounds.Height;
        var deltas = new List<PixelDelta>();

        for (int y = 0; y < h; y++)
        {
            int py = ctx.Selection.Bounds.Top + y;
            for (int x = 0; x < w; x++)
            {
                int px = ctx.Selection.Bounds.Left + x;
                var oldColor = ctx.ActiveLayer.GetPixel(px, py);
                if (oldColor != PixelColor.Transparent)
                {
                    deltas.Add(new PixelDelta(px, py, oldColor, PixelColor.Transparent));
                }
            }
        }

        ctx.Selection.Lift(ctx.ActiveLayer);

        if (deltas.Count > 0)
        {
            var command = new DrawPixelsCommand(ctx.ActiveLayer, deltas.ToArray(), "Lift Selection");
            ctx.History.ExecuteCommand(command);
        }
    }

    /// <summary>
    /// Stamps floating selection onto the active layer and records undo deltas.
    /// </summary>
    public void Stamp(ToolContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (!ctx.Selection.IsActive || !ctx.Selection.IsFloating || ctx.Selection.Pixels.Length == 0)
        {
            return;
        }

        int w = ctx.Selection.Bounds.Width;
        int h = ctx.Selection.Bounds.Height;
        int destStartX = ctx.Selection.Bounds.Left + ctx.Selection.OffsetX;
        int destStartY = ctx.Selection.Bounds.Top + ctx.Selection.OffsetY;
        var deltas = new List<PixelDelta>();

        for (int y = 0; y < h; y++)
        {
            int dstY = destStartY + y;
            for (int x = 0; x < w; x++)
            {
                int dstX = destStartX + x;
                var newColor = ctx.Selection.Pixels[y * w + x];
                if (newColor.A > 0 && (uint)dstX < (uint)ctx.Canvas.Width && (uint)dstY < (uint)ctx.Canvas.Height)
                {
                    var oldColor = ctx.ActiveLayer.GetPixel(dstX, dstY);
                    if (oldColor != newColor)
                    {
                        deltas.Add(new PixelDelta(dstX, dstY, oldColor, newColor));
                    }
                }
            }
        }

        ctx.Selection.Stamp(ctx.ActiveLayer);

        if (deltas.Count > 0)
        {
            var command = new DrawPixelsCommand(ctx.ActiveLayer, deltas.ToArray(), "Stamp Floating Selection");
            ctx.History.ExecuteCommand(command);
        }
    }

    /// <summary>
    /// Deselects the current region, stamping any floating pixels first.
    /// </summary>
    public void Deselect(ToolContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (ctx.Selection.IsFloating)
        {
            Stamp(ctx);
        }

        ctx.Selection.Clear();
    }
}
