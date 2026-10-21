# PixelForge v0.1.5 Release Notes

## 🎨 Professional Pixel Art Studio — Global Update

We are proud to present **PixelForge v0.1.5**, a major feature and polish update focusing on user experience, comprehensive localization, deep Windows integration, and new artist workflow tools!

---

### ✨ Key New Features in v0.1.5

#### 1. 🌐 100% Full Dual-Language Localization (RU 🇷🇺 / EN 🇬🇧)
- Every single UI element — menus, toolbars, buttons, tooltips, HUD labels, status notifications, and all modal dialogs — is fully localized in Russian and English.
- Instant, seamless runtime language switching with zero reload required. Click the **🌐 RU / EN** switcher button on the status bar or choose **Правка ➔ Язык (Edit ➔ Language)** from the top menu.
- Complete localization across all dialogs:
  - **New Canvas Dialog (Новый холст)**
  - **Resize Canvas Dialog (Размер холста)**
  - **Color Ramp Generator (Генератор цветовой рампы)**
  - **Keyboard Shortcuts Reference (Горячие клавиши)**
  - **Color Picker & Palette Inspector (Палитра и цвет)**
  - **About Window (О программе)**

#### 2. 🪟 Windows Shell & File Association Integration (`.pxproj`)
- PixelForge project files (`.pxproj`) are now fully integrated with Windows Explorer.
- Double-clicking any `.pxproj` file automatically launches PixelForge and opens the project.
- PixelForge is registered in Windows Shell `OpenWithProgids` and `Applications`, making it always available in Windows "Open with..." dialog.
- Command-line argument support: launch with `PixelForge.App.exe "path/to/project.pxproj"`.
- One-click manual association registration available in **Файл ➔ Ассоциировать файлы .pxproj** without requiring administrator privileges.

#### 3. 📂 Drag-and-Drop Support
- **Drag & drop projects**: Drag any `.pxproj` file from Windows Explorer straight onto PixelForge to immediately open it.
- **Drag & drop images**: Drag any image (`.png`, `.jpg`, `.jpeg`, `.bmp`, `.webp`) onto the window to instantly load it as a semi-transparent reference overlay on your canvas.

#### 4. 🖼️ Semi-Transparent Reference Image Overlay
- An indispensable tool for pixel artists! Load any reference artwork, sketch, or photo directly onto the canvas.
- Hardware-accelerated SkiaSharp rendering pass with zero heap allocations during drawing and panning.
- Dedicated inspector card in the right sidebar with:
  - **Opacity slider** (0% to 100%)
  - **Visibility toggle** (Show / Hide reference)
  - Quick **Load...** and **Clear** buttons

#### 5. 🕒 Recent Projects Menu
- Automatically tracks and persists your recently opened `.pxproj` projects.
- Transactionally saved in `%APPDATA%/PixelForge/settings.json`.
- Access your recent work instantly from **Файл ➔ Недавние проекты (File ➔ Recent Projects)**.
- Includes a **Clear Recent Projects** action to reset the list.

#### 6. ⚔️ Handcrafted Pixel-Art Hero Splash Screen
- Replaced the minimal loading screen with a vibrant, handcrafted 540x180 pixel-art hero artwork: a cyber-fantasy glowing forge and cosmic anvil with amethyst nebulas and neon cyan sparks.
- Displays version badge (`v0.1.5`), localized system boot steps (RAM Working Set pre-faulting, assembly locking, SkiaSharp pipeline warm-up), and animated progress.

---

### 🚀 Performance & Stability Fixes
- **Zero GC Allocations**: Maintained strict zero heap allocation discipline in pointer motion, drawing loops, and Skia rendering operations.
- **Cross-Platform Safety**: Added Windows platform guards (`OperatingSystem.IsWindows()`) to eliminate compilation and runtime warnings.
- **Transactional Persistence**: User preferences (language, theme, recent projects) are saved atomically to `.tmp` before renaming to prevent corrupt configurations on abrupt shutdown.
- **100% Green Test Suite**: All 264 unit and integration tests passing (`PixelForge.Core.Tests` and `PixelForge.App.Tests`).

---

### 📦 Installation
Download and run `PixelForge_0.1.5_x64-setup.exe`. The ultra-compact LZMA2 Inno Setup installer will install PixelForge, register file associations, and create start menu and desktop shortcuts.
