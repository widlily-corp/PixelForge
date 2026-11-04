using CommunityToolkit.Mvvm.ComponentModel;

namespace PixelForge.Core.Models;

/// <summary>
/// Represents a single cel: the intersection of a layer track and an animation frame.
/// Holds the raw pixel buffer for that specific layer on that specific frame.
/// </summary>
public class AnimationCel : ObservableObject
{
    private PixelColor[] _pixels;
    private bool _isEmpty;

    /// <summary>
    /// Gets the unique identifier of the layer this cel belongs to.
    /// </summary>
    public Guid LayerId { get; }

    /// <summary>
    /// Gets the pixel buffer for this cel.
    /// </summary>
    public PixelColor[] Pixels
    {
        get => _pixels;
        private set => SetProperty(ref _pixels, value);
    }

    /// <summary>
    /// Gets or sets whether this cel is considered blank or unpainted.
    /// </summary>
    public bool IsEmpty
    {
        get => _isEmpty;
        set => SetProperty(ref _isEmpty, value);
    }

    /// <summary>
    /// Initializes a new blank cel for the specified layer with width and height.
    /// </summary>
    /// <param name="layerId">The associated layer ID.</param>
    /// <param name="width">Layer width in pixels.</param>
    /// <param name="height">Layer height in pixels.</param>
    public AnimationCel(Guid layerId, int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
        }

        LayerId = layerId;
        _pixels = new PixelColor[width * height];
        _isEmpty = true;
    }

    /// <summary>
    /// Initializes a cel for the specified layer with an existing pixel buffer.
    /// </summary>
    /// <param name="layerId">The associated layer ID.</param>
    /// <param name="pixels">The pixel array.</param>
    /// <param name="isEmpty">Whether the cel is initially empty.</param>
    public AnimationCel(Guid layerId, PixelColor[] pixels, bool isEmpty = false)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        LayerId = layerId;
        _pixels = pixels;
        _isEmpty = isEmpty;
    }

    /// <summary>
    /// Replaces the pixel buffer of this cel.
    /// </summary>
    /// <param name="pixels">The replacement pixel buffer.</param>
    /// <param name="isEmpty">Whether the cel is considered empty.</param>
    public void SetPixels(PixelColor[] pixels, bool isEmpty = false)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        Pixels = pixels;
        IsEmpty = isEmpty;
    }

    /// <summary>
    /// Evaluates all pixels in the cel and updates <see cref="IsEmpty"/> accordingly.
    /// </summary>
    public void UpdateIsEmpty()
    {
        for (int i = 0; i < _pixels.Length; i++)
        {
            if (_pixels[i].A > 0)
            {
                IsEmpty = false;
                return;
            }
        }

        IsEmpty = true;
    }

    /// <summary>
    /// Creates a deep copy of this cel with a cloned pixel buffer.
    /// </summary>
    public AnimationCel Clone()
    {
        var pixelCopy = new PixelColor[_pixels.Length];
        Array.Copy(_pixels, pixelCopy, _pixels.Length);
        return new AnimationCel(LayerId, pixelCopy, _isEmpty);
    }
}
