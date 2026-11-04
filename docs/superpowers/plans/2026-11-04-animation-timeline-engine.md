# Frame Animation & Timeline Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a high-performance, 60+ FPS frame animation and timeline engine with Cel-Layer matrix, zero-alloc SkiaSharp onion skinning, dual playback (canvas + mini-preview), Aseprite-compatible spritesheet/GIF export, and full `.pxproj v2` backward compatibility in PixelForge.

**Architecture:** Facade Cel-Layer projection model via `PixelCanvas`. Layers act as continuous horizontal tracks, frames act as discrete time steps, and active cels are swapped into layer pixel buffers ($O(1)$ zero allocations) when navigating frames. Drawing tools continue operating via `ActiveLayer` with zero regression.

**Tech Stack:** .NET 8.0, C# 12, Avalonia UI 11.1.3, SkiaSharp 2.88.8, CommunityToolkit.Mvvm 8.3.2, xUnit, FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-11-04-animation-timeline-engine-design.md`

## Global Constraints
- Target Framework: .NET 8.0 (C# 12).
- Zero-Alloc in render and pointer loops: no `new`, no LINQ, no string concatenation in `Render()` or mouse handlers.
- Pure Core Domain: `PixelForge.Core` has 0 references to Avalonia UI.
- All layer operations (Add, Remove, Merge Down, Flip, Resize) operate globally across all frames in the timeline.
- Backward compatibility: 100% loss-free loading of `.pxproj v1` projects into synthesized single-frame v2 format.
- Build & Tests: `dotnet build -c Release` with 0 warnings, 0 errors, and all tests passing.

## Review Focus
1. Frame switching during rapid playback must not cause memory leaks or garbage collection spikes.
2. Undo/Redo across frames must auto-navigate to the command's target frame and correctly restore sparse deltas.
3. Onion skinning rendering with 5+ previous/next frames must maintain 60+ FPS on SkiaSharp canvas.
4. Exported Aseprite-compatible JSON spritesheets must strictly conform to the standard schema with correct frame coordinates and durations.
5. Opening legacy `.pxproj` v1 files must automatically upgrade to v2 with duration 100ms and 0 data loss.

---

### Task 1: Core Animation Domain Models & Timeline Management
**Files:**
- Create: `src/PixelForge.Core/Models/AnimationCel.cs`
- Create: `src/PixelForge.Core/Models/AnimationFrame.cs`
- Create: `src/PixelForge.Core/Models/AnimationTag.cs`
- Create: `src/PixelForge.Core/Models/OnionSkinSettings.cs`
- Modify: `src/PixelForge.Core/Models/PixelCanvas.cs`
- Test: `tests/PixelForge.Core.Tests/AnimationDomainTests.cs`

**Steps:**
- [ ] Create `AnimationCel.cs` holding `Guid LayerId`, `PixelColor[] Pixels`, and `bool IsEmpty`.
- [ ] Create `AnimationFrame.cs` with `Guid Id`, `int DurationMs`, and `Dictionary<Guid, AnimationCel> Cels`.
- [ ] Create `AnimationTag.cs` with `Name`, `FromFrame`, `ToFrame`, `Color`, `LoopMode`, `RepeatCount`.
- [ ] Create `OnionSkinSettings.cs` with `IsEnabled`, `PrevFramesCount`, `NextFramesCount`, `MaxOpacity`, `PrevFrameTint`, `NextFrameTint`.
- [ ] Extend `PixelCanvas.cs`:
  - Add `ObservableCollection<AnimationFrame> Frames`.
  - Add `ObservableCollection<AnimationTag> Tags`.
  - Add `OnionSkinSettings OnionSkin`.
  - Add `int CurrentFrameIndex` with bounds checking.
  - Implement `SwitchToFrame(int index)` with zero-allocation cel buffer swapping into `Layer.Pixels`.
  - Implement `AddFrame(int? insertIndex = null, int durationMs = 100)`.
  - Implement `DuplicateFrame(int sourceIndex)`.
  - Implement `RemoveFrame(int index)`.
- [ ] Write unit tests in `AnimationDomainTests.cs` testing frame switching, cel buffer swapping, layer synchronization, and boundary conditions.
- [ ] Run `dotnet test tests/PixelForge.Core.Tests` and confirm passing.

---

### Task 2: Timeline Undo/Redo Commands & Global Layer Operations
**Files:**
- Modify: `src/PixelForge.Core/History/DrawPixelsCommand.cs`
- Create: `src/PixelForge.Core/History/TimelineCommands.cs`
- Modify: `src/PixelForge.Core/Models/PixelCanvas.cs`
- Test: `tests/PixelForge.Core.Tests/TimelineHistoryTests.cs`

**Steps:**
- [ ] Update `DrawPixelsCommand.cs` to store `int FrameIndex` and `PixelCanvas Canvas`. In `Execute()` and `Undo()`, ensure `Canvas.SwitchToFrame(FrameIndex)` is invoked if `Canvas.CurrentFrameIndex != FrameIndex`.
- [ ] Create `TimelineCommands.cs` containing:
  - `AddFrameCommand`
  - `RemoveFrameCommand`
  - `DuplicateFrameCommand`
  - `ReorderFramesCommand`
  - `ChangeFrameDurationCommand`
  - `AddTagCommand`, `RemoveTagCommand`
- [ ] Update global layer operations in `PixelCanvas.cs`:
  - `AddLayer()` creates an empty cel in all frames.
  - `RemoveLayer()` purges cels from all frames.
  - `MergeLayerDown()` blends cels across all frames.
  - `SetDimensions()` resizes all cels across all frames.
- [ ] Write comprehensive unit tests in `TimelineHistoryTests.cs` validating undo/redo for stroke drawing on different frames and timeline mutations.
- [ ] Run `dotnet test tests/PixelForge.Core.Tests` and confirm passing.

---

### Task 3: Project File (.pxproj v2) Serialization & Compatibility
**Files:**
- Modify: `src/PixelForge.Core/Models/Project.cs`
- Modify: `src/PixelForge.Core/Services/ProjectService.cs`
- Test: `tests/PixelForge.Core.Tests/ProjectSerializationV2Tests.cs`

**Steps:**
- [ ] In `Project.cs`:
  - Add `FrameData` and `CelData` serializable DTOs.
  - Add `TagData` DTO.
  - Add `List<FrameData> Frames` and `List<TagData> Tags`.
  - Add `FormatVersion = 2`.
  - Update `FromCanvas()` to serialize all frames and tags.
  - Update `ToCanvas()` to support both `FormatVersion == 1` (synthesizing Frame 0 from static layers) and `FormatVersion == 2`.
- [ ] Write unit tests in `ProjectSerializationV2Tests.cs`:
  - Test saving and loading multi-frame projects with tags.
  - Test loading legacy v1 JSON strings and asserting clean conversion to 1-frame canvas.
- [ ] Run `dotnet test` and confirm passing.

---

### Task 4: Export Engine: Animated GIF, Spritesheet & PNG Sequence
**Files:**
- Create: `src/PixelForge.Core/Services/GifEncoder.cs`
- Modify: `src/PixelForge.Core/Services/IExportService.cs`
- Modify: `src/PixelForge.Core/Services/ExportService.cs`
- Test: `tests/PixelForge.Core.Tests/AnimationExportTests.cs`

**Steps:**
- [ ] Implement zero-dependency lightweight `GifEncoder.cs` (color quantization + LZW encoding for multi-frame GIF).
- [ ] Extend `IExportService.cs` and `ExportService.cs`:
  - `ExportGifAsync(PixelCanvas canvas, string filePath, int scale = 1, ...)`
  - `ExportSpritesheetAsync(PixelCanvas canvas, string imagePath, string jsonPath, SpritesheetSettings settings, ...)`
  - `ExportPngSequenceAsync(PixelCanvas canvas, string outputDirectory, string prefix, int scale = 1, ...)`
- [ ] Implement Aseprite-compatible JSON export formatting in `ExportSpritesheetAsync`.
- [ ] Write tests in `AnimationExportTests.cs` verifying GIF generation, Spritesheet layout calculations, and JSON schema output.
- [ ] Run `dotnet test` and confirm passing.

---

### Task 5: SkiaSharp Graphics Pipeline & Zero-Alloc Onion Skinning
**Files:**
- Modify: `src/PixelForge.App/Controls/PixelCanvasControl.cs`
- Test: Manual rendering verification + Skia composite tests in `tests/PixelForge.Core.Tests/`

**Steps:**
- [ ] In `PixelCanvasControl.cs`:
  - Pre-allocate cached `SKPaint` and `SKColorFilter` fields for onion skinning.
  - In `Render()` / `ICustomDrawOperation`, add Pass 1 (Previous frames) and Pass 2 (Next frames) with tint blending and opacity attenuation.
  - Ensure zero allocations in the drawing loop (no `new SKPaint`, no closures).
  - Add pointer event guards: when `IsPlaying` is true, pointer press immediately stops playback and selects the clicked frame.

---

### Task 6: Playback Service & Mini-Preview Window
**Files:**
- Create: `src/PixelForge.App/Services/PlaybackService.cs`
- Create: `src/PixelForge.App/ViewModels/AnimationPreviewViewModel.cs`
- Create: `src/PixelForge.App/Views/AnimationPreviewWindow.axaml`
- Create: `src/PixelForge.App/Views/AnimationPreviewWindow.axaml.cs`

**Steps:**
- [ ] Create `PlaybackService.cs` using high-precision timer loop to tick frames respecting individual `DurationMs` and active tag loop bounds.
- [ ] Create `AnimationPreviewViewModel.cs` and `AnimationPreviewWindow.axaml` displaying a dedicated animated preview at selectable scales (1x, 2x, 3x, 4x).
- [ ] Ensure preview window renders concurrently without interfering with main canvas pointer drawing.

---

### Task 7: UI Timeline Control & Main Integration
**Files:**
- Create: `src/PixelForge.App/ViewModels/TimelineViewModel.cs`
- Create: `src/PixelForge.App/Views/TimelineControl.axaml`
- Create: `src/PixelForge.App/Views/TimelineControl.axaml.cs`
- Modify: `src/PixelForge.App/ViewModels/MainViewModel.cs`
- Modify: `src/PixelForge.App/Views/MainWindow.axaml`

**Steps:**
- [ ] Implement `TimelineViewModel.cs` wrapping `PixelCanvas.Frames`, `Tags`, playback commands, and onion skin toggles.
- [ ] Implement `TimelineControl.axaml` with transport toolbar (Play, Pause, Step, Loop, FPS, Onion, Add, Duplicate, Delete), tag ribbon, frame header, and layer/cel grid with drag-and-drop frame reordering.
- [ ] Integrate `TimelineControl` into `MainWindow.axaml` below canvas with a `GridSplitter` and collapsible toggle (`Tab` / `F7`).
- [ ] Wire shortcuts: `,` / `.` (step frames), `Space` (play/pause), `F6` (new frame), `F5` (duplicate), `O` (onion skin), `Tab` (toggle timeline).

---

### Task 8: Verification, Documentation & Release
**Files:**
- Modify: `README.md`
- Run: `dotnet build -c Release`
- Run: `dotnet test`
- Execute: `.\.agents\scripts\bump-version.ps1 0.2.0 -ReleaseNotes "Add frame animation and timeline engine" -Pack -Commit`
- Git push to origin.
