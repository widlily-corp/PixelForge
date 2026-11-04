using CommunityToolkit.Mvvm.ComponentModel;

namespace PixelForge.Core.Models;

/// <summary>
/// Configures real-time onion skinning overlays on the canvas during animation editing.
/// </summary>
public class OnionSkinSettings : ObservableObject
{
    private bool _isEnabled;
    private int _prevFramesCount = 1;
    private int _nextFramesCount = 1;
    private float _maxOpacity = 0.45f;
    private PixelColor _prevFrameTint = new(0xFF, 0x44, 0x44, 0x80); // Translucent red
    private PixelColor _nextFrameTint = new(0x44, 0x88, 0xFF, 0x80); // Translucent blue
    private bool _activeLayerOnly;

    /// <summary>
    /// Gets or sets whether onion skinning overlay rendering is enabled.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    /// <summary>
    /// Gets or sets the number of preceding frames to display (clamped to [1, 5]).
    /// </summary>
    public int PrevFramesCount
    {
        get => _prevFramesCount;
        set => SetProperty(ref _prevFramesCount, Math.Clamp(value, 1, 5));
    }

    /// <summary>
    /// Gets or sets the number of succeeding frames to display (clamped to [1, 5]).
    /// </summary>
    public int NextFramesCount
    {
        get => _nextFramesCount;
        set => SetProperty(ref _nextFramesCount, Math.Clamp(value, 1, 5));
    }

    /// <summary>
    /// Gets or sets the maximum opacity for the closest ghost frame (clamped to [0.0, 1.0]).
    /// </summary>
    public float MaxOpacity
    {
        get => _maxOpacity;
        set => SetProperty(ref _maxOpacity, Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the tint color applied to preceding frames (default: red tint).
    /// </summary>
    public PixelColor PrevFrameTint
    {
        get => _prevFrameTint;
        set => SetProperty(ref _prevFrameTint, value);
    }

    /// <summary>
    /// Gets or sets the tint color applied to succeeding frames (default: blue tint).
    /// </summary>
    public PixelColor NextFrameTint
    {
        get => _nextFrameTint;
        set => SetProperty(ref _nextFrameTint, value);
    }

    /// <summary>
    /// Gets or sets whether onion skinning is restricted only to the active layer rather than the full composite.
    /// </summary>
    public bool ActiveLayerOnly
    {
        get => _activeLayerOnly;
        set => SetProperty(ref _activeLayerOnly, value);
    }
}
