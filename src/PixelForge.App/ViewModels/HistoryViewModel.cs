using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelForge.Core.History;

namespace PixelForge.App.ViewModels;

/// <summary>
/// ViewModel exposing Undo/Redo stack state and commands to the UI.
/// </summary>
public partial class HistoryViewModel : ViewModelBase
{
    private readonly UndoRedoService _undoRedoService;

    public bool CanUndo => _undoRedoService.CanUndo;

    public bool CanRedo => _undoRedoService.CanRedo;

    public string UndoDescription => _undoRedoService.UndoStack.Count > 0
        ? _undoRedoService.UndoStack[^1].Description
        : "Undo";

    public string RedoDescription => _undoRedoService.RedoStack.Count > 0
        ? _undoRedoService.RedoStack[^1].Description
        : "Redo";

    public UndoRedoService Service => _undoRedoService;

    /// <summary>
    /// Event forwarded when any command is executed, undone, redone, or cleared.
    /// </summary>
    public event Action? HistoryChanged;

    public HistoryViewModel(UndoRedoService undoRedoService)
    {
        _undoRedoService = undoRedoService ?? throw new ArgumentNullException(nameof(undoRedoService));
        _undoRedoService.PropertyChanged += OnUndoRedoServicePropertyChanged;
        _undoRedoService.HistoryChanged += () => HistoryChanged?.Invoke();
    }

    private void OnUndoRedoServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UndoRedoService.CanUndo) ||
            e.PropertyName == nameof(UndoRedoService.UndoStack))
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(UndoDescription));
            UndoCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName == nameof(UndoRedoService.CanRedo) ||
            e.PropertyName == nameof(UndoRedoService.RedoStack))
        {
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(RedoDescription));
            RedoCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    public void Undo()
    {
        _undoRedoService.Undo();
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    public void Redo()
    {
        _undoRedoService.Redo();
    }

    [RelayCommand]
    public void Clear()
    {
        _undoRedoService.Clear();
    }
}
