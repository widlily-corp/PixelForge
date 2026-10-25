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

    private int _maxSteps;

    /// <summary>
    /// Gets or sets the maximum number of undo steps retained in memory.
    /// </summary>
    public int MaxSteps
    {
        get => _maxSteps;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "MaxSteps must be greater than zero.");
            }
            _maxSteps = value;
            while (_undoStack.Count > _maxSteps)
            {
                _undoStack.RemoveAt(0);
            }
        }
    }

    public IReadOnlyList<IUndoableCommand> UndoStack => _undoStack;
    public IReadOnlyList<IUndoableCommand> RedoStack => _redoStack;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Event raised whenever a command is executed, undone, redone, or the history stack is cleared.
    /// </summary>
    public event Action? HistoryChanged;

    /// <summary>
    /// Event raised when a command is newly executed.
    /// </summary>
    public event Action<IUndoableCommand>? CommandExecuted;

    /// <summary>
    /// Event raised when a command is undone.
    /// </summary>
    public event Action<IUndoableCommand>? CommandUndone;

    /// <summary>
    /// Event raised when a command is redone.
    /// </summary>
    public event Action<IUndoableCommand>? CommandRedone;

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
        CommandExecuted?.Invoke(command);
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
        CommandUndone?.Invoke(command);
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
        CommandRedone?.Invoke(command);
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
        HistoryChanged?.Invoke();
    }
}
