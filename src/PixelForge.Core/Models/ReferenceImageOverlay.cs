using System.ComponentModel;
using SkiaSharp;

namespace PixelForge.Core.Models;

/// <summary>
/// Encapsulates a semi-transparent reference image overlay rendered on top of or beneath the pixel art canvas.
/// Artists use reference overlays for tracing, anatomical reference, and composition guides.
/// </summary>
public class ReferenceImageOverlay : INotifyPropertyChanged, IDisposable
{
    private SKBitmap? _bitmap;
    private float _opacity = 0.5f;
    private bool _isVisible = true;
    private string? _filePath;
    private bool _isDisposed;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the decoded native Skia bitmap for hardware-accelerated rendering.
    /// </summary>
    public SKBitmap? Bitmap => _bitmap;

    /// <summary>
    /// Gets or sets the overlay opacity ranging from 0.0 (transparent) to 1.0 (fully opaque).
    /// </summary>
    public float Opacity
    {
        get => _opacity;
        set
        {
            var clamped = Math.Clamp(value, 0f, 1f);
            if (Math.Abs(_opacity - clamped) > 0.001f)
            {
                _opacity = clamped;
                OnPropertyChanged(nameof(Opacity));
            }
        }
    }

    /// <summary>
    /// Gets or sets whether the reference overlay is currently displayed on the canvas.
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                OnPropertyChanged(nameof(IsVisible));
            }
        }
    }

    /// <summary>
    /// Gets the original disk file path of the loaded reference image, if any.
    /// </summary>
    public string? FilePath => _filePath;

    /// <summary>
    /// Gets whether a valid reference image is currently loaded.
    /// </summary>
    public bool HasImage => _bitmap != null && !_bitmap.IsEmpty;

    /// <summary>
    /// Gets the native width of the loaded reference image.
    /// </summary>
    public int Width => _bitmap?.Width ?? 0;

    /// <summary>
    /// Gets the native height of the loaded reference image.
    /// </summary>
    public int Height => _bitmap?.Height ?? 0;

    /// <summary>
    /// Loads an image from the specified file path (PNG, JPEG, BMP, WEBP).
    /// </summary>
    public void LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Reference image not found at '{path}'.", path);
        }

        using var stream = File.OpenRead(path);
        LoadFromStream(stream, path);
    }

    /// <summary>
    /// Loads an image from an open readable stream.
    /// </summary>
    public void LoadFromStream(Stream stream, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var decoded = SKBitmap.Decode(stream);
        if (decoded == null)
        {
            throw new InvalidOperationException("Failed to decode image data into a valid Skia bitmap.");
        }

        var oldBitmap = _bitmap;
        _bitmap = decoded;
        _filePath = path;

        oldBitmap?.Dispose();

        OnPropertyChanged(nameof(Bitmap));
        OnPropertyChanged(nameof(HasImage));
        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Height));
        OnPropertyChanged(nameof(FilePath));
    }

    /// <summary>
    /// Clears and disposes the loaded reference image.
    /// </summary>
    public void Clear()
    {
        if (_bitmap != null)
        {
            _bitmap.Dispose();
            _bitmap = null;
            _filePath = null;

            OnPropertyChanged(nameof(Bitmap));
            OnPropertyChanged(nameof(HasImage));
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(FilePath));
        }
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            Clear();
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
