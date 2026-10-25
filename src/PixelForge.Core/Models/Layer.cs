using CommunityToolkit.Mvvm.ComponentModel;

namespace PixelForge.Core.Models;

/// <summary>
/// Represents an editable raster layer within a pixel canvas.
/// </summary>
public partial class Layer : ObservableObject
{
    private string _name;
    private bool _isVisible;
    private float _opacity;
    private bool _isAlphaLocked;
    private bool _isRenaming;

    /// <summary>
    /// Event raised when the pixel buffer of this layer is modified.
    /// </summary>
    public event Action? PixelsChanged;

    public Guid Id { get; init; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public PixelColor[] Pixels { get; private set; }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public float Opacity
    {
        get => _opacity;
        set => SetProperty(ref _opacity, Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets whether alpha transparency locking is enabled.
    /// When true, pixel operations and drawing tools will not modify pixels where existing Alpha is 0.
    /// </summary>
    public bool IsAlphaLocked
    {
        get => _isAlphaLocked;
        set => SetProperty(ref _isAlphaLocked, value);
    }

    /// <summary>
    /// Gets or sets whether inline name renaming mode is active in the UI.
    /// </summary>
    public bool IsRenaming
    {
        get => _isRenaming;
        set => SetProperty(ref _isRenaming, value);
    }

    public Layer(string name, int width, int height, Guid? id = null)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
        }

        Id = id ?? Guid.NewGuid();
        _name = name ?? throw new ArgumentNullException(nameof(name));
        Width = width;
        Height = height;
        Pixels = new PixelColor[width * height];
        _isVisible = true;
        _opacity = 1f;
    }

    public Layer(string name, int width, int height, PixelColor[] pixels, Guid? id = null)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(pixels);

        if (pixels.Length != width * height)
        {
            throw new ArgumentException($"Pixel array length ({pixels.Length}) does not match layer dimensions ({width}x{height} = {width * height}).", nameof(pixels));
        }

        Id = id ?? Guid.NewGuid();
        _name = name ?? throw new ArgumentNullException(nameof(name));
        Width = width;
        Height = height;
        Pixels = pixels;
        _isVisible = true;
        _opacity = 1f;
    }

    /// <summary>
    /// Updates the layer dimensions and replaces the pixel buffer during canvas resize.
    /// </summary>
    public void SetDimensionsAndPixels(int newWidth, int newHeight, PixelColor[] newPixels)
    {
        if (newWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newWidth), "Width must be greater than zero.");
        }

        if (newHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newHeight), "Height must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(newPixels);

        if (newPixels.Length != newWidth * newHeight)
        {
            throw new ArgumentException($"Pixel array length ({newPixels.Length}) does not match new dimensions ({newWidth}x{newHeight} = {newWidth * newHeight}).", nameof(newPixels));
        }

        Width = newWidth;
        Height = newHeight;
        Pixels = newPixels;

        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Height));
        OnPropertyChanged(nameof(Pixels));
        Invalidate();
    }

    /// <summary>
    /// Gets the color of the pixel at the specified coordinates.
    /// Returns Transparent if coordinates are out of bounds.
    /// </summary>
    public PixelColor GetPixel(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
        {
            return PixelColor.Transparent;
        }

        return Pixels[y * Width + x];
    }

    /// <summary>
    /// Sets the color of the pixel at the specified coordinates.
    /// Ignored if coordinates are out of bounds.
    /// </summary>
    public void SetPixel(int x, int y, PixelColor color)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
        {
            return;
        }

        int index = y * Width + x;
        if (_isAlphaLocked && Pixels[index].A == 0)
        {
            return;
        }

        Pixels[index] = color;
    }

    /// <summary>
    /// Clears the entire layer with the specified color or transparent by default.
    /// </summary>
    public void Clear(PixelColor? color = null)
    {
        var fill = color ?? PixelColor.Transparent;
        if (_isAlphaLocked)
        {
            for (int i = 0; i < Pixels.Length; i++)
            {
                if (Pixels[i].A > 0)
                {
                    Pixels[i] = fill;
                }
            }
        }
        else
        {
            Array.Fill(Pixels, fill);
        }
        Invalidate();
    }

    /// <summary>
    /// Notifies listeners that the pixel buffer has been modified.
    /// </summary>
    public void Invalidate()
    {
        PixelsChanged?.Invoke();
    }

    /// <summary>
    /// Creates a deep copy of the layer including its pixel data.
    /// </summary>
    /// <param name="newId">If true, generates a new Guid; otherwise preserves the current Id.</param>
    public Layer Clone(bool newId = false)
    {
        var pixelCopy = new PixelColor[Pixels.Length];
        Array.Copy(Pixels, pixelCopy, Pixels.Length);

        return new Layer(Name, Width, Height, pixelCopy, newId ? Guid.NewGuid() : Id)
        {
            IsVisible = IsVisible,
            Opacity = Opacity,
            IsAlphaLocked = IsAlphaLocked
        };
    }
}
