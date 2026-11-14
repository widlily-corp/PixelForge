# System Design: Free Transform Tool & Pixel Resampling Engine (PixelForge v0.3.0 - Phase 1)

## 1. Intent & The "Why"
PixelForge is a high-performance, hardware-accelerated desktop pixel art editor (.NET 8, Avalonia UI 11.1.3, SkiaSharp).
In version v0.2.1, artists gained frame-by-frame animation and timeline capabilities. However, pixel manipulation is currently restricted to moving rectangular selections and discrete 90-degree rotations (`Flip H/V`, `Rotate 90°/180°`).
Artists creating characters, props, and animations constantly need to:
- Scale elements up or down (with optional aspect ratio lock).
- Rotate elements by arbitrary angles (e.g. tilting a sword, rotating an arm during a swing).
- Flip or mirror regions interactively.
- Apply high-fidelity pixel-art resampling without bilinear blur, maintaining crisp pixel edges and palette fidelity.

The goal of Phase 1 is to introduce a dedicated **Free Transform Tool (`TransformTool`)** with direct-manipulation handles (<kbd>Ctrl+T</kbd> / <kbd>T</kbd>), an interactive GPU-accelerated preview overlay, RotSprite and Nearest-Neighbor resampling algorithms, and transactional Undo/Redo integration.

---

## 2. Goals, Non-Goals, & Anti-Goals

### 2.1 Goals
- **Dedicated Transform Tool (`TransformTool`)**:
  - Activated via shortcut <kbd>Ctrl+T</kbd>, <kbd>T</kbd>, or tool panel button.
  - Automatically targets active selection if present; otherwise targets the bounding box of non-empty pixels on the active layer/cel.
- **Direct Manipulation On-Canvas Overlay**:
  - Bounding box with 8 resize handles (4 corners, 4 edge midpoints).
  - Rotation handle located on a stem above the top-edge midpoint.
  - Center pivot indicator (default at bounding box center).
  - Dragging inside the box translates (moves) the selection.
  - Clicking outside the box or pressing <kbd>Enter</kbd> commits the transformation; pressing <kbd>Esc</kbd> cancels and reverts pixels.
  - Viewport navigation (Middle Mouse or Space + Drag) remains fully functional and never commits prematurely.
- **Precision Modifiers**:
  - <kbd>Shift</kbd> + Corner drag: Preserves aspect ratio ($1:1$).
  - <kbd>Ctrl</kbd> + Rotation drag: Snaps rotation angle to 15° increments ($0^\circ, 15^\circ, 30^\circ, 45^\circ\dots$).
- **Pixel-Art Resampling Algorithms (`PixelTransform`)**:
  - **Nearest-Neighbor (Fast)**: Zero blur, 100% palette preservation, instant computation for interactive drag preview.
  - **RotSprite (High-Fidelity)**: Scale2x / EPX upscaling pass followed by rotated sampling and majority voting downscale, preventing edge gaps, staircase distortion, and isolated single-pixel holes during rotation.
  - **Combined Transform Execution**: Two-pass pipeline: when rotation is non-zero and dimensions $\le 256\times 256$, RotSprite rotates 1:1, followed by Nearest-Neighbor scaling. For dimensions $> 256\times 256$, automatically falls back to Nearest-Neighbor to guarantee zero UI lag.
- **Transactional History & Lifecycle (Undo/Redo)**:
  - Pixels are lifted into `TransformSession` at activation (clearing original area on `ActiveLayer`).
  - Committing stamps transformed pixels and emits a single atomic `ApplyTransformCommand` recording sparse `PixelDelta` changes.
  - Switching to another tool or selecting another layer auto-commits the transform.
  - Canceling with <kbd>Esc</kbd> restores original pixels without emitting an undo command.
- **Performance & Zero-Alloc**:
  - Interactive transform drag maintains 60+ FPS via SkiaSharp matrix rendering (`SKCanvas.SetMatrix`).
  - Zero heap allocations in pointer drag loops.

### 2.2 Non-Goals
- Freeform mesh warping / Puppet warp (deferred to future advanced updates).
- Perspective / 3D skew deformation (only 2D affine scale, rotation, and translation).
- Changing canvas dimensions during transform (pixels outside canvas boundaries are clipped).

### 2.3 Anti-Goals (Quality Failures to Prevent)
- Introducing bilinear or bicubic filtering: PixelForge must never blur pixel art (`SKFilterQuality.None` strictly).
- Allocations (`new`, `LINQ`, lambdas) during mouse drag.
- Overwriting or corrupting layer pixels if the user switches tools or presses <kbd>Esc</kbd>.
- Freezing UI thread during RotSprite computation.

---

## 3. Approaches & Trade-Offs

### 3.1 Evaluated Approaches

| Approach | Description | Pros | Cons / Verdict |
| :--- | :--- | :--- | :--- |
| **Approach 1: Dedicated `TransformTool` (<kbd>Ctrl+T</kbd>)** *(Chosen)* | Independent tool implementing `ITool`. Manages its own modal state (Commit/Cancel), handles overlay, and math algorithms. | Strict Single Responsibility Principle; clean isolation from `SelectionTool`; no accidental handle clicks during normal selection; familiar Photoshop/Krita UX. | Requires a new tool button and mode transition. |
| **Approach 2: Embedded handles inside `SelectionTool`** | Every rectangular selection immediately gains 8 handles and rotation ring. | Less tool switching for simple moves. | Frequent accidental clicks on small 16×16 sprites; bloated state machine in `SelectionTool`; high regression risk for existing selection tests. |
| **Approach 3: Modal Dialog with Numeric Inputs** | Edit -> Free Transform window with numeric degree/scale sliders. | Simple to code. | Terrible UX for pixel art; no direct manipulation; breaks artist flow. Rejected. |

**Decision**: Approach 1 is selected. It provides the cleanest architecture, protects 712 existing unit tests, and delivers the standard workflow expected by digital artists.

---

## 4. Architectural Domain Model (`PixelForge.Core`)

### 4.1 Transform State Structure

```csharp
namespace PixelForge.Core.Transforms;

public enum TransformHandle
{
    None,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,
    Rotate,
    Move
}

public enum ResampleAlgorithm
{
    NearestNeighbor,
    RotSprite
}

/// <summary>
/// Encapsulates the active affine transformation parameters and lifted pixel buffer.
/// </summary>
public sealed class TransformSession
{
    public SKRectI OriginalBounds { get; }
    public PixelColor[] OriginalPixels { get; }
    public SKPoint Pivot { get; set; }
    public float ScaleX { get; set; } = 1.0f;
    public float ScaleY { get; set; } = 1.0f;
    public float RotationDegrees { get; set; } = 0.0f;
    public float TranslationX { get; set; } = 0.0f;
    public float TranslationY { get; set; } = 0.0f;
    public ResampleAlgorithm Algorithm { get; set; } = ResampleAlgorithm.RotSprite;

    public TransformSession(SKRectI bounds, PixelColor[] pixels)
    {
        OriginalBounds = bounds;
        OriginalPixels = (PixelColor[])pixels.Clone();
        Pivot = new SKPoint(bounds.Left + bounds.Width / 2.0f, bounds.Top + bounds.Height / 2.0f);
    }

    /// <summary>
    /// Computes the forward transformation matrix from original coordinates to current transformed coordinates.
    /// </summary>
    public SKMatrix ComputeMatrix()
    {
        var matrix = SKMatrix.CreateTranslation(TranslationX, TranslationY);
        matrix = matrix.PostConcat(SKMatrix.CreateTranslation(-Pivot.X, -Pivot.Y));
        matrix = matrix.PostConcat(SKMatrix.CreateScale(ScaleX, ScaleY));
        matrix = matrix.PostConcat(SKMatrix.CreateRotationDegrees(RotationDegrees));
        matrix = matrix.PostConcat(SKMatrix.CreateTranslation(Pivot.X, Pivot.Y));
        return matrix;
    }
}
```

### 4.2 Pixel Resampling Engine (`PixelTransform.cs`)

Located in `PixelForge.Core/Transforms/PixelTransform.cs`:
1. **Inverse Mapping (Backward Sampling)**:
   For any affine transformation (Scale + Rotation + Translation), forward mapping creates gaps/holes. The resampling engine computes the inverted matrix $M^{-1}$:
   $$\begin{pmatrix} x_{src} \\ y_{src} \end{pmatrix} = M^{-1} \begin{pmatrix} x_{dst} \\ y_{dst} \end{pmatrix}$$
2. **Nearest-Neighbor Resampler**:
   Evaluates transformed bounding box corners to find destination rectangle `[minX, minY, maxX, maxY]`. Loops over destination grid, samples nearest pixel from source buffer with bounds checking.
3. **RotSprite Resampler**:
   - For pure integer scaling or 0/90/180/270 degree rotations, falls back to exact coordinate transformations.
   - For arbitrary angles:
     1. Upscales source pixels by 8× using EPX/Scale2x cascade (creates directional diagonal pixel-art connectivity without blur).
     2. Samples backward from destination into the 8× buffer using inverted rotation matrix.
     3. Downsamples with majority color voting over the 8×8 block, preserving original palette colors without intermediate color blending.
   - **Combined Scaling & Safety**: If $\text{ScaleX} \neq 1.0$ or $\text{ScaleY} \neq 1.0$, RotSprite executes the 1:1 rotation pass first, followed by Nearest-Neighbor scale pass. For bounds exceeding $256 \times 256$, automatically switches to Nearest-Neighbor to prevent any UI freeze.

### 4.3 Lifecycle & Undo/Redo Integration (`ApplyTransformCommand.cs`)

Located in `PixelForge.Core/History/ApplyTransformCommand.cs`:
- **Liftoff**: At tool activation, source pixels are read from `ActiveLayer` into `TransformSession.OriginalPixels`, and the source region in `ActiveLayer` is cleared to transparent.
- **Commit**: Resampling generates final `(int X, int Y, PixelColor Color)` coordinates. An atomic `ApplyTransformCommand` is created comparing the original state before liftoff with the newly stamped state, recording sparse `PixelDelta[]`.
- **Cancel**: Restores `TransformSession.OriginalPixels` back into `ActiveLayer` at `OriginalBounds`, zeroing out session with no history mutation.
- **Tool Switch**: If user switches tools or layers while session is active, auto-commit is executed.

---

## 5. UI & Viewport Overlay Integration (`PixelForge.App`)

### 5.1 Handles & Interaction Geometry
- Handle size: $8 \times 8$ pixels in screen coordinates (constant size across zoom levels).
- Rotation handle: Circle of radius 5px on a 20px stem extending upwards from Top midpoint handle.
- Cursor feedback:
  - TopLeft / BottomRight: Diagonal resize cursor.
  - TopRight / BottomLeft: Diagonal resize cursor.
  - Top / Bottom: Vertical resize cursor.
  - Left / Right: Horizontal resize cursor.
  - Rotate: Hand / Cross cursor.
  - Inside: Move cursor.

### 5.2 SkiaSharp Overlay Render Pass
In `PixelCanvasControl.RenderOverlay()`:
- When `TransformTool` has an active `TransformSession`:
  - Renders the transformed pixel preview via `canvas.Save()`, `canvas.Concat(session.ComputeMatrix())`, `canvas.DrawBitmap(...)`, `canvas.Restore()`.
  - Renders bounding box outline (dash pattern, cyan / white).
  - Renders 8 filled rectangular handles with dark borders.
  - Renders rotation stem line and rotation circle handle.
  - Renders pivot crosshair.

### 5.3 Keyboard & Toolbar Shortcuts
- <kbd>Ctrl+T</kbd> / <kbd>T</kbd>: Activate `TransformTool`.
- <kbd>Enter</kbd>: Commit transform and return to previously active tool.
- <kbd>Esc</kbd>: Cancel transform without applying changes and return to previous tool.
- Toolbar: Added to primary tools list with `IconTransform` vector icon.

---

## 6. Definition of Done (Phase 1 Acceptance)
- [ ] `TransformTool` fully functional with <kbd>Ctrl+T</kbd> / <kbd>T</kbd> shortcut.
- [ ] 8 scale handles + 1 rotation handle with interactive mouse dragging.
- [ ] Shift preserves aspect ratio; Ctrl snaps rotation to 15°.
- [ ] <kbd>Enter</kbd> commits with sparse Undo/Redo; <kbd>Esc</kbd> cancels cleanly.
- [ ] Resampling maintains crystal-clear pixel art without blur (`SKFilterQuality.None`).
- [ ] 0 GC-alloc in mouse drag loop; 60+ FPS maintained on canvas.
- [ ] All new algorithms covered by AAA unit tests in `PixelForge.Core.Tests`.
- [ ] All 712+ existing tests remain 100% green; `dotnet build` with 0 warnings.
