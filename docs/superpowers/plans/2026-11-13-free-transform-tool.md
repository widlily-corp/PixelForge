# Free Transform Tool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a dedicated Free Transform Tool (`TransformTool`) in PixelForge providing interactive bounding-box scaling, rotation, RotSprite/Nearest-Neighbor resampling, and sparse Undo/Redo deltas.

**Architecture:** Clean Architecture + SkiaSharp GPU preview overlay. The domain math lives in `PixelForge.Core/Transforms/PixelTransform.cs` and `TransformSession.cs`. History is recorded through `ApplyTransformCommand.cs` with sparse `PixelDelta`. Interactive handles and affine matrices are rendered via `PixelCanvasControl.RenderOverlay`.

**Tech Stack:** C# 12, .NET 8.0, SkiaSharp 2.88.8, Avalonia UI 11.1.3, CommunityToolkit.Mvvm 8.3.2, xUnit, FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-11-13-free-transform-tool-design.md`

## Global Constraints
- Zero heap allocations (`new`, `LINQ`, boxing) during mouse movement and drag loops.
- Resampling strictly avoids bilinear or bicubic filtering (`SKFilterQuality.None`).
- 100% backward compatibility for `.pxproj` projects (no schema breakage).
- All 712+ existing unit tests must remain green.
- `dotnet build PixelForge.sln -c Release` must pass with 0 errors and 0 warnings.

## Review Focus
1. Non-uniform scaling combined with rotation must not crash or corrupt bounds.
2. Pressing <kbd>Esc</kbd> must cleanly restore original layer pixels without leaving dirty state or phantom undo entries.
3. Rotating 360° or 0° must perfectly preserve original pixel colors without loss.
4. Dragging handles on small 8×8 or 16×16 sprites must remain accessible without handle-clipping deadlocks.
5. Canvas panning (<kbd>Space</kbd> + Drag or Middle Mouse) must never accidentally commit an active transform session.

---

### Task 1: Core Transform Data Structures & Nearest-Neighbor Resampler
**Files:**
- Create: `src/PixelForge.Core/Transforms/TransformSession.cs`
- Create: `src/PixelForge.Core/Transforms/PixelTransform.cs`
- Create: `tests/PixelForge.Core.Tests/Transforms/PixelTransformTests.cs`

- [ ] Write unit tests for `TransformSession` matrix computation (Translation, Scale, Rotation about Pivot).
- [ ] Write unit tests for `PixelTransform.TransformNearestNeighbor` verifying 1:1 identity, 90° rotation, and $2\times$ integer scale.
- [ ] Implement `TransformHandle`, `ResampleAlgorithm`, and `TransformSession` in `PixelForge.Core/Transforms/TransformSession.cs`.
- [ ] Implement `PixelTransform.TransformNearestNeighbor` with backward coordinate sampling and tight bounding calculation.
- [ ] Run `dotnet test tests/PixelForge.Core.Tests/PixelForge.Core.Tests.csproj` and verify all tests pass.

---

### Task 2: RotSprite & Scale2x Resampling Engine
**Files:**
- Modify: `src/PixelForge.Core/Transforms/PixelTransform.cs`
- Modify: `tests/PixelForge.Core.Tests/Transforms/PixelTransformTests.cs`

- [ ] Write unit tests in `PixelTransformTests.cs` verifying Scale2x upscaling on a 3×3 diagonal line pattern.
- [ ] Write unit tests verifying RotSprite preserves single-pixel line continuity on 45° rotation.
- [ ] Write unit tests verifying fallback to Nearest-Neighbor for dimensions $> 256\times 256$.
- [ ] Implement EPX / Scale2x upscaler in `PixelTransform.cs`.
- [ ] Implement RotSprite sampling with inverted matrix rotation and $8\times 8$ majority color voting.
- [ ] Run `dotnet test` and ensure all tests pass.

---

### Task 3: Transactional Undo/Redo (`ApplyTransformCommand`)
**Files:**
- Create: `src/PixelForge.Core/History/ApplyTransformCommand.cs`
- Create: `tests/PixelForge.Core.Tests/History/ApplyTransformCommandTests.cs`

- [ ] Write unit tests verifying `ApplyTransformCommand` records sparse `PixelDelta` for lifted vs transformed pixels.
- [ ] Write unit tests verifying `Undo()` perfectly restores original layer pixels.
- [ ] Write unit tests verifying `Redo()` reapplies transformed pixels.
- [ ] Implement `ApplyTransformCommand : IUndoRedoCommand`.
- [ ] Run `dotnet test` and confirm all tests pass.

---

### Task 4: `TransformTool` Implementation (`ITool`)
**Files:**
- Create: `src/PixelForge.Core/Tools/TransformTool.cs`
- Create: `tests/PixelForge.Core.Tests/Tools/TransformToolTests.cs`

- [ ] Write unit tests for `TransformTool` hit-testing: TopLeft, TopRight, Rotate, Inside (Move), Outside.
- [ ] Write unit tests verifying <kbd>Shift</kbd> locks aspect ratio and <kbd>Ctrl</kbd> snaps rotation to 15°.
- [ ] Write unit tests verifying `Commit()` stamps pixels and emits history, while `Cancel()` restores pixels.
- [ ] Implement `TransformTool : ITool` with full state machine (`ActiveSession`, dragging, scaling, rotating, translating).
- [ ] Run `dotnet test` and verify all tests pass.

---

### Task 5: Viewport Rendering & Direct Manipulation Overlay
**Files:**
- Modify: `src/PixelForge.App/Controls/PixelCanvasControl.cs`
- Modify: `src/PixelForge.App/ViewModels/ToolViewModel.cs`
- Modify: `src/PixelForge.App/ViewModels/MainViewModel.cs`
- Modify: `src/PixelForge.App/Views/MainWindow.axaml`
- Modify: `src/PixelForge.App/Assets/Icons.axaml`

- [ ] Add `IconTransform` vector icon path to `Icons.axaml`.
- [ ] Register `TransformTool` in `ToolViewModel.cs` (Icon, Tooltip, Shortcut <kbd>Ctrl+T</kbd> / <kbd>T</kbd>).
- [ ] Wire <kbd>Ctrl+T</kbd>, <kbd>Enter</kbd> (Commit), and <kbd>Esc</kbd> (Cancel) keyboard handlers in `MainWindow.axaml` / `MainViewModel.cs`.
- [ ] Implement overlay rendering in `PixelCanvasControl.cs`: transformed preview bitmap, 8 resize handles, rotation handle stem, pivot crosshair.
- [ ] Implement cursor feedback based on hovered `TransformHandle`.

---

### Task 6: End-to-End Verification & Release Build
**Files:**
- Run full test suite across the solution.
- Verify zero compiler warnings in Release configuration.

- [ ] Run `dotnet test PixelForge.sln -c Release`.
- [ ] Run `dotnet build PixelForge.sln -c Release` with `/p:TreatWarningsAsErrors=true` to guarantee 0 warnings.
- [ ] Verify existing timeline animation and drawing tools show no regressions.
