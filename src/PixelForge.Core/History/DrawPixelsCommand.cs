using PixelForge.Core.Models;

namespace PixelForge.Core.History;

/// <summary>
/// Undoable command that applies or reverts pixel modifications on a layer.
/// Supports timeline frame auto-navigation when associated with a canvas and frame index.
/// </summary>
public class DrawPixelsCommand : IUndoableCommand
{
    public Guid LayerId { get; }
    public Layer Layer { get; }
    public IReadOnlyList<PixelDelta> Deltas { get; }
    public string Description { get; }
    public PixelCanvas? Canvas { get; }
    public int? FrameIndex { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="DrawPixelsCommand"/> without timeline auto-navigation.
    /// </summary>
    public DrawPixelsCommand(Layer layer, IReadOnlyList<PixelDelta> deltas, string description = "Draw Pixels")
        : this(layer, deltas, null, null, description)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DrawPixelsCommand"/> with optional timeline auto-navigation.
    /// </summary>
    public DrawPixelsCommand(
        Layer layer,
        IReadOnlyList<PixelDelta> deltas,
        PixelCanvas? canvas,
        int? frameIndex = null,
        string description = "Draw Pixels")
    {
        Layer = layer ?? throw new ArgumentNullException(nameof(layer));
        LayerId = layer.Id;
        Deltas = deltas ?? throw new ArgumentNullException(nameof(deltas));
        Canvas = canvas;
        FrameIndex = frameIndex ?? canvas?.CurrentFrameIndex;
        Description = description;
    }

    public void Execute()
    {
        if (Canvas != null && FrameIndex.HasValue && Canvas.CurrentFrameIndex != FrameIndex.Value)
        {
            Canvas.SwitchToFrame(FrameIndex.Value);
        }

        for (int i = 0; i < Deltas.Count; i++)
        {
            var delta = Deltas[i];
            Layer.SetPixel(delta.X, delta.Y, delta.NewColor);
        }
        Layer.Invalidate();
    }

    public void Undo()
    {
        if (Canvas != null && FrameIndex.HasValue && Canvas.CurrentFrameIndex != FrameIndex.Value)
        {
            Canvas.SwitchToFrame(FrameIndex.Value);
        }

        for (int i = 0; i < Deltas.Count; i++)
        {
            var delta = Deltas[i];
            Layer.SetPixel(delta.X, delta.Y, delta.OldColor);
        }
        Layer.Invalidate();
    }
}
