# System Design: Frame Animation & Timeline Engine (PixelForge v0.2.0)

## 1. Intent & The "Why"
PixelForge is a modern, responsive, hardware-accelerated desktop pixel art editor (.NET 8, Avalonia UI 11, SkiaSharp). Prior to this update (v0.1.13), PixelForge was strictly a static illustration editor with multi-layer support.
However, in game development and professional digital art, animation is the primary differentiator between a hobbyist sketching tool and an industry-standard creation suite (such as Aseprite).
The goal of this "Super Update" is to introduce a complete, zero-allocation, 60+ FPS frame animation and timeline engine without regressing existing drawing tools or canvas performance.

---

## 2. Goals, Non-Goals, & Anti-Goals

### 2.1 Goals
- **Cel-Layer Matrix**: True multi-layer animation where layers represent continuous horizontal tracks and frames represent vertical discrete time steps.
- **Collapsible Bottom Timeline Dock**: Docked under the canvas with a `GridSplitter`, frame scrubber, playhead, cell selector, layer tracks, frame durations, and playback controls.
- **High-Performance Onion Skinning**: Real-time tinted ghosting (previous frames in red tint, next frames in blue tint) rendered directly in the SkiaSharp composite pass at 60+ FPS with zero memory allocation.
- **Dual Playback Model**:
  - *Main Canvas Playback*: Preview mode for reviewing motion in place. Drawing is paused during playback; clicking the canvas or pressing Space/Enter stops playback and returns to interactive editing on the current frame.
  - *Concurrent Mini-Preview Window*: Dedicated floating/docked window displaying looping animation at 1x-4x scale in real time while the artist continues drawing on the active frame.
- **Animation Tags**: Grouping ranges of frames into clips (e.g. "Idle", "Run", "Jump", "Attack") with custom colors, repeat counts, and playback loop modes (Forward, Reverse, Ping-Pong).
- **Industry-Standard Export Pipeline**:
  - Animated GIF encoder with palette quantization.
  - Spritesheet (Texture Atlas) generator: grid/strip layout, customizable padding, border, and **Aseprite-compatible JSON metadata** (out-of-the-box support for Unity, Godot, Phaser).
  - PNG Image Sequence (`frame_001.png`, `frame_002.png`, etc.).
- **Zero Regression for Existing 20+ Tools**: All existing tools (`PencilTool`, `EraserTool`, `FillTool`, `LineTool`, `MagicWandTool`, `DitherBrushTool`, etc.) continue working seamlessly via the active frame cel projection.
- **Undo/Redo & Backward Compatibility**:
  - Full undo/redo support for all frame operations (Add, Delete, Duplicate, Reorder, Change Duration) and cel pixel deltas.
  - Auto-navigation: invoking Undo/Redo automatically navigates the timeline to the frame affected by the command.
  - All layer operations (Add, Remove, Merge Down, Flip, Resize) operate globally across all frames in the timeline.
  - `.pxproj` format v2 specification with 100% backward compatibility for v1 projects.

### 2.2 Non-Goals
- Linked Cels (`LinkedCelId`) with shared buffers: explicitly deferred to v0.3.0 to keep memory management and Undo/Redo 100% deterministic. Each Cel in v0.2.0 owns its pixel buffer.
- Skeletal / Bone animation or inverse kinematics (PixelForge is strictly hand-crafted frame-by-frame pixel art).
- Audio track synchronization in this milestone (deferred to future minor releases).

### 2.3 Anti-Goals (What would make this a failure)
- Allocating memory (`new byte[]`, `LINQ`, closures) during playback or pointer movements on the canvas.
- Storing full canvas snapshots per frame in memory or undo history (sparse delta format must be strictly preserved).
- Slowing down canvas panning, zooming, or drawing when Onion Skinning is enabled.
- Breaking existing `.pxproj` project files created in versions 0.1.0–0.1.13.

---

## 3. Architecture & Trade-Offs

### 3.1 Chosen Approach: Facade Cel-Layer Architecture via `PixelCanvas` Projection
- **Concept**: `PixelCanvas` manages an `ObservableCollection<AnimationFrame> Frames`. Each `AnimationFrame` maps `LayerId -> AnimationCel`.
- **Projection Mechanism**: The active frame's cels are directly projected into the canvas layers. To achieve $O(1)$ zero-allocation frame switching, each `Layer` in `PixelCanvas.Layers` simply swaps its internal `PixelColor[] Pixels` reference with the corresponding `cel.Pixels` array (validating dimensions) and fires `InvalidateComposite()`.
- **Zero-Regression Tool Compatibility**: Tools draw into `ctx.ActiveLayer.SetPixel()`. Because `ActiveLayer.Pixels` points directly to the active cel's array, no tool code requires modification.

---

## 4. Architectural Domain Model (`PixelForge.Core`)

### 4.1 Data Structures

```csharp
namespace PixelForge.Core.Models;

/// <summary>
/// Represents a single cel: the intersection of a Layer track and an AnimationFrame.
/// Holds the raw pixel buffer for that specific layer on that specific frame.
/// </summary>
public class AnimationCel : ObservableObject
{
    public Guid LayerId { get; }
    public PixelColor[] Pixels { get; private set; }
    public bool IsEmpty { get; set; }

    public AnimationCel(Guid layerId, int width, int height)
    {
        LayerId = layerId;
        Pixels = new PixelColor[width * height];
        IsEmpty = true;
    }

    public AnimationCel(Guid layerId, PixelColor[] pixels)
    {
        LayerId = layerId;
        Pixels = pixels;
        IsEmpty = false;
    }

    public void SetPixels(PixelColor[] pixels)
    {
        Pixels = pixels ?? throw new ArgumentNullException(nameof(pixels));
    }
}

/// <summary>
/// Represents a discrete time step (frame) in the timeline.
/// </summary>
public class AnimationFrame : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public int DurationMs { get; set; } = 100; // Default 10 FPS (100ms)
    
    // Map of LayerId -> AnimationCel
    public Dictionary<Guid, AnimationCel> Cels { get; } = new();

    public AnimationFrame(int durationMs = 100)
    {
        DurationMs = durationMs;
    }
}

/// <summary>
/// Represents an animation tag grouping a range of frames.
/// </summary>
public class AnimationTag : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; } = "Animation";
    public int FromFrame { get; set; }
    public int ToFrame { get; set; }
    public PixelColor Color { get; set; } = new(0x4A, 0x90, 0xE2, 0xFF);
    public AnimationLoopMode LoopMode { get; set; } = AnimationLoopMode.Forward;
    public int RepeatCount { get; set; } = 0; // 0 = infinite loop
}

public enum AnimationLoopMode
{
    Forward,
    Reverse,
    PingPong
}

/// <summary>
/// Configures real-time onion skinning overlays.
/// </summary>
public class OnionSkinSettings : ObservableObject
{
    public bool IsEnabled { get; set; } = false;
    public int PrevFramesCount { get; set; } = 1; // 1 to 5
    public int NextFramesCount { get; set; } = 1; // 1 to 5
    public float MaxOpacity { get; set; } = 0.45f;
    public PixelColor PrevFrameTint { get; set; } = new(0xFF, 0x44, 0x44, 0x80); // Red tint
    public PixelColor NextFrameTint { get; set; } = new(0x44, 0x88, 0xFF, 0x80); // Blue tint
    public bool ActiveLayerOnly { get; set; } = false;
}
```

### 4.2 Timeline Management in `PixelCanvas`
`PixelCanvas` manages:
- `ObservableCollection<AnimationFrame> Frames`
- `ObservableCollection<AnimationTag> Tags`
- `OnionSkinSettings OnionSkin`
- `int CurrentFrameIndex`
- `SwitchToFrame(int newIndex)`:
  1. Clamps index to `[0, Frames.Count - 1]`.
  2. For each layer in `Layers`:
     - Finds the cel in `Frames[newIndex].Cels[layer.Id]`. If missing, creates an empty cel.
     - Swaps `layer.Pixels = cel.Pixels`.
  3. Updates `CurrentFrameIndex`.
  4. Triggers `InvalidateComposite()` and `FrameChanged` event.

### 4.3 Global Layer Operations across Timeline
All layer commands (`AddLayerCommand`, `RemoveLayerCommand`, `MergeLayerDownCommand`, `DuplicateLayerCommand`, `FlipLayerCommand`, `ResizeCanvasCommand`) operate across all frames:
- **Add Layer**: Adds the layer and generates an empty cel in every frame.
- **Remove Layer**: Removes the layer and purges its cels from all frames.
- **Merge Down**: Blends upper and lower cels for every frame in the timeline.
- **Resize Canvas**: Reallocates cels for all frames according to new dimensions.

---

## 5. SkiaSharp Graphics Pipeline & Onion Skinning (`PixelForge.App`)

### 5.1 Onion Skinning Rendering Strategy (Zero Alloc)
Inside `PixelCanvasControl.Render()` (`ICustomDrawOperation`):
1. **Pass 0: Background Checkerboard & Reference Overlay** (existing).
2. **Pass 1: Onion Skinning (Previous Frames)**:
   - For $k = 1 \dots PrevFramesCount$:
     Calculate frame index $idx = CurrentFrame - k$. If within bounds:
     - Composite visible cels for frame $idx$ using cached pre-allocated Skia bitmap.
     - Apply cached `SKColorFilter.CreateBlendMode(prevTint, SKBlendMode.SrcATop)`.
     - Fade opacity: $\alpha_k = MaxOpacity \times (1.0 - \frac{k - 1}{PrevFramesCount})$.
     - Draw to canvas with nearest-neighbor sampling.
3. **Pass 2: Onion Skinning (Next Frames)**:
   - For $k = 1 \dots NextFramesCount$:
     Calculate frame index $idx = CurrentFrame + k$. If within bounds, composite with blue tint and opacity fading.
4. **Pass 3: Current Active Frame Composite** (existing composite bitmap).
5. **Pass 4: Dynamic Overlays** (Selection marching ants, symmetry axes, tool preview, pixel grid).

---

## 6. Playback Engine & Mini-Preview Window

### 6.1 Playback Concurrency Model
1. **Main Canvas Playback**:
   - Toggled via `Space` / `Enter` or Play/Pause button on Timeline.
   - When playing, `PlaybackService` ticks `CurrentFrameIndex` according to each frame's `DurationMs`.
   - Drawing tools are inactive during playback; clicking anywhere on canvas immediately pauses playback at the clicked frame.
2. **Mini-Preview Window (`AnimationPreviewWindow.axaml`)**:
   - Operates an independent off-screen composite loop.
   - Reads directly from `Frames[i].Cels` without changing `PixelCanvas.CurrentFrameIndex`.
   - The user can draw on the main canvas while the Mini-Preview continuously animates at 1x-4x scale.

---

## 7. Timeline UI / UX Specification

### 7.1 Bottom Collapsible Dock Layout (`TimelineControl.axaml`)
- Positioned in `MainWindow.axaml` at the bottom of the canvas area, separated by a `GridSplitter`.
- **Top Control Bar**:
  - Play / Pause button (`Space` / `Enter`).
  - Step Backward (`,`) / Step Forward (`.`).
  - First Frame (`Home`) / Last Frame (`End`).
  - Loop Mode Toggle (Repeat all vs Repeat current tag).
  - FPS Stepper / Global Speed multiplier.
  - Onion Skinning Toggle (`O`) + Popover configuration.
  - Add Frame (`F6` / `Alt+N`), Duplicate Frame (`F5`), Delete Frame (`Delete`).
  - Add / Edit Tag button.
  - Collapse / Expand button (`Tab` / `F7`).
- **Tag Header Track**: Displays color badges for tags over their respective frame column spans.
- **Frame Columns & Cel Matrix**:
  - Header: Frame numbers (1, 2, 3...) with duration badges (e.g. `100ms`).
  - Rows: Layer names matching the Layer panel.
  - Cells: Circular/rectangular indicators showing empty cels vs filled cels. Clicking selects active frame and layer. Dragging reorders frames.

---

## 8. Export Pipeline (`ExportService`)

### 8.1 Animated GIF Export
- Pure C# / SkiaSharp zero-dependency GIF encoder.
- Quantizes 32-bit RGBA frames into 8-bit indexed palette (256 colors) with transparent color key.
- Supports individual frame delay times and loop count (0 = infinite).

### 8.2 Spritesheet (Texture Atlas) Export
- Configurable layout modes:
  - **Grid**: $C$ columns $\times$ $R$ rows.
  - **Horizontal Strip**: 1 row of $N$ frames.
  - **Vertical Strip**: $N$ rows of 1 frame.
- Padding: Inner padding and border margin.
- Scale: Nearest-neighbor integer scaling (1x, 2x, 4x, 8x).
- **Aseprite-Compatible JSON Format**:
```json
{
  "frames": [
    {
      "filename": "frame_0",
      "frame": { "x": 0, "y": 0, "w": 32, "h": 32 },
      "rotated": false,
      "trimmed": false,
      "spriteSourceSize": { "x": 0, "y": 0, "w": 32, "h": 32 },
      "sourceSize": { "w": 32, "h": 32 },
      "duration": 100
    }
  ],
  "meta": {
    "app": "PixelForge",
    "version": "0.2.0",
    "size": { "w": 128, "h": 32 },
    "scale": "1",
    "frameTags": [
      { "name": "Idle", "from": 0, "to": 3, "direction": "forward" }
    ]
  }
}
```

### 8.3 PNG Image Sequence Export
- Exports `filename_000.png`, `filename_001.png`, ... to the selected target directory.

---

## 9. Undo / Redo & Timeline Navigation

- `DrawPixelsCommand`:
  - Augmented to capture `FrameIndex` at time of creation: `public int FrameIndex { get; }`.
  - In `Execute()` and `Undo()`, if `Canvas.CurrentFrameIndex != FrameIndex`, the command invokes `Canvas.SwitchToFrame(FrameIndex)` first, guaranteeing the user sees the undone/redone stroke on the correct frame.
- Dedicated Timeline Commands:
  - `AddFrameCommand(int insertIndex, AnimationFrame frame)`
  - `RemoveFrameCommand(int frameIndex, AnimationFrame removedFrame)`
  - `DuplicateFrameCommand(int sourceIndex, int targetIndex)`
  - `ReorderFramesCommand(int oldIndex, int newIndex)`
  - `ChangeFrameDurationCommand(int frameIndex, int oldDuration, int newDuration)`
  - `AddTagCommand`, `RemoveTagCommand`, `ModifyTagCommand`

---

## 10. Backward Compatibility & Project File (.pxproj v2)

### 10.1 Schema Definition
```json
{
  "formatVersion": 2,
  "appVersion": "0.2.0",
  "width": 32,
  "height": 32,
  "palette": [ "#000000", "#FFFFFF" ],
  "layers": [
    { "id": "guid-1", "name": "Background", "isVisible": true, "opacity": 1.0 },
    { "id": "guid-2", "name": "Character", "isVisible": true, "opacity": 1.0 }
  ],
  "frames": [
    {
      "id": "frame-1",
      "durationMs": 100,
      "cels": [
        { "layerId": "guid-1", "pixels": "base64..." },
        { "layerId": "guid-2", "pixels": "base64..." }
      ]
    }
  ],
  "tags": [
    { "name": "Idle", "from": 0, "to": 3, "color": "#4A90E2FF", "loopMode": "Forward" }
  ],
  "activeFrameIndex": 0,
  "activeLayerIndex": 1
}
```

### 10.2 Backward Compatibility Guarantees
- Loading a `formatVersion == 1` file (or missing `frames`):
  - Synthesizes 1 default frame with 100ms duration.
  - Converts existing layer pixels into Frame 0 cels.
  - Zero data loss, seamless loading.
- Atomic saving (`.tmp` -> `File.Move(..., overwrite: true)`) preserved.
