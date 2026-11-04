using PixelForge.Core.Models;

namespace PixelForge.Core.History;

/// <summary>
/// Undoable command that inserts a new animation frame at a specified position.
/// </summary>
public class AddFrameCommand : IUndoableCommand
{
    public PixelCanvas Canvas { get; }
    public int InsertIndex { get; }
    public AnimationFrame Frame { get; }
    public string Description { get; }

    public AddFrameCommand(PixelCanvas canvas, int insertIndex, AnimationFrame frame, string description = "Add Frame")
    {
        Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        InsertIndex = insertIndex;
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
        Description = description;
    }

    public AddFrameCommand(PixelCanvas canvas, AnimationFrame frame, string description = "Add Frame")
        : this(canvas, canvas?.Frames.Count ?? 0, frame, description)
    {
    }

    public void Execute()
    {
        int target = Math.Clamp(InsertIndex, 0, Canvas.Frames.Count);
        if (!Canvas.Frames.Contains(Frame))
        {
            Canvas.Frames.Insert(target, Frame);
        }
        Canvas.SwitchToFrame(target);
    }

    public void Undo()
    {
        int idx = Canvas.Frames.IndexOf(Frame);
        if (idx >= 0)
        {
            Canvas.RemoveFrame(idx);
        }
    }
}

/// <summary>
/// Undoable command that removes an animation frame from the timeline.
/// </summary>
public class RemoveFrameCommand : IUndoableCommand
{
    public PixelCanvas Canvas { get; }
    public int FrameIndex { get; }
    public AnimationFrame Frame { get; }
    public string Description { get; }

    public RemoveFrameCommand(PixelCanvas canvas, int frameIndex, AnimationFrame frame, string description = "Remove Frame")
    {
        Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        FrameIndex = frameIndex;
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
        Description = description;
    }

    public void Execute()
    {
        int idx = Canvas.Frames.IndexOf(Frame);
        if (idx >= 0)
        {
            Canvas.RemoveFrame(idx);
        }
        else if (FrameIndex >= 0 && FrameIndex < Canvas.Frames.Count)
        {
            Canvas.RemoveFrame(FrameIndex);
        }
    }

    public void Undo()
    {
        int target = Math.Clamp(FrameIndex, 0, Canvas.Frames.Count);
        if (!Canvas.Frames.Contains(Frame))
        {
            Canvas.Frames.Insert(target, Frame);
        }
        Canvas.SwitchToFrame(target);
    }
}

/// <summary>
/// Undoable command that duplicates an animation frame and inserts it at the specified target index.
/// </summary>
public class DuplicateFrameCommand : IUndoableCommand
{
    public PixelCanvas Canvas { get; }
    public int SourceIndex { get; }
    public int NewIndex { get; }
    public AnimationFrame DuplicatedFrame { get; }
    public string Description { get; }

    public DuplicateFrameCommand(
        PixelCanvas canvas,
        int sourceIndex,
        int newIndex,
        AnimationFrame duplicatedFrame,
        string description = "Duplicate Frame")
    {
        Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        SourceIndex = sourceIndex;
        NewIndex = newIndex;
        DuplicatedFrame = duplicatedFrame ?? throw new ArgumentNullException(nameof(duplicatedFrame));
        Description = description;
    }

    public void Execute()
    {
        int target = Math.Clamp(NewIndex, 0, Canvas.Frames.Count);
        if (!Canvas.Frames.Contains(DuplicatedFrame))
        {
            Canvas.Frames.Insert(target, DuplicatedFrame);
        }
        Canvas.SwitchToFrame(target);
    }

    public void Undo()
    {
        int idx = Canvas.Frames.IndexOf(DuplicatedFrame);
        if (idx >= 0)
        {
            Canvas.RemoveFrame(idx);
        }
        Canvas.SwitchToFrame(Math.Clamp(SourceIndex, 0, Canvas.Frames.Count - 1));
    }
}

/// <summary>
/// Undoable command that reorders an animation frame from an old index to a new index in the timeline.
/// </summary>
public class ReorderFramesCommand : IUndoableCommand
{
    public PixelCanvas Canvas { get; }
    public int OldIndex { get; }
    public int NewIndex { get; }
    public string Description { get; }

    public ReorderFramesCommand(PixelCanvas canvas, int oldIndex, int newIndex, string description = "Reorder Frames")
    {
        Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        if (oldIndex < 0 || oldIndex >= canvas.Frames.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(oldIndex), "Old index is out of bounds.");
        }
        if (newIndex < 0 || newIndex >= canvas.Frames.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(newIndex), "New index is out of bounds.");
        }

        OldIndex = oldIndex;
        NewIndex = newIndex;
        Description = description;
    }

    public void Execute()
    {
        if (OldIndex != NewIndex && OldIndex >= 0 && OldIndex < Canvas.Frames.Count && NewIndex >= 0 && NewIndex < Canvas.Frames.Count)
        {
            Canvas.Frames.Move(OldIndex, NewIndex);
            Canvas.SwitchToFrame(NewIndex);
        }
    }

    public void Undo()
    {
        if (OldIndex != NewIndex && NewIndex >= 0 && NewIndex < Canvas.Frames.Count && OldIndex >= 0 && OldIndex < Canvas.Frames.Count)
        {
            Canvas.Frames.Move(NewIndex, OldIndex);
            Canvas.SwitchToFrame(OldIndex);
        }
    }
}

/// <summary>
/// Undoable command that modifies the duration of an animation frame.
/// </summary>
public class ChangeFrameDurationCommand : IUndoableCommand
{
    public AnimationFrame Frame { get; }
    public int OldDuration { get; }
    public int NewDuration { get; }
    public string Description { get; }

    public ChangeFrameDurationCommand(
        AnimationFrame frame,
        int oldDuration,
        int newDuration,
        string description = "Change Frame Duration")
    {
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
        OldDuration = oldDuration;
        NewDuration = newDuration;
        Description = description;
    }

    public ChangeFrameDurationCommand(
        AnimationFrame frame,
        int newDuration,
        string description = "Change Frame Duration")
        : this(frame, frame?.DurationMs ?? 100, newDuration, description)
    {
    }

    public void Execute()
    {
        Frame.DurationMs = NewDuration;
    }

    public void Undo()
    {
        Frame.DurationMs = OldDuration;
    }
}

/// <summary>
/// Undoable command that adds a new animation tag to the canvas.
/// </summary>
public class AddTagCommand : IUndoableCommand
{
    public PixelCanvas Canvas { get; }
    public AnimationTag Tag { get; }
    public string Description { get; }

    public AddTagCommand(PixelCanvas canvas, AnimationTag tag, string description = "Add Tag")
    {
        Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        Tag = tag ?? throw new ArgumentNullException(nameof(tag));
        Description = description;
    }

    public void Execute()
    {
        if (!Canvas.Tags.Contains(Tag))
        {
            Canvas.Tags.Add(Tag);
        }
    }

    public void Undo()
    {
        Canvas.Tags.Remove(Tag);
    }
}

/// <summary>
/// Undoable command that removes an animation tag from the canvas.
/// </summary>
public class RemoveTagCommand : IUndoableCommand
{
    public PixelCanvas Canvas { get; }
    public AnimationTag Tag { get; }
    public int Index { get; }
    public string Description { get; }

    public RemoveTagCommand(PixelCanvas canvas, AnimationTag tag, string description = "Remove Tag")
    {
        Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        Tag = tag ?? throw new ArgumentNullException(nameof(tag));
        Index = canvas.Tags.IndexOf(tag);
        Description = description;
    }

    public void Execute()
    {
        Canvas.Tags.Remove(Tag);
    }

    public void Undo()
    {
        if (!Canvas.Tags.Contains(Tag))
        {
            int target = Index >= 0 ? Math.Clamp(Index, 0, Canvas.Tags.Count) : Canvas.Tags.Count;
            Canvas.Tags.Insert(target, Tag);
        }
    }
}
