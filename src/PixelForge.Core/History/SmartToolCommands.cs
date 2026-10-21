using PixelForge.Core.Models;
using PixelForge.Core.Tools;

namespace PixelForge.Core.History;

/// <summary>
/// Undoable command that creates or removes a smart pixel outline around non-transparent pixels.
/// </summary>
public class OutlineCommand : IUndoableCommand
{
    public Layer Layer { get; }
    public PixelColor OutlineColor { get; }
    public bool Is8Way { get; }
    public int Thickness { get; }
    public SelectionModel? Selection { get; }
    public IReadOnlyList<PixelDelta> Deltas { get; }
    public string Description { get; }

    public OutlineCommand(
        Layer layer,
        PixelColor outlineColor,
        bool is8Way = false,
        int thickness = 1,
        SelectionModel? selection = null,
        string description = "Generate Outline")
    {
        Layer = layer ?? throw new ArgumentNullException(nameof(layer));
        OutlineColor = outlineColor;
        Is8Way = is8Way;
        Thickness = thickness;
        Selection = selection;
        Description = description;
        Deltas = SmartTools.ComputeOutlineDeltas(layer, outlineColor, is8Way, thickness, selection);
    }

    public OutlineCommand(
        Layer layer,
        IReadOnlyList<PixelDelta> deltas,
        string description = "Generate Outline")
    {
        Layer = layer ?? throw new ArgumentNullException(nameof(layer));
        Deltas = deltas ?? throw new ArgumentNullException(nameof(deltas));
        Description = description;
    }

    public void Execute()
    {
        SmartTools.ApplyDeltas(Layer, Deltas);
    }

    public void Undo()
    {
        SmartTools.RevertDeltas(Layer, Deltas);
    }
}

/// <summary>
/// Undoable command that generates or removes an offset drop shadow.
/// </summary>
public class ShadowCommand : IUndoableCommand
{
    public Layer Layer { get; }
    public PixelColor ShadowColor { get; }
    public int Dx { get; }
    public int Dy { get; }
    public float Opacity { get; }
    public SelectionModel? Selection { get; }
    public IReadOnlyList<PixelDelta> Deltas { get; }
    public string Description { get; }

    public ShadowCommand(
        Layer layer,
        PixelColor shadowColor,
        int dx = 1,
        int dy = 1,
        float opacity = 0.5f,
        SelectionModel? selection = null,
        string description = "Generate Shadow")
    {
        Layer = layer ?? throw new ArgumentNullException(nameof(layer));
        ShadowColor = shadowColor;
        Dx = dx;
        Dy = dy;
        Opacity = opacity;
        Selection = selection;
        Description = description;
        Deltas = SmartTools.ComputeShadowDeltas(layer, shadowColor, dx, dy, opacity, selection);
    }

    public ShadowCommand(
        Layer layer,
        IReadOnlyList<PixelDelta> deltas,
        string description = "Generate Shadow")
    {
        Layer = layer ?? throw new ArgumentNullException(nameof(layer));
        Deltas = deltas ?? throw new ArgumentNullException(nameof(deltas));
        Description = description;
    }

    public void Execute()
    {
        SmartTools.ApplyDeltas(Layer, Deltas);
    }

    public void Undo()
    {
        SmartTools.RevertDeltas(Layer, Deltas);
    }
}

/// <summary>
/// Undoable command that replaces occurrences of a color across the layer or selection.
/// </summary>
public class ReplaceColorCommand : IUndoableCommand
{
    public Layer Layer { get; }
    public PixelColor TargetColor { get; }
    public PixelColor ReplacementColor { get; }
    public int Tolerance { get; }
    public SelectionModel? Selection { get; }
    public IReadOnlyList<PixelDelta> Deltas { get; }
    public string Description { get; }

    public ReplaceColorCommand(
        Layer layer,
        PixelColor targetColor,
        PixelColor replacementColor,
        int tolerance = 0,
        SelectionModel? selection = null,
        string description = "Replace Color")
    {
        Layer = layer ?? throw new ArgumentNullException(nameof(layer));
        TargetColor = targetColor;
        ReplacementColor = replacementColor;
        Tolerance = tolerance;
        Selection = selection;
        Description = description;
        Deltas = SmartTools.ComputeReplaceColorDeltas(layer, targetColor, replacementColor, tolerance, selection);
    }

    public ReplaceColorCommand(
        Layer layer,
        IReadOnlyList<PixelDelta> deltas,
        string description = "Replace Color")
    {
        Layer = layer ?? throw new ArgumentNullException(nameof(layer));
        Deltas = deltas ?? throw new ArgumentNullException(nameof(deltas));
        Description = description;
    }

    public void Execute()
    {
        SmartTools.ApplyDeltas(Layer, Deltas);
    }

    public void Undo()
    {
        SmartTools.RevertDeltas(Layer, Deltas);
    }
}
