using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;

namespace PixelForge.Core.Models;

/// <summary>
/// Settings for the canvas tile grid overlay (supporting standard sizes: 8x8, 16x16, 24x24, 32x32, 48x48, 64x64).
/// Used by pixel artists for tileset design, sprite sheet alignment, and modular pattern creation.
/// </summary>
public partial class TileGridSettings : ObservableObject
{
    /// <summary>
    /// Default tile dimension in pixels.
    /// </summary>
    public const int DefaultTileSize = 16;

    /// <summary>
    /// Minimum allowed tile dimension in pixels.
    /// </summary>
    public const int MinTileSize = 2;

    /// <summary>
    /// Standard available tile sizes for pixel art asset creation.
    /// </summary>
    public static readonly int[] AvailableSizes = [8, 16, 24, 32, 48, 64];

    /// <summary>
    /// Default overlay grid line color: subtle cyan with alpha in the 40-70 range.
    /// </summary>
    public static readonly SKColor DefaultGridColor = new(0, 210, 255, 65);

    private bool _isVisible;
    private int _tileSize = DefaultTileSize;
    private SKColor _gridColor = DefaultGridColor;

    /// <summary>
    /// Initializes a new instance of <see cref="TileGridSettings"/> with default parameters.
    /// </summary>
    public TileGridSettings()
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TileGridSettings"/> with specified parameters.
    /// </summary>
    /// <param name="tileSize">The tile dimension in pixels (clamped to >= 2).</param>
    /// <param name="isVisible">Whether the tile grid overlay is visible.</param>
    /// <param name="gridColor">The color used for drawing tile boundary lines.</param>
    public TileGridSettings(int tileSize, bool isVisible = false, SKColor? gridColor = null)
    {
        _tileSize = Math.Max(MinTileSize, tileSize);
        _isVisible = isVisible;
        _gridColor = gridColor ?? DefaultGridColor;
    }

    /// <summary>
    /// Gets or sets whether the tile grid overlay is currently visible on the canvas.
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    /// <summary>
    /// Gets or sets the tile size in pixels. Clamped to a minimum of <see cref="MinTileSize"/> (2).
    /// </summary>
    public int TileSize
    {
        get => _tileSize;
        set
        {
            int clamped = Math.Max(MinTileSize, value);
            if (SetProperty(ref _tileSize, clamped))
            {
                OnPropertyChanged(nameof(TileGridText));
            }
        }
    }

    /// <summary>
    /// Gets or sets the SkiaSharp color used for rendering tile boundaries.
    /// </summary>
    public SKColor GridColor
    {
        get => _gridColor;
        set => SetProperty(ref _gridColor, value);
    }

    /// <summary>
    /// Gets a formatted display label for the current tile size (e.g. "16 × 16 px").
    /// </summary>
    public string TileGridText => $"{TileSize} × {TileSize} px";

    /// <summary>
    /// Cycles the current tile size to the next available size in sequence:
    /// 8 -> 16 -> 24 -> 32 -> 48 -> 64 -> 8.
    /// </summary>
    public void CycleNextSize()
    {
        TileSize = GetNextSize(TileSize);
    }

    /// <summary>
    /// Clamps an arbitrary size to the nearest standard available tile size (8, 16, 24, 32, 48, 64).
    /// </summary>
    /// <param name="size">The arbitrary size in pixels.</param>
    /// <returns>The closest standard tile size.</returns>
    public static int ClampToAvailableSize(int size)
    {
        if (size <= AvailableSizes[0]) return AvailableSizes[0];
        if (size >= AvailableSizes[^1]) return AvailableSizes[^1];

        int closest = AvailableSizes[0];
        int minDiff = Math.Abs(size - closest);

        for (int i = 1; i < AvailableSizes.Length; i++)
        {
            int diff = Math.Abs(size - AvailableSizes[i]);
            if (diff < minDiff)
            {
                minDiff = diff;
                closest = AvailableSizes[i];
            }
        }

        return closest;
    }

    /// <summary>
    /// Gets the next tile size following <paramref name="currentSize"/> in the standard sequence:
    /// 8 -> 16 -> 24 -> 32 -> 48 -> 64 -> 8.
    /// If the size is not a standard size, moves to the next larger available size or wraps to 8.
    /// </summary>
    /// <param name="currentSize">The current tile size.</param>
    /// <returns>The next tile size in the cycle.</returns>
    public static int GetNextSize(int currentSize)
    {
        for (int i = 0; i < AvailableSizes.Length; i++)
        {
            if (AvailableSizes[i] == currentSize)
            {
                return AvailableSizes[(i + 1) % AvailableSizes.Length];
            }
        }

        // If not found in standard sizes, find the next larger size
        for (int i = 0; i < AvailableSizes.Length; i++)
        {
            if (AvailableSizes[i] > currentSize)
            {
                return AvailableSizes[i];
            }
        }

        // If greater than maximum standard size or negative, wrap to first size
        return AvailableSizes[0];
    }

    /// <summary>
    /// Snaps a 1D pixel coordinate to the start of its containing tile.
    /// </summary>
    /// <param name="coordinate">The 1D pixel coordinate.</param>
    /// <param name="tileSize">The tile dimension.</param>
    /// <returns>The 1D coordinate snapped to the tile origin.</returns>
    public static int SnapToTile(int coordinate, int tileSize)
    {
        if (tileSize <= 0) return coordinate;
        return (int)Math.Floor((double)coordinate / tileSize) * tileSize;
    }

    /// <summary>
    /// Snaps 2D pixel coordinates to the top-left pixel corner of the containing tile.
    /// </summary>
    /// <param name="pixelX">The X pixel coordinate.</param>
    /// <param name="pixelY">The Y pixel coordinate.</param>
    /// <param name="tileSize">The tile dimension in pixels.</param>
    /// <returns>The snapped top-left (X, Y) coordinates of the tile.</returns>
    public static (int X, int Y) SnapToTile(int pixelX, int pixelY, int tileSize)
    {
        return (SnapToTile(pixelX, tileSize), SnapToTile(pixelY, tileSize));
    }

    /// <summary>
    /// Computes the zero-based column and row index for given pixel coordinates.
    /// </summary>
    /// <param name="pixelX">The X pixel coordinate.</param>
    /// <param name="pixelY">The Y pixel coordinate.</param>
    /// <param name="tileSize">The tile dimension in pixels.</param>
    /// <returns>A tuple of (TileX, TileY) column and row indices.</returns>
    public static (int TileX, int TileY) GetTileIndex(int pixelX, int pixelY, int tileSize)
    {
        if (tileSize <= 0) return (0, 0);
        int tx = (int)Math.Floor((double)pixelX / tileSize);
        int ty = (int)Math.Floor((double)pixelY / tileSize);
        return (tx, ty);
    }

    /// <summary>
    /// Gets the pixel bounding coordinates for a given tile index.
    /// </summary>
    /// <param name="tileX">The tile column index.</param>
    /// <param name="tileY">The tile row index.</param>
    /// <param name="tileSize">The tile dimension in pixels.</param>
    /// <returns>A tuple of (Left, Top, Right, Bottom) boundaries in pixel units.</returns>
    public static (int Left, int Top, int Right, int Bottom) GetTileBounds(int tileX, int tileY, int tileSize)
    {
        if (tileSize <= 0) return (0, 0, 0, 0);
        int left = tileX * tileSize;
        int top = tileY * tileSize;
        return (left, top, left + tileSize, top + tileSize);
    }

    /// <summary>
    /// Creates a deep clone of the current settings.
    /// </summary>
    public TileGridSettings Clone()
    {
        return new TileGridSettings(TileSize, IsVisible, GridColor);
    }
}
