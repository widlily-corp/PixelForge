using PixelForge.Core.Models;
using SkiaSharp;

namespace PixelForge.Core.History;

/// <summary>
/// Reversible command that flips floating selection pixels horizontally or vertically.
/// </summary>
public class FlipFloatingSelectionCommand : IUndoableCommand
{
    private readonly SelectionModel _selection;
    private readonly FlipDirection _direction;
    private readonly PixelColor[] _oldPixels;
    private readonly PixelColor[] _newPixels;
    private readonly Action? _onChanged;

    public string Description => $"Flip Floating Selection {(_direction == FlipDirection.Horizontal ? "Horizontal" : "Vertical")}";

    public FlipFloatingSelectionCommand(SelectionModel selection, FlipDirection direction, Action? onChanged = null)
    {
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _direction = direction;
        _onChanged = onChanged;

        _oldPixels = new PixelColor[selection.Pixels.Length];
        Array.Copy(selection.Pixels, _oldPixels, selection.Pixels.Length);

        int w = selection.Bounds.Width;
        int h = selection.Bounds.Height;
        _newPixels = new PixelColor[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int srcX = direction == FlipDirection.Horizontal ? (w - 1 - x) : x;
                int srcY = direction == FlipDirection.Vertical ? (h - 1 - y) : y;
                _newPixels[y * w + x] = _oldPixels[srcY * w + srcX];
            }
        }
    }

    public void Execute()
    {
        var copy = new PixelColor[_newPixels.Length];
        Array.Copy(_newPixels, copy, _newPixels.Length);
        _selection.Pixels = copy;
        _onChanged?.Invoke();
    }

    public void Undo()
    {
        var copy = new PixelColor[_oldPixels.Length];
        Array.Copy(_oldPixels, copy, _oldPixels.Length);
        _selection.Pixels = copy;
        _onChanged?.Invoke();
    }
}

/// <summary>
/// Reversible command that rotates floating selection pixels by 90 CW, 90 CCW, or 180 degrees, updating bounds dimensions.
/// </summary>
public class RotateFloatingSelectionCommand : IUndoableCommand
{
    private readonly SelectionModel _selection;
    private readonly RotationAngle _angle;
    private readonly PixelColor[] _oldPixels;
    private readonly SKRectI _oldBounds;
    private readonly PixelColor[] _newPixels;
    private readonly SKRectI _newBounds;
    private readonly Action? _onChanged;

    public string Description => $"Rotate Floating Selection {_angle switch
    {
        RotationAngle.Rotate90CW => "90° CW",
        RotationAngle.Rotate90CCW => "90° CCW",
        RotationAngle.Rotate180 => "180°",
        _ => ""
    }}";

    public RotateFloatingSelectionCommand(SelectionModel selection, RotationAngle angle, Action? onChanged = null)
    {
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _angle = angle;
        _onChanged = onChanged;

        _oldBounds = selection.Bounds;
        _oldPixels = new PixelColor[selection.Pixels.Length];
        Array.Copy(selection.Pixels, _oldPixels, selection.Pixels.Length);

        int w = selection.Bounds.Width;
        int h = selection.Bounds.Height;

        if (angle == RotationAngle.Rotate180)
        {
            _newBounds = _oldBounds;
            _newPixels = new PixelColor[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    _newPixels[y * w + x] = _oldPixels[(h - 1 - y) * w + (w - 1 - x)];
                }
            }
        }
        else
        {
            int newW = h;
            int newH = w;
            _newBounds = new SKRectI(_oldBounds.Left, _oldBounds.Top, _oldBounds.Left + newW, _oldBounds.Top + newH);
            _newPixels = new PixelColor[newW * newH];

            for (int ny = 0; ny < newH; ny++)
            {
                for (int nx = 0; nx < newW; nx++)
                {
                    int srcX = angle == RotationAngle.Rotate90CW ? ny : (w - 1 - ny);
                    int srcY = angle == RotationAngle.Rotate90CW ? (h - 1 - nx) : nx;
                    _newPixels[ny * newW + nx] = _oldPixels[srcY * w + srcX];
                }
            }
        }
    }

    public void Execute()
    {
        _selection.Bounds = _newBounds;
        var copy = new PixelColor[_newPixels.Length];
        Array.Copy(_newPixels, copy, _newPixels.Length);
        _selection.Pixels = copy;
        _onChanged?.Invoke();
    }

    public void Undo()
    {
        _selection.Bounds = _oldBounds;
        var copy = new PixelColor[_oldPixels.Length];
        Array.Copy(_oldPixels, copy, _oldPixels.Length);
        _selection.Pixels = copy;
        _onChanged?.Invoke();
    }
}
