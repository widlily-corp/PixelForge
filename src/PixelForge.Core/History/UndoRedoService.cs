using CommunityToolkit.Mvvm.ComponentModel;

namespace PixelForge.Core.History;

/// <summary>
/// Manages the command history stack for undo and redo operations with capacity limits.
/// </summary>
public partial class UndoRedoService : ObservableObject
{
    public const int DefaultMaxSteps = 150;

    private readonly List<IUndoableCommand> _undoStack = new();
    private readonly List<IUndoableCommand> _redoStack = new();

    public int MaxSteps { get; }

    public IReadOnlyList<IUndoableCommand> UndoStack => _undoStack;
    public IReadOnlyList<IUndoableCommand> RedoStack => _redoStack;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public UndoRedoService(int maxSteps = DefaultMaxSteps)
    {
        if (maxSteps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSteps), "MaxSteps must be greater than zero.");
        }

        MaxSteps = maxSteps;
    }

    /// <summary>
    /// Executes a command, adds it to the undo stack, and clears the redo stack.
    /// </summary>
    public void ExecuteCommand(IUndoableCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.Execute();
        _undoStack.Add(command);

        while (_undoStack.Count > MaxSteps)
        {
            _undoStack.RemoveAt(0);
        }

        _redoStack.Clear();
        NotifyStateChanged();
    }

    /// <summary>
    /// Undoes the most recent command.
    /// </summary>
    public bool Undo()
    {
        if (!CanUndo)
        {
            return false;
        }

        int lastIndex = _undoStack.Count - 1;
        var command = _undoStack[lastIndex];
        _undoStack.RemoveAt(lastIndex);

        command.Undo();
        _redoStack.Add(command);

        NotifyStateChanged();
        return true;
    }

    /// <summary>
    /// Redoes the most recently undone command.
    /// </summary>
    public bool Redo()
    {
        if (!CanRedo)
        {
            return false;
        }

        int lastIndex = _redoStack.Count - 1;
        var command = _redoStack[lastIndex];
        _redoStack.RemoveAt(lastIndex);

        command.Execute();
        _undoStack.Add(command);

        NotifyStateChanged();
        return true;
    }

    /// <summary>
    /// Clears both undo and redo histories.
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoStack));
        OnPropertyChanged(nameof(RedoStack));
    }
}
