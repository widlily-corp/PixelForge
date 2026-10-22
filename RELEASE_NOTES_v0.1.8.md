# Release Notes - PixelForge v0.1.8 (Studio UI Polish & Cyber Splash)

## 🇷🇺 Что нового в PixelForge v0.1.8

### 1. Устранение всех наложений и перекрытий интерфейса (Zero-Overlap Canvas)
- **Чистый холст без плавающих капсул:**
  - Удалена плавающая плашка горячих клавиш мыши, находившаяся прямо над холстом и перекрывавшая нижнюю часть рисуемого пиксель-арта.
  - Подсказки мыши (`ЛКМ Рисовать`, `ПКМ Стереть`, `Space+Drag Панорама`, `Ctrl+Колесо Зум`) аккуратно интегрированы в строку состояния (Status Bar) внизу окна.
- **Адаптивный правый инспектор:**
  - Вся правая колонка (`LivePreviewControl`, `ColorPickerView`, `ReferenceOverlay`, `LayersManager`) теперь обёрнута в интеллектуальный `ScrollViewer` (`VerticalScrollBarVisibility="Auto"`).
  - Панель слоёв больше никогда не сплющивается и не обрезается на экранах с масштабированием 125%/150% или при уменьшении высоты окна (гарантированная минимальная высота 220px).
  - В `LivePreviewControl` добавлен переключатель сворачивания/разворачивания (`▲/▼`), позволяющий свернуть 1x-превью до компактной полосы в один клик.
  - Панель полупрозрачного референса теперь компактна и показывает ползунок прозрачности только при загруженном референсном изображении.
- **Компактная панель быстрых инструментов:**
  - Кнопки переключения сетки, осей симметрии X/Y, тайлинга и тайловой сетки переведены на аккуратные квадратные кнопки с подсветкой и подробными всплывающими подсказками, что освободило более 250px горизонтального пространства.
  - В контекстной панели активного инструмента убран избыточный дубликат бейджа зума, что предотвращает переполнение чипов настроек инструмента.

### 2. Профессиональный экран предзагрузки (Cyber-Studio Splash Screen)
- **Студийный визуальный стиль уровня Rider / Blender:**
  - Премиальный обсидиановый контейнер (`#0A0D15`) со скруглёнными углами 16px и неоновым свечением контура (`#00D2FF25`).
  - Бесшовный кинематографичный градиентный переход пиксель-арт баннера в тёмную тему окна.
  - Фирменный значок PixelForge STUDIO с динамическим отображением версии из метаданных сборок.
- **Матрица диагностики подсистем в реальном времени:**
  - 4 динамических модуля статуса:
    - ❖ `ОЗУ пул 1024 МБ` (Working set resident)
    - ⚡ `Pre-JIT движок` (Hot paths compiled)
    - 🎨 `SkiaSharp 60+ FPS` (Hardware raster pipeline)
    - ↺ `Zero-Alloc дельта` (Sparse undo/redo kernel)
  - Индикаторы загораются изумрудным и бирюзовым цветом по мере прохождения фаз инициализации.
  - Ультратонкий светящийся неоновый прогресс-бар и вывод прогресса в процентах.

### 3. Инженерия и качество
- **443 модульных теста (AAA xUnit):** 100% тестов пройдены успешно с 0 ошибками и 0 предупреждениями компилятора (`TreatWarningsAsErrors=true`).
- **Синхронизация потоков тестирования:** Добавлена строгая изоляция общего состояния локализации через xUnit Test Collection.
- **Компактный инсталлятор Inno Setup:** Сжатие LZMA2/Ultra (35.37 МБ) с регистрацией расширения `.pxproj` и автоматической проверкой OTA через `latest.json`.

---

## 🇬🇧 What's New in PixelForge v0.1.8

### 1. Canvas & Inspector Overlap Fixes
- **Unobstructed Viewport:** Removed the floating mouse shortcut capsule from the bottom of the canvas. Mouse hints are now seamlessly docked in the bottom status bar.
- **Adaptive Right Column:** Wrapped inspector panels in an intelligent `ScrollViewer`. Layers manager never squashes or clips on laptops or scaled monitors.
- **Collapsible Live Preview:** Added a quick collapse/expand toggle (`▲/▼`) to fold 1x preview when extra vertical workspace is desired.
- **Smart Reference Panel:** Compact card hiding the opacity slider when no image is loaded.
- **Compact Quick Toolbar:** Overlay toggles now use streamlined icon buttons with radiant active states, saving 250+ px.

### 2. Professional Cyber-Studio Splash Screen
- **Studio Aesthetics:** Obsidian backdrop (`#0A0D15`), 16px rounded corners, and glowing cyan perimeter rim.
- **Live 4-Engine Diagnostic Telemetry:** Dynamic indicators for RAM resident matrix, Pre-JIT compiler, Skia GPU pipeline, and Zero-Alloc delta history.
- **Real-Time Telemetry Illumination:** Status nodes light up in emerald and cyan as bootstrap phases progress.
- **Sleek Progress Bar:** Thin glowing neon accent bar with precise percentage readout.

### 3. Architecture & Reliability
- **443 AAA Unit Tests:** 100% green pass with 0 compiler warnings.
- **Single Source of Truth:** Centralized version management in `Directory.Build.props`.
- **Inno Setup LZMA2 Packaging:** Compact 35.37 MB installer with `.pxproj` associations and OTA update manifests.
