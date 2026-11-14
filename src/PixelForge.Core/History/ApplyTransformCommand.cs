using PixelForge.Core.Models;
using SkiaSharp;

namespace PixelForge.Core.History;

/// <summary>
/// Reversible transactional command that applies transformed pixels to a layer and manages sparse pixel deltas.
/// </summary>
public class ApplyTransformCommand : IUndoableCommand
{
    public Layer Layer { get; }
    public IReadOnlyList<PixelDelta> Deltas { get; }
    public SKRectI OriginalBounds { get; }
    public SKRectI TransformedBounds { get; }
    public SelectionModel? Selection { get; }
    public PixelCanvas? Canvas { get; }
    public int? FrameIndex { get; }
    public string Description { get; }

    public ApplyTransformCommand(
        Layer layer,
        IReadOnlyList<PixelDelta> deltas,
        SKRectI originalBounds,
        SKRectI transformedBounds,
        SelectionModel? selection = null,
        PixelCanvas? canvas = null,
        int? frameIndex = null,
        string description = "Apply Free Transform")
    {
        Layer = layer ?? throw new ArgumentNullException(nameof(layer));
        Deltas = deltas ?? throw new ArgumentNullException(nameof(deltas));
        OriginalBounds = originalBounds;
        TransformedBounds = transformedBounds;
        Selection = selection;
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

        if (Selection != null && Selection.IsActive)
        {
            Selection.Bounds = TransformedBounds;
            Selection.OffsetX = 0;
            Selection.OffsetY = 0;
        }
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

        if (Selection != null && Selection.IsActive)
        {
            Selection.Bounds = OriginalBounds;
            Selection.OffsetX = 0;
            Selection.OffsetY = 0;
        }
    }
}
