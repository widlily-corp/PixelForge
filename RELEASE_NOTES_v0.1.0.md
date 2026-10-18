# Release Notes — PixelForge Windows v0.1.0 🚀⚡🎨💎

We are excited to announce **PixelForge Windows v0.1.0** — our major performance, stability, and UX upgrade!

This release introduces full RAM working set residency, pre-JIT compiled hot paths, a paced startup splash screen with real-time subsystem progress, native Windows minidump crash diagnostics, interactive canvas resizing with 3×3 anchor positioning, and a compact LZMA2 ultra-compressed Windows installer (reducing download size from 73+ MB to 35 MB).

---

## 🌟 What's New in v0.1.0

### 1. 🖼️ Interactive Canvas Resizing & Layer Transformations
- **Canvas Size Dialog (`ResizeCanvasDialog`)**: Full control over canvas dimensions (8 to 1024 px) accessible via Menu (`Холст -> Размер холста...`), keyboard shortcut (`Ctrl+Alt+C` / `Ctrl+R`), and direct click on the dimensions badge in the bottom status bar.
- **3×3 Directional Anchor Positioning**: Repositions artwork to Top-Left, Top-Center, Top-Right, Middle-Left, Center, Middle-Right, Bottom-Left, Bottom-Center, or Bottom-Right when cropping or expanding.
- **Nearest-Neighbor Scaling Mode**: Proportional pixel scaling without blur or distortion.
- **Quick Presets & Multipliers**: Instant 16×16, 32×32, 48×48, 64×64, 128×128, 256×256, 2× scale, and 0.5× scale.
- **Layer Transformations**: Instant horizontal and vertical layer flipping (`FlipLayerCommand`) and active layer clearing, fully reversible in Undo/Redo history.

### 2. 🧠 Resident Memory Execution (Выгрузка в ОЗУ)
- **High-Priority Working Set Expansion**: Win32 `SetProcessWorkingSetSize` expands the process working set (256 MB..1024 MB) so Windows retains all application pages in physical RAM.
- **Zero-Paging Memory Pre-Faulting**: `MemoryPreloadService` iterates over all native and managed modules (`libSkiaSharp.dll`, Avalonia binaries, PixelForge Core), touching 4KB pages via `VirtualQuery` and `Volatile.Read`. All code and data are resident in physical RAM before user interaction begins.
- **VirtualLock Pinning**: Locks critical memory pages in physical RAM to prevent OS pagefile swapping.
- **Pre-JIT Compilation**: Automatically pre-compiles critical methods and constructors (`RuntimeHelpers.PrepareMethod`) across Core models, tools, commands, and ViewModels ahead of time, eliminating first-action JIT compilation stalls.

### 3. 🎨 High-Performance Startup Splash Screen (`SplashWindow`)
- **Paced Visual Progression (~5 Seconds)**: `AppBootstrapper` smoothly guides users through all 5 preloading phases over ~5.0 seconds so every milestone (RAM allocation, module page locking, hot-path JIT, Skia warm-up, and service building) is clearly visible.
- **Professional Studio Design**: Borderless widescreen dark studio card (`#12131C`) with subtle radial gradients, glowing cyan accents (`#00D2FF`), and branding typography.
- **Real-Time Subsystem Progress**: Dynamic progress bar (0%..100%) reporting genuine initialization milestones with detailed diagnostic messages.
- **Asynchronous Transition**: Background `AppBootstrapper` transitions smoothly to `MainWindow` once components are 100% prepared.

### 4. 🛡️ Native Windows MiniDump Crash Reporting & Logging
- **Automated Native MiniDumps**: Native Windows minidump generation via `DbgHelp.dll` (`MiniDumpWriteDump`) captured automatically upon unhandled UI or background exceptions.
- **Structured JSON Diagnostics**: Generates complete environment details, working set memory statistics, and nested stack traces in `%LOCALAPPDATA%/PixelForge/CrashReports/`.
- **Immediate Streaming Logging (`debug.log`)**: `AppLogger` records high-fidelity diagnostic timestamps and process milestones directly adjacent to the application executable with immediate flush.

### 5. 🔄 OTA Updater & GitHub Manifest Hotfix
- **Fixed Manifest Endpoint**: Corrected repository endpoint to `https://raw.githubusercontent.com/widlily-corp/PixelForge/main/latest.json`.
- **Friendly Localized Feedback**: Informative Russian status messages in the status bar and non-intrusive update banner.

### 6. 📦 Compact Windows Installer (Inno Setup LZMA2 Ultra)
- **Elimination of Fat Portable Executable**: Removed bloated 73+ MB portable binaries.
- **Ultra-Compressed Installer**: Built using Inno Setup 6 with `lzma2/ultra64` solid compression (`PixelForge_0.1.0_x64-setup.exe`), reducing download size to **35.04 MB** (>52% bandwidth savings).
- **Full Windows Integration**:
  - Registers `.pxproj` ("PixelForge Project") file extension with custom application icon.
  - Creates Start Menu program group and optional Desktop shortcut.
  - Clean uninstaller registered in Windows Settings ("Installed Apps" / Add-Remove Programs).

---

## 🧪 Verification & Quality Metrics

- **Unit Tests**: 89/89 tests passing (100% green).
- **Zero Compiler Warnings**: `dotnet build PixelForge.sln -c Release /warnaserror` exits with 0 errors and 0 warnings.
- **Packaging Pipeline**: Fully verified automated packaging via `.agents/scripts/pack-release.ps1`.

---

## 📦 Release Artifacts

- **Windows Installer**: `publish/win-x64/PixelForge_0.1.0_x64-setup.exe` (~35 MB)
- **OTA Manifest**: `publish/win-x64/latest.json` & `latest.json`
- **Installer Script**: `installer/PixelForge.iss`
