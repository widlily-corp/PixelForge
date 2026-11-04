using CommunityToolkit.Mvvm.ComponentModel;

namespace PixelForge.Core.Models;

/// <summary>
/// Specifies the playback loop direction mode for an animation tag.
/// </summary>
public enum AnimationLoopMode
{
    Forward,
    Reverse,
    PingPong
}

/// <summary>
/// Represents an animation tag grouping a range of timeline frames (e.g., "Idle", "Run", "Attack").
/// </summary>
public class AnimationTag : ObservableObject
{
    private string _name = "Animation";
    private int _fromFrame;
    private int _toFrame;
    private PixelColor _color = new(0x4A, 0x90, 0xE2, 0xFF);
    private AnimationLoopMode _loopMode = AnimationLoopMode.Forward;
    private int _repeatCount;

    /// <summary>
    /// Gets the unique identifier of this tag.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets or sets the tag label name.
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the starting frame index (0-based, inclusive).
    /// </summary>
    public int FromFrame
    {
        get => _fromFrame;
        set => SetProperty(ref _fromFrame, value);
    }

    /// <summary>
    /// Gets or sets the ending frame index (0-based, inclusive).
    /// </summary>
    public int ToFrame
    {
        get => _toFrame;
        set => SetProperty(ref _toFrame, value);
    }

    /// <summary>
    /// Gets or sets the display color for this tag's UI marker.
    /// </summary>
    public PixelColor Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }

    /// <summary>
    /// Gets or sets the playback loop direction mode.
    /// </summary>
    public AnimationLoopMode LoopMode
    {
        get => _loopMode;
        set => SetProperty(ref _loopMode, value);
    }

    /// <summary>
    /// Gets or sets the number of times this tag repeats during playback (0 = infinite).
    /// </summary>
    public int RepeatCount
    {
        get => _repeatCount;
        set => SetProperty(ref _repeatCount, Math.Max(0, value));
    }

    /// <summary>
    /// Initializes a new animation tag with an optional ID.
    /// </summary>
    /// <param name="id">Optional unique identifier.</param>
    public AnimationTag(Guid? id = null)
    {
        Id = id ?? Guid.NewGuid();
    }
}
