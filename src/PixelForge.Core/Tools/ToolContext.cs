using PixelForge.Core.History;
using PixelForge.Core.Models;

namespace PixelForge.Core.Tools;

/// <summary>
/// Execution context provided to canvas tools during interactive manipulation.
/// </summary>
public class ToolContext
{
    private PixelColor _primaryColor;
    private PixelColor _secondaryColor;

    /// <summary>
    /// Gets or sets the target canvas being edited.
    /// </summary>
    public PixelCanvas Canvas { get; set; }

    /// <summary>
    /// Gets or sets the currently active layer.
    /// </summary>
    public Layer ActiveLayer { get; set; }

    /// <summary>
    /// Gets or sets the active primary drawing color.
    /// </summary>
    public PixelColor PrimaryColor
    {
        get => _primaryColor;
        set
        {
            _primaryColor = value;
            PrimaryColorChanged?.Invoke(value);
        }
    }

    /// <summary>
    /// Gets or sets the active secondary drawing color.
    /// </summary>
    public PixelColor SecondaryColor
    {
        get => _secondaryColor;
        set
        {
            _secondaryColor = value;
            SecondaryColorChanged?.Invoke(value);
        }
    }

    /// <summary>
    /// Gets or sets whether horizontal reflection symmetry across the vertical axis is enabled.
    /// </summary>
    public bool SymmetryX { get; set; }

    /// <summary>
    /// Gets or sets whether vertical reflection symmetry across the horizontal axis is enabled.
    /// </summary>
    public bool SymmetryY { get; set; }

    /// <summary>
    /// Gets or sets the X coordinate of the vertical symmetry mirror axis.
    /// </summary>
    public int SymmetryAxisX { get; set; }

    /// <summary>
    /// Gets or sets the Y coordinate of the horizontal symmetry mirror axis.
    /// </summary>
    public int SymmetryAxisY { get; set; }

    /// <summary>
    /// Gets or sets the undo/redo history service.
    /// </summary>
    public UndoRedoService History { get; set; }

    /// <summary>
    /// Gets or sets the selection model state on the canvas.
    /// </summary>
    public SelectionModel Selection { get; set; }

    /// <summary>
    /// Gets or sets the active dithering pattern for brushes and gradients.
    /// </summary>
    public DitherPattern ActiveDitherPattern { get; set; } = DitherPattern.None;

    /// <summary>
    /// Gets or sets the dithering threshold/intensity (0.0 to 1.0).
    /// </summary>
    public float DitherIntensity { get; set; } = 0.5f;

    /// <summary>
    /// Gets or sets color difference threshold for flood fill (0 to 255).
    /// </summary>
    public int FillTolerance { get; set; } = 0;

    /// <summary>
    /// Gets or sets whether flood fill operates contiguously (BFS) or globally replaces color across the layer.
    /// </summary>
    public bool FillContiguous { get; set; } = true;

    /// <summary>
    /// Event triggered when the primary color is modified by a tool (e.g., eyedropper).
    /// </summary>
    public event Action<PixelColor>? PrimaryColorChanged;

    /// <summary>
    /// Event triggered when the secondary color is modified.
    /// </summary>
    public event Action<PixelColor>? SecondaryColorChanged;

    public ToolContext(
        PixelCanvas canvas,
        Layer? activeLayer = null,
        UndoRedoService? history = null,
        PixelColor? primaryColor = null,
        PixelColor? secondaryColor = null,
        bool symmetryX = false,
        bool symmetryY = false,
        int? symmetryAxisX = null,
        int? symmetryAxisY = null,
        SelectionModel? selection = null)
    {
        Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        ActiveLayer = activeLayer ?? canvas.ActiveLayer ?? (canvas.Layers.Count > 0 ? canvas.Layers[0] : throw new InvalidOperationException("Canvas has no layers."));
        History = history ?? new UndoRedoService();
        _primaryColor = primaryColor ?? PixelColor.Black;
        _secondaryColor = secondaryColor ?? PixelColor.White;
        SymmetryX = symmetryX;
        SymmetryY = symmetryY;
        SymmetryAxisX = symmetryAxisX ?? (canvas.Width / 2);
        SymmetryAxisY = symmetryAxisY ?? (canvas.Height / 2);
        Selection = selection ?? new SelectionModel();
    }

    /// <summary>
    /// Calculates the reflected X coordinate according to the vertical symmetry axis.
    /// </summary>
    public int GetSymmetricX(int x)
    {
        if (SymmetryAxisX == Canvas.Width / 2)
        {
            return Canvas.Width - 1 - x;
        }

        return 2 * SymmetryAxisX - x;
    }

    /// <summary>
    /// Calculates the reflected Y coordinate according to the horizontal symmetry axis.
    /// </summary>
    public int GetSymmetricY(int y)
    {
        if (SymmetryAxisY == Canvas.Height / 2)
        {
            return Canvas.Height - 1 - y;
        }

        return 2 * SymmetryAxisY - y;
    }

    /// <summary>
    /// Invokes the action for the given coordinate and all valid reflected symmetry coordinates.
    /// </summary>
    public void ForEachSymmetricPoint(int x, int y, Action<int, int> action)
    {
        if ((uint)x >= (uint)Canvas.Width || (uint)y >= (uint)Canvas.Height)
        {
            return;
        }

        action(x, y);

        int sx = SymmetryX ? GetSymmetricX(x) : x;
        int sy = SymmetryY ? GetSymmetricY(y) : y;

        bool validSx = SymmetryX && sx >= 0 && sx < Canvas.Width && sx != x;
        bool validSy = SymmetryY && sy >= 0 && sy < Canvas.Height && sy != y;

        if (validSx)
        {
            action(sx, y);
        }

        if (validSy)
        {
            action(x, sy);
        }

        if (validSx && validSy)
        {
            action(sx, sy);
        }
    }

    /// <summary>
    /// Enumerates the given coordinate and all active symmetry counterpart coordinates.
    /// </summary>
    public IEnumerable<(int X, int Y)> GetSymmetricPoints(int x, int y)
    {
        if ((uint)x >= (uint)Canvas.Width || (uint)y >= (uint)Canvas.Height)
        {
            yield break;
        }

        yield return (x, y);

        int sx = SymmetryX ? GetSymmetricX(x) : x;
        int sy = SymmetryY ? GetSymmetricY(y) : y;

        bool validSx = SymmetryX && sx >= 0 && sx < Canvas.Width && sx != x;
        bool validSy = SymmetryY && sy >= 0 && sy < Canvas.Height && sy != y;

        if (validSx)
        {
            yield return (sx, y);
        }

        if (validSy)
        {
            yield return (x, sy);
        }

        if (validSx && validSy)
        {
            yield return (sx, sy);
        }
    }
}
