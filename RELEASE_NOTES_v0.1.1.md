# Release Notes — PixelForge Windows v0.1.1 🎨⚡🔄✨

We are proud to announce **PixelForge Windows v0.1.1** — bringing the best creative features inspired by **Pixel Studio** to our high-performance desktop pixel art editor!

---

## 🌟 What's New in v0.1.1

### 1. 🎨 Dither & Pattern Brushes (`DitherBrushTool` & Patterns)
- **Dedicated Pattern Brush (`DitherBrushTool`)**:
  - Quickly select with hotkey `D` or from the left toolbar.
  - Draws textured strokes alternating between Primary and Secondary colors using mathematical dither matrices.
- **Iconic Pixel Art Dither Patterns (`DitherPattern`)**:
  - `Bayer 2×2`, `Bayer 4×4`, `Bayer 8×8` (classic ordered dithering matrices).
  - `Checkerboard 50%`, `Dots 25%`, `Dots 75%`.
  - `Horizontal Scanlines`, `Vertical Scanlines`, `Diagonal 45° Hatching`.
- **Integrated Pattern Selector**:
  - Accessible directly in the top Tool Context Bar when Pencil, Dither Brush, Fill, Gradient, or Shapes are active.

### 2. 🌈 Pixel Gradients (`GradientTool`)
- **Linear & Radial Pixel Gradients**:
  - Dedicated tool with hotkey `T` and toolbar icon.
  - Drag from start point to end point with live hardware-accelerated vector preview overlay on the Skia canvas.
- **Ordered Dithering vs. Smooth Blending**:
  - Supports classic retro 1-bit Bayer dithering between palette colors or smooth multi-step pixel interpolation.
  - Automatically respects active rectangular selections (`Selection.Bounds`).

### 3. 🪣 Flood Fill with Tolerance & Global Color Replace
- **Threshold / Tolerance (`0%` to `100%`)**:
  - Color difference matching in RGBA Euclidean space (`PixelColor.Distance`).
  - Allows filling anti-aliased or noisy regions with designer-tunable sensitivity via a numeric control in the Tool Context Bar.
- **Contiguous Toggle (Смежные)**:
  - **Contiguous On**: Classic BFS flood fill bounded by edges and tolerance.
  - **Contiguous Off**: Global color replace across the active layer (or within selection).
- Supports filling with active dither patterns.

### 4. 🔄 Layer & Canvas Transformations (Flip & Rotate)
- **Layer & Selection Transformations**:
  - **Flip Horizontal / Vertical**: Automatically clips to active selection bounds or flips the whole layer.
  - **Rotate 90° Clockwise, 90° Counter-Clockwise, and 180°**:
    - Square layers: exact in-place rotation.
    - Rectangular layers: centered rotation.
    - Active selection: rotates pixel buffer within the bounding box.
- **Canvas-Wide Transformations**:
  - **Flip Canvas (H/V)**: Flips all layers simultaneously.
  - **Rotate Canvas (90° CW, 90° CCW, 180°)**:
    - Automatically swaps canvas dimensions ($W \times H \leftrightarrow H \times W$) for non-square aspect ratios.
    - Accurately transforms all layers with full Undo/Redo support.

---

## 🧪 Quality & Verification Metrics

- **Unit Tests**: 112/112 tests passing (100% green).
  - 13 new comprehensive AAA unit tests in `PixelStudioFeaturesTests.cs`.
- **Zero Compiler Warnings**: `dotnet build PixelForge.sln -c Release /warnaserror` (0 errors, 0 warnings).
- **Zero-Allocation**: No GC pressure in pointer move or rendering cycles.

---

## 📦 Release Artifacts

- **Windows Installer**: `publish/win-x64/PixelForge_0.1.1_x64-setup.exe` (~35 MB)
- **OTA Manifest**: `publish/win-x64/latest.json` & `latest.json`
