# Release Notes — PixelForge Windows v0.1.2 🎨⚡🔄✨

We are thrilled to announce **PixelForge Windows v0.1.2** — a massive update introducing a complete creative toolset inspired by **Pixel Studio**, automated silent in-app updates, and high-performance desktop graphics optimizations!

---

## 🌟 What's New in v0.1.2

### 1. 🔄 Automated Silent In-App Self-Updating («Прям само»)
- **True In-App Update Engine**:
  - PixelForge now automatically detects new versions from the repository manifest.
  - One-click update banner with real-time download progress bar directly in the application header.
  - Silent background installer execution (`/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS`).
  - Seamless process handoff and restart without opening web browsers or external download pages.

### 2. ✏️ Pixel-Perfect Drawing Engine (`Shift+P`)
- **Real-Time Jaggie Removal**:
  - Proprietary `PixelPerfectAlgorithm` continuously monitors freehand mouse and stylus strokes.
  - Automatically identifies and clips unsightly L-corner jaggies and staircase artifacts.
  - Produces crisp, beautiful single-pixel lines across both the Pencil and Eraser tools.
  - Accessible via the top context toolbar or hotkey `Shift+P`.

### 3. 🎨 8×8 Retro Pattern Brushes & Dithering Presets (`P`)
- **Zero-Allocation Bitmask Patterns**:
  - Powered by 64-bit `ulong` binary matrix masks with zero heap allocations during drawing cycles.
  - 11 built-in classic pixel art presets:
    - *Checkerboard (50% Dither)*
    - *Dense Dots (25%)* & *Sparse Dots (12.5%)*
    - *Bricks*
    - *Crosshatch*
    - *Diagonal Stripes*
    - *Fish Scales*
    - *Woven Texture*
    - *Procedural Noise*
    - *Horizontal & Vertical Scanlines*
- **Pattern Brush Tool (`PatternBrushTool`)**:
  - Full support for brush sizing, secondary colors, and horizontal/vertical symmetry axes.

### 4. 🎯 Stroke Stabilizer (0..10)
- **Exponential Smoothing Engine**:
  - Adjustable stabilizer slider in the tool parameters bar.
  - Dampens hand tremor and mouse jitter for smooth curves and precise linework.

### 5. 🪄 Magic Wand Selection Tool (`W`)
- **Intelligent Color Selection**:
  - **Contiguous Mode**: High-speed Breadth-First Search (BFS) flood selection bounded by neighboring pixels.
  - **Global Mode**: Instantly selects all matching color pixels across the entire active layer.
  - **Tolerance Threshold (0..255)**: Euclidean RGBA distance matching for picking color ranges.

### 6. 🔒 Layer Alpha Lock
- **Transparency Protection**:
  - Added a reactive lock toggle on every layer row in the Layers panel.
  - When locked, transparent pixels are completely shielded from brush strokes, flood fill, and erasure.
  - Perfect for shading and highlighting existing sprites without bleeding onto the background.

### 7. 🪄 Smart Tools (Menu: Edit -> Smart Tools)
- **Smart Outline (Автоматическая обводка)**:
  - Generates crisp pixel outlines (4-way cross or 8-way diagonal) with customizable color and thickness.
- **Smart Shadow (Автоматическая тень)**:
  - Casts customizable drop shadows with directional pixel offsets (`dx`, `dy`) and opacity.
- **Color Replace (Замена цвета)**:
  - Fast single-layer color replacement respecting customizable tolerance.
- Full sparse-delta Undo/Redo integration.

### 8. 🧩 Seamless Tiled Mode (`F10`)
- **Real-Time 3×3 Repeating Viewport**:
  - Live 3×3 wrap-around composite preview for seamless textures and game tilesets.
  - Subtle boundary grid indicators for the central tile.
  - Automatic coordinate wrapping `(x % w + w) % w` during drawing.

---

## 🧪 Quality & Engineering Verification

- **100% Unit Test Suite**: 218/218 tests passing (182 `PixelForge.Core` + 36 `PixelForge.App`).
- **Compiler Health**: Built with `/warnaserror` (0 warnings, 0 errors).
- **Zero Allocations**: Zero garbage collection pressure in pointer move and rendering loops.
- **Reliable Persistence**: Transactional `.tmp` -> `.pxproj` project saving with backward compatibility.

---

## 📦 Distribution Packages

- **Installer**: `PixelForge_0.1.2_x64-setup.exe` (~35 MB, Inno Setup LZMA2/Ultra)
- **OTA Manifest**: `latest.json`
