# Release Notes — PixelForge Windows v0.1.3 🎨✨🚀💎

We are extremely proud to announce **PixelForge Windows v0.1.3** — our most stunning and intuitive update yet! This release delivers a complete overhaul of the desktop user interface, combining modern design aesthetics with exceptional usability, clear visual hierarchy, and full keyboard workflow support.

---

## 🌟 What's New in v0.1.3

### 1. 🎨 Premium Glassmorphism Design System & Semantic Themes
- **Deep Velvet Palette**:
  - Re-engineered dark theme with soothing background tones (`#0E1017` / `#141622`), elevated cards (`#1A1D2E`), and soft borders (`#24283E`).
- **Glow & Accent States**:
  - Active tool buttons and toggles feature vibrant neon cyan accents (`#00D2FF`) with soft ambient glow backdrops (`AccentGlowBrush`, `#3300D2FF`).
- **New Vector StreamGeometry Icons**:
  - Sharp 24×24 icons: `IconPalette`, `IconSliders`, `IconKeyboard`, `IconHelp`, `IconSparkles`, `IconCheck`, `IconInformation`.
- **Harmonized Light Theme**:
  - Full parity with light theme tokens for consistent contrast and crispness.

### 2. 🛠️ Intuitively Grouped Toolbar (Left Sidebar)
- **4 Distinct Categorized Sections with Micro-Headers**:
  1. ✏️ **DRAWING (РИСОВАНИЕ)**: Pencil (`B`), Eraser (`E`), Dither Brush (`D`), 8×8 Pattern Brush (`P`).
  2. 🪣 **COLOR & FILL (ЗАЛИВКА И ЦВЕТ)**: Flood Fill (`G`), Magic Wand (`W`), Gradient (`T`), Eyedropper (`I`).
  3. 📐 **SHAPES (ФИГУРЫ)**: Line (`L`), Rectangle (`R / U`), Ellipse (`C`).
  4. ✂️ **SELECTION (ВЫБОР)**: Rectangular Selection (`S / M`).
- **Bottom Brush Stepper Card**:
  - Dedicated bottom widget displaying current brush size in pixels with direct `−` and `+` increment controls.

### 3. 🎛️ Modular Tool Context Bar (Chip & Capsule UI)
- All tool options are organized into rounded card chips:
  - **Tool Identity Chip**: Vector icon and bold colored name of the current tool.
  - **Quick Size Presets**: Pill buttons for instant `1`, `2`, `3`, `4`, `8` px switching.
  - **Pixel-Perfect & Stabilizer Chip**: Glowing toggle switch (`Shift+P`) and smooth slider (0..10) with value readout.
  - **Pattern / Texture Chip**: Rapid drop-down selector for 8×8 binary patterns or Bayer dither matrices.
  - **Fill & Wand Settings**: Numeric Tolerance control with percentage format and Contiguous toggle.
  - **Gradient & Shape Chips**: Linear/Radial modes and Filled Shape toggle.

### 4. 💡 Floating Canvas HUD (Real-Time Control Hints)
- Unobtrusive floating HUD capsule anchored at the bottom-center of the canvas:
  > `ЛКМ: Рисовать • ПКМ: Стереть/Вторичный • Пробел+Drag: Панорама • Ctrl+Колесо: Зум`
- Non-interactive (`IsHitTestVisible="False"`), completely transparent to mouse strokes, providing immediate guidance to both beginners and power users.

### 5. 🎨 Redesigned Color Picker & Dual Palette Cards
- **Primary & Secondary Color Cards**:
  - Clear `[ЛКМ] Основной` and `[ПКМ] Вторичный` indicator badges with large preview swatches.
  - Quick-swap button (`⇄`, `X`) with smooth hover feedback.
- **Dual Mode Switching**:
  - **Palette Grid**: Rounded color swatches (`CornerRadius="4"`) with hover magnification, glowing selection border, and Hex code input with `+` Add button.
  - **RGB Sliders**: Precision channel sliders for Red, Green, Blue, and Alpha with color-coded badges and numeric readouts.

### 6. ⌨️ Interactive Hotkeys Cheat Sheet Dialog (<kbd>F1</kbd>)
- Brand-new modal dialog accessed via <kbd>F1</kbd> or **Help ➔ Hotkeys...**:
  - Keyboard shortcut cheat sheet categorized into Drawing, Tools, Navigation, and Project Management.
  - Styled with physical-keyboard-key badges and clear explanations.

---

## 🧪 Quality & Engineering Verification

- **100% Test Suite**: 225/225 tests passing (182 `PixelForge.Core` + 43 `PixelForge.App`).
- **Clean Compiler Build**: `dotnet build PixelForge.sln -c Release /warnaserror` (0 warnings, 0 errors).
- **Zero Allocations**: Zero GC pressure in mouse movement and SkiaSharp canvas rendering loops.

---

## 📦 Distribution Packages

- **Installer**: `PixelForge_0.1.3_x64-setup.exe` (~35 MB, Inno Setup LZMA2/Ultra)
- **OTA Manifest**: `latest.json`
