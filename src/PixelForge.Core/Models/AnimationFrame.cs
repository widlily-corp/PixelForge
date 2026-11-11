using CommunityToolkit.Mvvm.ComponentModel;

namespace PixelForge.Core.Models;

/// <summary>
/// Represents a discrete time step (frame) in the animation timeline.
/// Holds a dictionary mapping layer ID to the corresponding <see cref="AnimationCel"/>.
/// </summary>
public class AnimationFrame : ObservableObject
{
    private int _durationMs = 100;
    private int _frameNumber = 1;

    /// <summary>
    /// Gets the unique identifier of this frame.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets or sets the 1-based display number of this frame (e.g. 1, 2, 3...).
    /// </summary>
    public int FrameNumber
    {
        get => _frameNumber;
        set => SetProperty(ref _frameNumber, value);
    }

    /// <summary>
    /// Gets or sets the frame duration in milliseconds (default 100 ms = 10 FPS).
    /// </summary>
    public int DurationMs
    {
        get => _durationMs;
        set => SetProperty(ref _durationMs, Math.Max(1, value));
    }

    /// <summary>
    /// Gets the dictionary of cels mapped by layer identifier.
    /// </summary>
    public Dictionary<Guid, AnimationCel> Cels { get; } = new();

    /// <summary>
    /// Initializes a new animation frame with the specified duration and optional ID.
    /// </summary>
    /// <param name="durationMs">Duration in milliseconds (default: 100ms).</param>
    /// <param name="id">Optional unique identifier.</param>
    public AnimationFrame(int durationMs = 100, Guid? id = null)
    {
        Id = id ?? Guid.NewGuid();
        DurationMs = durationMs;
    }

    /// <summary>
    /// Creates a deep clone of this frame and all of its cels.
    /// </summary>
    /// <param name="newId">Whether to generate a new GUID or preserve the current ID.</param>
    public AnimationFrame Clone(bool newId = true)
    {
        var clone = new AnimationFrame(DurationMs, newId ? Guid.NewGuid() : Id);
        foreach (var kvp in Cels)
        {
            clone.Cels[kvp.Key] = kvp.Value.Clone();
        }

        return clone;
    }
}
