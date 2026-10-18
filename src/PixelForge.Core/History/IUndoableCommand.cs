namespace PixelForge.Core.History;

/// <summary>
/// Defines an operation that can be executed and undone.
/// </summary>
public interface IUndoableCommand
{
    /// <summary>
    /// Human-readable description of the operation (e.g., "Draw stroke", "Add layer").
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Executes or re-executes the command.
    /// </summary>
    void Execute();

    /// <summary>
    /// Undoes the command, reverting state to before execution.
    /// </summary>
    void Undo();
}
