# Unified Style, HSV Color Wheel, Tooltips Everywhere & Detailed Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Unify the application visual theme across Light and Dark modes, provide an interactive Photoshop/Aseprite-style HSV Color Wheel (Hue ring + SV square), deliver rich explanatory tooltips for every control and tool (including stabilizer, pixel-perfect, dithering), and add a comprehensive tabbed Settings Window accessible via menu for granular customization of canvas, tools, performance, and autosave.

**Architecture:** 
1. Clean UI styling layer eliminating default Avalonia accent clashes on accordion toggles and unifying design tokens (colors, padding, borders).
2. Hardware-accelerated Avalonia custom control `ColorWheelControl` using HSV calculations from `ColorSpaceHelper` integrated as a primary tab in `ColorPickerView`.
3. Centralized rich tooltip localization in `LocalizationService` with title, shortcut badges, and clear human explanations applied to every interactive control.
4. Clean Architecture settings model in `PixelForge.Core/Services/AppSettingsService.cs` with transactional atomic persistence, paired with `SettingsViewModel` and modern tabbed `SettingsWindow` in `PixelForge.App`.

**Tech Stack:** .NET 8.0, C# 12, Avalonia UI 11.1.3, SkiaSharp 2.88.8, CommunityToolkit.Mvvm 8.3.2, xUnit, FluentAssertions.

**Spec:** User request (unified style, HSV color wheel with Hue ring + inner SV area, tooltips everywhere with explanations like stabilizer, and a detailed settings window in the menu).

## Global Constraints

- Never use synchronous I/O on UI thread; settings persistence must remain transactional (`.tmp` -> `File.Move(..., overwrite: true)`).
- Zero allocations in pointer-drag/rendering loops of the color wheel.
- Keep `PixelForge.Core` 100% free of Avalonia UI references; `AppSettingsService` and data models belong in Core, UI views in App.
- Retain backwards-compatibility of `settings.json` with fallback defaults.
- All 465+ existing tests must remain green; add new unit tests for all new features.
- Build must succeed with 0 errors and 0 warnings (`dotnet build PixelForge.sln -c Release`).

## Review Focus

1. Color Wheel Drag: Dragging the hue ring or SV box must not lock up or desynchronize when pointer moves outside control bounds.
2. Settings Persistence: Saving corrupted settings or unreadable JSON must cleanly recover to defaults without crashing the app.
3. Live Theme Switching: Toggling Light/Dark theme or changing settings must immediately update styles without requiring app restart.
4. High-DPI / Zoom scaling: Color wheel and tooltips must render crisply on fractional scaling without visual clipping.
5. Localization completeness: Every tooltip and settings option must have valid localized Russian and English strings.

---

### Task 1: UI Theme Polish & Unified Design System

**Files:**
- Modify: `src/PixelForge.App/Views/MainWindow.axaml`
- Modify: `src/PixelForge.App/Styles/DarkTheme.axaml`
- Modify: `src/PixelForge.App/Styles/LightTheme.axaml`
- Test: `tests/PixelForge.App.Tests/ThemeStylingTests.cs`

**Interfaces:**
- Consumes: Theme resource dictionaries (`DarkTheme.axaml`, `LightTheme.axaml`)
- Produces: Polished `ToggleButton.inspector-card-header`, `ToggleButton.tool-opt-btn`, and unified surface borders across the entire app window.

- [ ] **Step 1: Write the failing test for theme consistency and inspector header style**

```csharp
[Fact]
public void InspectorCardHeader_Styles_DoNotUseJarringDefaultAccent()
{
    // Verifies that ToggleButton.inspector-card-header defines explicit checked/pointerover states
    // with transparent background and subtle hover, avoiding Avalonia FluentTheme bright blue overrides.
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/PixelForge.App.Tests --filter FullyQualifiedName~ThemeStylingTests`
Expected: FAIL or missing test file.

- [ ] **Step 3: Implement style refinements in `MainWindow.axaml`, `DarkTheme.axaml`, and `LightTheme.axaml`**

- Add explicit `ToggleButton.inspector-card-header:checked`, `ToggleButton.inspector-card-header:pointerover`, and `ToggleButton.inspector-card-header:pressed` styles with `Background="Transparent"` and proper foreground colors.
- Fix capsule chips, segmented button bars, and sub-toolbar controls so active items use `AccentGlowBrush` with `AccentPrimaryBrush` borders rather than raw Avalonia default accent blocks.
- Align light and dark theme contrast tokens for secondary panels, badges, and card headers.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/PixelForge.App.Tests --filter FullyQualifiedName~ThemeStylingTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/PixelForge.App/Views/MainWindow.axaml src/PixelForge.App/Styles/DarkTheme.axaml src/PixelForge.App/Styles/LightTheme.axaml tests/PixelForge.App.Tests/ThemeStylingTests.cs
git commit -m "style: unify UI design system and fix inspector header checked states"
```

---

### Task 2: HSV Color Wheel Control & ColorPickerView Integration

**Files:**
- Create: `src/PixelForge.App/Controls/ColorWheelControl.cs`
- Modify: `src/PixelForge.App/Views/ColorPickerView.axaml`
- Modify: `src/PixelForge.App/Views/ColorPickerView.axaml.cs`
- Test: `tests/PixelForge.App.Tests/ColorWheelControlTests.cs`

**Interfaces:**
- Consumes: `ColorSpaceHelper.ToHsv`, `ColorSpaceHelper.FromHsv`, `PixelColor`
- Produces: `ColorWheelControl` Avalonia control with `SelectedColor` property, `ColorChanged` event, and interactive Hue ring + Saturation/Value central box; integrated as 3rd tab in `ColorPickerView`.

- [ ] **Step 1: Write failing unit test for `ColorWheelControl` color conversions and state updates**

```csharp
[Fact]
public void ColorWheel_SettingRgbColor_CalculatesCorrectHsvAndPositions()
{
    var control = new ColorWheelControl();
    control.SelectedColor = new PixelColor(255, 0, 0, 255); // Pure Red
    control.Hue.Should().BeInRange(359f, 360f); // or ~0f
    control.Saturation.Should().BeApproximately(1f, 0.01f);
    control.Value.Should().BeApproximately(1f, 0.01f);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/PixelForge.App.Tests --filter FullyQualifiedName~ColorWheelControlTests`
Expected: FAIL (`ColorWheelControl` not found).

- [ ] **Step 3: Implement `ColorWheelControl.cs`**

- Render Hue ring using conical/angular geometry or sweep gradient on outer ring ($r_{in} \dots r_{out}$).
- Render inner square/triangle with horizontal Saturation gradient (White $\to$ Pure Hue) and vertical Value gradient (Transparent $\to$ Black).
- Handle pointer press, move, release with capture to smoothly track dragging inside Hue ring or SV box.
- Provide `ColorChanged` event and Avalonia StyledProperty `SelectedColor`.

- [ ] **Step 4: Integrate into `ColorPickerView.axaml` & `ColorPickerView.axaml.cs`**

- Add tab switcher button `WheelTabButton` ("Круг" / "Wheel") in `ColorPickerView.axaml`.
- Add container `WheelContainer` with `ColorWheelControl`.
- Bidirectionally synchronize with Primary Color (LMB) and Secondary Color (RMB), Hex TextBox, and Sliders.

- [ ] **Step 5: Run tests to verify all pass**

Run: `dotnet test tests/PixelForge.App.Tests --filter FullyQualifiedName~ColorWheelControlTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/PixelForge.App/Controls/ColorWheelControl.cs src/PixelForge.App/Views/ColorPickerView.axaml src/PixelForge.App/Views/ColorPickerView.axaml.cs tests/PixelForge.App.Tests/ColorWheelControlTests.cs
git commit -m "feat: add HSV color wheel control with hue ring and SV square"
```

---

### Task 3: Comprehensive Tooltips System (Everywhere & Detailed)

**Files:**
- Modify: `src/PixelForge.Core/Localization/LocalizationService.cs`
- Modify: `src/PixelForge.App/Views/MainWindow.axaml`
- Modify: `src/PixelForge.App/Views/ColorPickerView.axaml`
- Modify: `src/PixelForge.App/Views/LivePreviewControl.axaml`
- Test: `tests/PixelForge.Core.Tests/LocalizationTooltipTests.cs`

**Interfaces:**
- Consumes: `LocalizationService.Instance`
- Produces: Rich localized tooltips for Stabilizer, Pixel-Perfect, Dither algorithms, Brush Size, Symmetry X/Y, Grid, Tools, History, Layers, Reference image, and Canvas navigation.

- [ ] **Step 1: Write failing test verifying that all tooltip keys exist in both RU and EN dictionaries**

```csharp
[Theory]
[InlineData("Tip_Stabilizer")]
[InlineData("Tip_PixelPerfect")]
[InlineData("Tip_Dithering")]
[InlineData("Tip_PencilTool")]
[InlineData("Tip_TileGrid")]
[InlineData("Tip_SymmetryX")]
public void TooltipKeys_AreDefinedInBothLanguages(string key)
{
    var loc = LocalizationService.Instance;
    loc.CurrentLanguage = AppLanguage.Russian;
    loc.Get(key).Should().NotBe(key).And.NotBeNullOrWhiteSpace();
    loc.CurrentLanguage = AppLanguage.English;
    loc.Get(key).Should().NotBe(key).And.NotBeNullOrWhiteSpace();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/PixelForge.Core.Tests --filter FullyQualifiedName~LocalizationTooltipTests`
Expected: FAIL (missing keys).

- [ ] **Step 3: Add all detailed tooltip strings to `LocalizationService.cs`**

- Add keys with descriptive explanations (e.g. `Tip_Stabilizer`: "Стабилизатор штриха (0-10) — сглаживание дрожания руки (LazyMouse) для идеально плавных линий и контуров").
- Add keys for Pixel-Perfect, Dithering patterns, all tools, top action bar buttons, layer operations, palette operations.
- Provide matching high-quality English translations.

- [ ] **Step 4: Bind tooltips in `MainWindow.axaml`, `ColorPickerView.axaml`, and `LivePreviewControl.axaml`**

- Apply `ToolTip.Tip="{Binding Loc[Tip_...]}"` with `ToolTip.ShowDelay="150"` across all controls.

- [ ] **Step 5: Run tests to verify all pass**

Run: `dotnet test tests/PixelForge.Core.Tests --filter FullyQualifiedName~LocalizationTooltipTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/PixelForge.Core/Localization/LocalizationService.cs src/PixelForge.App/Views/MainWindow.axaml src/PixelForge.App/Views/ColorPickerView.axaml src/PixelForge.App/Views/LivePreviewControl.axaml tests/PixelForge.Core.Tests/LocalizationTooltipTests.cs
git commit -m "feat: add comprehensive rich tooltips across all tools and controls"
```

---

### Task 4: Detailed Settings Architecture & Settings Window

**Files:**
- Modify: `src/PixelForge.Core/Services/AppSettingsService.cs`
- Create: `src/PixelForge.App/ViewModels/SettingsViewModel.cs`
- Create: `src/PixelForge.App/Views/SettingsWindow.axaml`
- Create: `src/PixelForge.App/Views/SettingsWindow.axaml.cs`
- Modify: `src/PixelForge.App/Views/MainWindow.axaml`
- Modify: `src/PixelForge.App/Views/MainWindow.axaml.cs`
- Modify: `src/PixelForge.App/ViewModels/MainViewModel.cs`
- Test: `tests/PixelForge.Core.Tests/AppSettingsExtendedTests.cs`
- Test: `tests/PixelForge.App.Tests/SettingsViewModelTests.cs`

**Interfaces:**
- Consumes: `AppSettingsService`, `ThemeService`, `LocalizationService`, `IAutosaveService`
- Produces: `SettingsViewModel`, `SettingsWindow`, menu triggers `Правка -> Настройки...` (`Ctrl+,`) and `Файл -> Настройки...`, immediate application of configured values to canvas and tools.

- [ ] **Step 1: Write failing unit tests for extended settings model and SettingsViewModel**

```csharp
[Fact]
public void AppSettings_StoresAndRetrievesExtendedOptions()
{
    var service = new AppSettingsService(tempFilePath);
    service.AutosaveIntervalMinutes = 10;
    service.DefaultCanvasWidth = 64;
    service.DefaultStabilizerStrength = 3;
    service.Save();

    var reloaded = new AppSettingsService(tempFilePath);
    reloaded.AutosaveIntervalMinutes.Should().Be(10);
    reloaded.DefaultCanvasWidth.Should().Be(64);
    reloaded.DefaultStabilizerStrength.Should().Be(3);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/PixelForge.Core.Tests --filter FullyQualifiedName~AppSettingsExtendedTests`
Expected: FAIL.

- [ ] **Step 3: Implement extended `AppSettingsData` and `AppSettingsService`**

- Add properties for General (Theme, Language, UpdateCheck), Canvas & Grid (Default dimensions, pixel grid threshold, tile grid dimensions, checkerboard colors), Tools (Default brush size, stabilizer, pixel-perfect), History & Autosave (Autosave interval, max undo steps), Performance (Preload, Skia HW).
- Ensure safe migration and default fallback for older `settings.json`.

- [ ] **Step 4: Implement `SettingsViewModel.cs` and `SettingsWindow.axaml`**

- `SettingsViewModel`: Properties bound to `AppSettingsService`, `ApplyCommand`, `ResetDefaultsCommand`, `CancelCommand`.
- `SettingsWindow.axaml`: Multi-category tabbed UI:
  1. ⚙️ Общие (General)
  2. 🖼 Холст и сетка (Canvas & Grid)
  3. 🖌 Инструменты (Tools Defaults)
  4. 💾 История и автосохранение (Autosave & History)
  5. ⚡ Производительность (Performance)
  6. ⌨ Горячие клавиши (Hotkeys)
- Save / Apply / Cancel / Reset buttons.

- [ ] **Step 5: Add Settings menu item in `MainWindow.axaml` and wire up command in `MainWindow.axaml.cs`**

- Add `Правка -> Настройки...` with shortcut `Ctrl+,`.
- Add `Файл -> Настройки...`.
- In `OpenSettings()`, instantiate `SettingsViewModel`, show `SettingsWindow`, and on save apply settings directly to `MainViewModel`, `CanvasViewModel`, and `ToolViewModel`.

- [ ] **Step 6: Run tests to verify all pass**

Run: `dotnet test tests/PixelForge.Core.Tests --filter FullyQualifiedName~AppSettingsExtendedTests`
Run: `dotnet test tests/PixelForge.App.Tests --filter FullyQualifiedName~SettingsViewModelTests`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/PixelForge.Core/Services/AppSettingsService.cs src/PixelForge.App/ViewModels/SettingsViewModel.cs src/PixelForge.App/Views/SettingsWindow.axaml src/PixelForge.App/Views/SettingsWindow.axaml.cs src/PixelForge.App/Views/MainWindow.axaml src/PixelForge.App/Views/MainWindow.axaml.cs src/PixelForge.App/ViewModels/MainViewModel.cs tests/PixelForge.Core.Tests/AppSettingsExtendedTests.cs tests/PixelForge.App.Tests/SettingsViewModelTests.cs
git commit -m "feat: add comprehensive settings window and extended app configuration"
```

---

### Task 5: Integration, Full Test Suite & Release Verification

**Files:**
- Modify: `tests/PixelForge.Core.Tests/*`
- Modify: `tests/PixelForge.App.Tests/*`

- [ ] **Step 1: Run full solution unit tests**

Run: `dotnet test`
Expected: 100% of tests pass (all 465+ existing plus new tests).

- [ ] **Step 2: Build release configuration**

Run: `dotnet build PixelForge.sln -c Release`
Expected: 0 errors, 0 warnings.

- [ ] **Step 3: Verification check on DoD**

- Check that no allocations occur in pointer moved.
- Verify backwards compatibility of `.pxproj` and `settings.json`.
- Verify Russian and English text consistency.

- [ ] **Step 4: Commit**

```bash
git commit --allow-empty -m "chore: verify test suite and release build readiness"
```
