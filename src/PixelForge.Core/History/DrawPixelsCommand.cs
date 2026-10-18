using PixelForge.Core.Models;

namespace PixelForge.Core.History;

/// <summary>
/// Undoable command that applies or reverts pixel modifications on a layer.
/// </summary>
public class DrawPixelsCommand : IUndoableCommand
{
    public Guid LayerId { get; }
    public Layer Layer { get; }
    public IReadOnlyList<PixelDelta> Deltas { get; }
    public string Description { get; }

    public DrawPixelsCommand(Layer layer, IReadOnlyList<PixelDelta> deltas, string description = "Draw Pixels")
    {
        Layer = layer ?? throw new ArgumentNullException(nameof(layer));
        LayerId = layer.Id;
        Deltas = deltas ?? throw new ArgumentNullException(nameof(deltas));
        Description = description;
    }

    public void Execute()
    {
        for (int i = 0; i < Deltas.Count; i++)
        {
            var delta = Deltas[i];
            Layer.SetPixel(delta.X, delta.Y, delta.NewColor);
        }
    }

    public void Undo()
    {
        for (int i = 0; i < Deltas.Count; i++)
        {
            var delta = Deltas[i];
            Layer.SetPixel(delta.X, delta.Y, delta.OldColor);
        }
    }
}
