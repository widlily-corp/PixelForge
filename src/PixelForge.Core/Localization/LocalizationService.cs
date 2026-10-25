using System.ComponentModel;

namespace PixelForge.Core.Localization;

/// <summary>
/// Supported interface languages.
/// </summary>
public enum AppLanguage
{
    Russian,
    English
}

/// <summary>
/// Thread-safe centralized localization service providing instant dynamic language switching
/// across all views, controls, menus, tooltips, dialogs, and status messages.
/// </summary>
public class LocalizationService : INotifyPropertyChanged
{
    public static LocalizationService Instance { get; } = new();

    private AppLanguage _currentLanguage = AppLanguage.Russian;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<AppLanguage>? LanguageChanged;

    public AppLanguage CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                OnPropertyChanged(string.Empty);
                OnPropertyChanged("Item[]");
                LanguageChanged?.Invoke(this, value);
            }
        }
    }

    public bool IsRussian => _currentLanguage == AppLanguage.Russian;
    public bool IsEnglish => _currentLanguage == AppLanguage.English;

    /// <summary>
    /// Indexer for dynamic XAML data binding: {Binding [Key], Source={x:Static loc:LocalizationService.Instance}}
    /// </summary>
    public string this[string key] => Get(key);

    /// <summary>
    /// Retrieves the localized string for the specified key in the current language.
    /// </summary>
    public string Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;

        var dict = _currentLanguage == AppLanguage.Russian ? StringsRu : StringsEn;
        if (dict.TryGetValue(key, out var val))
        {
            return val;
        }

        // Fallback to English dictionary if missing in Russian
        if (StringsEn.TryGetValue(key, out var enVal))
        {
            return enVal;
        }

        return key;
    }

    /// <summary>
    /// Formats a localized string with arguments.
    /// </summary>
    public string Format(string key, params object[] args)
    {
        var template = Get(key);
        try
        {
            return string.Format(template, args);
        }
        catch
        {
            return template;
        }
    }

    public void SetLanguage(AppLanguage language)
    {
        CurrentLanguage = language;
    }

    public void ToggleLanguage()
    {
        CurrentLanguage = _currentLanguage == AppLanguage.Russian ? AppLanguage.English : AppLanguage.Russian;
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // ==========================================
    // RUSSIAN DICTIONARY
    // ==========================================
    private static readonly Dictionary<string, string> StringsRu = new(StringComparer.OrdinalIgnoreCase)
    {
        // Window & Title
        ["App_Title"] = "PixelForge",
        ["App_Untitled"] = "Без названия",
        ["App_Subtitle"] = "ПРОФЕССИОНАЛЬНАЯ СТУДИЯ ПИКСЕЛЬ-АРТА",

        // Top Menu: File
        ["Menu_File"] = "Файл",
        ["Menu_New"] = "Новый холст...",
        ["Menu_Open"] = "Открыть проект...",
        ["Menu_RecentFiles"] = "Недавние проекты",
        ["Menu_NoRecentFiles"] = "Нет недавних проектов",
        ["Menu_ClearRecentFiles"] = "Очистить список недавних",
        ["Menu_Save"] = "Сохранить",
        ["Menu_SaveAs"] = "Сохранить как...",
        ["Menu_ExportPng"] = "Экспорт в PNG...",
        ["Menu_ExportBmp"] = "Экспорт в BMP...",
        ["Menu_AssociateFiles"] = "Ассоциировать файлы .pxproj с PixelForge",
        ["Menu_Exit"] = "Выход",

        // Top Menu: Edit
        ["Menu_Edit"] = "Правка",
        ["Menu_Undo"] = "Отменить",
        ["Menu_Redo"] = "Повторить",
        ["Menu_Cut"] = "Вырезать",
        ["Menu_Copy"] = "Копировать",
        ["Menu_Paste"] = "Вставить",
        ["Menu_SelectAll"] = "Выделить всё",
        ["Menu_Deselect"] = "Снять выделение",
        ["Menu_DeleteSelection"] = "Удалить выделение",

        // Top Menu: Canvas
        ["Menu_Canvas"] = "Холст",
        ["Menu_ResizeCanvas"] = "Размер холста...",
        ["Menu_LayerTransform"] = "Трансформация слоя",
        ["Menu_FlipLayerH"] = "Отразить слой по горизонтали (H)",
        ["Menu_FlipLayerV"] = "Отразить слой по вертикали (V)",
        ["Menu_RotateLayerCW"] = "Повернуть слой на 90° по часовой",
        ["Menu_RotateLayerCCW"] = "Повернуть слой на 90° против часовой",
        ["Menu_RotateLayer180"] = "Повернуть слой на 180°",
        ["Menu_CanvasTransform"] = "Трансформация холста",
        ["Menu_FlipCanvasH"] = "Отразить холст по горизонтали",
        ["Menu_FlipCanvasV"] = "Отразить холст по вертикали",
        ["Menu_RotateCanvasCW"] = "Повернуть холст на 90° по часовой",
        ["Menu_RotateCanvasCCW"] = "Повернуть холст на 90° против часовой",
        ["Menu_RotateCanvas180"] = "Повернуть холст на 180°",
        ["Menu_ClearActiveLayer"] = "Очистить активный слой",
        ["Menu_SmartTools"] = "Смарт-инструменты",
        ["Menu_AddOutline"] = "Добавить контур (Outline)...",
        ["Menu_AddShadow"] = "Добавить тень (Shadow)...",
        ["Menu_ReplaceColor"] = "Заменить цвет (Replace Color)...",
        ["Menu_ColorRamp"] = "Генератор цветовой рампы (Color Ramp)...",
        ["Menu_TiledMode"] = "Бесшовный режим (Tiled Mode)",
        ["Menu_ReferenceImage"] = "Референс-изображение",
        ["Menu_LoadReference"] = "Загрузить референс...",
        ["Menu_ToggleReference"] = "Показать/скрыть референс",
        ["Menu_ClearReference"] = "Удалить референс",

        // Top Menu: View
        ["Menu_View"] = "Вид",
        ["Menu_Grid"] = "Пиксельная сетка",
        ["Menu_TileGrid"] = "Сетка тайлов",
        ["Menu_SymmetryX"] = "Симметрия по горизонтали (X)",
        ["Menu_SymmetryY"] = "Симметрия по вертикали (Y)",
        ["Menu_ZoomIn"] = "Приблизить",
        ["Menu_ZoomOut"] = "Отдалить",
        ["Menu_ResetZoom"] = "Сбросить масштаб (100%)",
        ["Menu_ToggleTheme"] = "Переключить тему (Тёмная/Светлая)",
        ["Menu_Language"] = "Язык (Language)",
        ["Menu_LangRu"] = "Русский (Russian)",
        ["Menu_LangEn"] = "English (Английский)",

        // Top Menu: Help
        ["Menu_Help"] = "Справка",
        ["Menu_Hotkeys"] = "Горячие клавиши... (F1)",
        ["Menu_CheckUpdates"] = "Проверить обновления...",
        ["Menu_About"] = "О программе...",

        // Quick Toolbar Tooltips
        ["Tip_NewCanvas"] = "Новый холст (Ctrl+N)",
        ["Tip_ResizeCanvas"] = "Изменить размер холста (Ctrl+Alt+C)",
        ["Tip_OpenProject"] = "Открыть проект (Ctrl+O)",
        ["Tip_SaveProject"] = "Сохранить проект (Ctrl+S)",
        ["Tip_QuickExport"] = "Быстрый экспорт (PNG / BMP)",
        ["Tip_FlipH"] = "Отразить активный слой по горизонтали (Shift+H)",
        ["Tip_FlipV"] = "Отразить активный слой по вертикали (V)",
        ["Tip_RotateCW"] = "Повернуть активный слой на 90° по часовой",
        ["Tip_SmartTools"] = "Смарт-инструменты (FX) и эффекты",
        ["Tip_Undo"] = "Отменить (Ctrl+Z)",
        ["Tip_Redo"] = "Повторить (Ctrl+Y)",
        ["Tip_Grid"] = "Пиксельная сетка (Ctrl+G)",
        ["Tip_TileGrid"] = "Сетка тайлов (Ctrl+Shift+G)",
        ["Tip_SymX"] = "Симметрия X (зеркало по горизонтали)",
        ["Tip_SymY"] = "Симметрия Y (зеркало по вертикали)",
        ["Tip_Tiled"] = "Бесшовный режим (Tiled Mode) (F10)",
        ["Tip_ZoomOut"] = "Отдалить (Ctrl+-)",
        ["Tip_ZoomIn"] = "Приблизить (Ctrl++)",
        ["Tip_ZoomFit"] = "100% масштаб",
        ["Tip_ToggleTheme"] = "Переключить тему оформления",
        ["Tip_ToggleLanguage"] = "Переключить язык интерфейса (RU / EN)",

        // Toolbar Button Labels
        ["Label_Grid"] = "Сетка",
        ["Label_TileGrid"] = "Тайлы",
        ["Label_SymX"] = "Симм. X",
        ["Label_SymY"] = "Симм. Y",
        ["Label_Tiled"] = "Тайлы",

        // Update Banner
        ["Banner_UpdateNow"] = "Обновить сейчас",
        ["Banner_Website"] = "Сайт",
        ["Banner_UpdateTooltip"] = "Автоматически скачать, установить и перезапустить PixelForge",

        // Tool Strip Sections
        ["ToolSection_Drawing"] = "РИСОВАНИЕ",
        ["ToolSection_DrawingTip"] = "Инструменты свободного рисования",
        ["ToolSection_Fill"] = "ЗАЛИВКА",
        ["ToolSection_FillTip"] = "Инструменты заливки и выбора цвета",
        ["ToolSection_Shapes"] = "ФИГУРЫ",
        ["ToolSection_ShapesTip"] = "Геометрические фигуры",
        ["ToolSection_Select"] = "ВЫБОР",
        ["ToolSection_SelectTip"] = "Выделение области",

        // Tool Names & Tooltips
        ["Tool_Pencil"] = "Карандаш",
        ["Tool_PencilTip"] = "Карандаш (B)",
        ["Tool_Eraser"] = "Ластик",
        ["Tool_EraserTip"] = "Ластик (E)",
        ["Tool_DitherBrush"] = "Дизеринг кисть",
        ["Tool_DitherBrushTip"] = "Дизеринг кисть (D)",
        ["Tool_PatternBrush"] = "Текстурная кисть",
        ["Tool_PatternBrushTip"] = "Текстурная кисть 8x8 (P)",
        ["Tool_ShadeBrush"] = "Кисть светотени",
        ["Tool_ShadeBrushTip"] = "Кисть светотени (H) - ЛКМ темнее, ПКМ светлее",
        ["Tool_SprayBrush"] = "Спрей / Шум",
        ["Tool_SprayBrushTip"] = "Спрей / Шум (A)",
        ["Tool_Fill"] = "Заливка",
        ["Tool_FillTip"] = "Заливка (G)",
        ["Tool_MagicWand"] = "Волшебная палочка",
        ["Tool_MagicWandTip"] = "Волшебная палочка (W)",
        ["Tool_Gradient"] = "Градиент",
        ["Tool_GradientTip"] = "Градиент (T)",
        ["Tool_Eyedropper"] = "Пипетка",
        ["Tool_EyedropperTip"] = "Пипетка (I)",
        ["Tool_Line"] = "Линия",
        ["Tool_LineTip"] = "Линия (L)",
        ["Tool_Rectangle"] = "Прямоугольник",
        ["Tool_RectangleTip"] = "Прямоугольник (R / U)",
        ["Tool_Ellipse"] = "Эллипс",
        ["Tool_EllipseTip"] = "Эллипс (C)",
        ["Tool_Selection"] = "Выделение",
        ["Tool_SelectionTip"] = "Выделение (S / M)",

        // Brush Size Widget
        ["Brush_Header"] = "КИСТЬ",
        ["Brush_SizeCurrentTip"] = "Текущий размер кисти (1–32 px)",
        ["Brush_DecreaseTip"] = "Уменьшить кисть ([)",
        ["Brush_IncreaseTip"] = "Увеличить кисть (])",

        // Context Bar
        ["Context_Size"] = "Размер:",
        ["Context_BrushTip1"] = "Кисть 1 px",
        ["Context_BrushTip2"] = "Кисть 2 px",
        ["Context_BrushTip3"] = "Кисть 3 px",
        ["Context_BrushTip4"] = "Кисть 4 px",
        ["Context_BrushTip8"] = "Кисть 8 px",
        ["Context_PixelPerfect"] = "Pixel-Perfect",
        ["Context_PixelPerfectTip"] = "Pixel-Perfect: устранение изломов в 1px линиях (Shift+P)",
        ["Context_TileGrid"] = "Сетка тайлов",
        ["Context_TileSize"] = "Шаг тайла",
        ["Context_SprayRadius"] = "Радиус:",
        ["Context_SprayDensity"] = "Плотность:",
        ["Context_Stabilizer"] = "Стабилизатор:",
        ["Context_Texture8x8"] = "Текстура 8x8:",
        ["Context_Dither"] = "Дизеринг:",
        ["Context_Tolerance"] = "Порог:",
        ["Context_Contiguous"] = "Смежные",
        ["Context_ContiguousTip"] = "Заливать только соединённую область или все схожие цвета на слое",
        ["Context_Gradient"] = "Градиент:",
        ["Context_Preset"] = "Пресет:",
        ["Context_PresetTip"] = "Многоточечные градиентные пресеты и палитровые рампы",
        ["Context_Ramp"] = "Рампа...",
        ["Context_RampTip"] = "Генератор палитровых рамп с Hue Shifting (от тени к свету)",
        ["Context_ShapeFill"] = "Заливка фигуры",
        ["Context_Zoom"] = "Масштаб:",

        // Canvas Floating HUD
        ["Hud_Lmb"] = "ЛКМ",
        ["Hud_Draw"] = "Рисовать",
        ["Hud_Rmb"] = "ПКМ",
        ["Hud_EraseSecondary"] = "Стереть/Вторичный",
        ["Hud_SpaceDrag"] = "Пробел+Drag",
        ["Hud_Pan"] = "Панорама",
        ["Hud_CtrlWheel"] = "Ctrl+Колесо",
        ["Hud_Zoom"] = "Зум",

        // Right Inspector: Live Preview
        ["Panel_LivePreview"] = "ПРЕДПРОСМОТР",
        ["Panel_TogglePreview"] = "Свернуть / развернуть предпросмотр",
        ["Preview_Scale"] = "Масштаб:",

        // Right Inspector: Color Picker & Palette
        ["Panel_ColorPicker"] = "ПАЛИТРА И ЦВЕТ",
        ["Color_Primary"] = "Основной",
        ["Color_Secondary"] = "Вторичный",
        ["Color_SwapTip"] = "Поменять цвета местами (X)",
        ["Color_AddColorTip"] = "Добавить текущий цвет в рабочую палитру",
        ["Color_RemoveColorTip"] = "Удалить выбранный цвет из палитры",
        ["Color_PresetsTip"] = "Выбрать готовую палитру (DawnBringer, PICO-8 и др.)",

        // Right Inspector: Layers
        ["Panel_Layers"] = "СЛОИ",
        ["Layer_AddTip"] = "Добавить слой",
        ["Layer_DuplicateTip"] = "Дублировать слой",
        ["Layer_MoveUpTip"] = "Поднять слой выше",
        ["Layer_MoveDownTip"] = "Опустить слой ниже",
        ["Layer_MergeDownTip"] = "Объединить с нижним",
        ["Layer_DeleteTip"] = "Удалить слой",
        ["Layer_Opacity"] = "Непрозр.",
        ["Layer_VisibilityTip"] = "Видимость слоя",
        ["Layer_AlphaLockTip"] = "Блокировка прозрачности (Alpha Lock)",

        // Right Inspector: Reference Image Overlay
        ["Panel_Reference"] = "РЕФЕРЕНС",
        ["Ref_Load"] = "Загрузить...",
        ["Ref_LoadTip"] = "Загрузить изображение-референс (PNG, JPG, BMP)",
        ["Ref_Clear"] = "Удалить",
        ["Ref_ClearTip"] = "Удалить текущий референс с холста",
        ["Ref_Opacity"] = "Прозрачность:",
        ["Ref_VisibilityTip"] = "Показать или скрыть изображение-референс",
        ["Ref_FitCanvas"] = "Под размер холста",

        // Status Bar
        ["Status_Ready"] = "Готов к работе",
        ["Status_DimensionsTip"] = "Разрешение холста — нажмите для изменения размера (Ctrl+Alt+C)",
        ["Status_TileGridTip"] = "Сетка тайлов (нажмите для смены размера)",
        ["Status_Zoom"] = "Зум:",
        ["Status_ZoomTip"] = "Масштаб: {0}% — Клик для смены шага, Ctrl+1..4 для пресетов",
        ["Status_LangButton"] = "RU 🇷🇺",
        ["Status_LangButtonTip"] = "Сменить язык (Текущий: Русский) / Switch language",
        ["Status_Selection"] = "Выделение: {0}",
        ["Status_SelectionTip"] = "Выделенная область: {0} — Клик для снятия (Ctrl+D)",
        ["Status_TiledActiveTip"] = "Бесшовный режим 3×3 активен (F10)",
        ["Status_SymmetryTip"] = "Симметрия: {0} (Shift+X / Shift+Y)",
        ["Status_PixelPerfectTip"] = "Pixel-Perfect режим активен (Shift+P)",
        ["Status_AlphaLockTip"] = "Блокировка прозрачности активна на текущем слое",
        ["Status_ColorUnderCursorTip"] = "Цвет под курсором: {0}",

        // Dynamic Status Messages
        ["Msg_LoadedProject"] = "Загружен проект {0}",
        ["Msg_SavedProject"] = "Сохранено в {0}",
        ["Msg_NewCanvas"] = "Новый холст ({0}×{1})",
        ["Msg_Autosaved"] = "Автосохранение в {0:HH:mm:ss}",
        ["Msg_AutosaveError"] = "Ошибка автосохранения: {0}",
        ["Msg_ResizeSuccess"] = "Размер холста изменён на {0} × {1} px",
        ["Msg_LayerFlippedH"] = "{0} отражён по горизонтали",
        ["Msg_LayerFlippedV"] = "{0} отражён по вертикали",
        ["Msg_CanvasFlippedH"] = "Холст отражён по горизонтали",
        ["Msg_CanvasFlippedV"] = "Холст отражён по вертикали",
        ["Msg_LayerRotated"] = "{0} повёрнут {1}",
        ["Msg_CanvasRotated"] = "Холст повёрнут {1}",
        ["Msg_LayerCleared"] = "Слой '{0}' очищен",
        ["Msg_ReferenceLoaded"] = "Референс загружен: {0}",
        ["Msg_ReferenceCleared"] = "Референс удалён",
        ["Msg_AssociationDone"] = "Ассоциация .pxproj с PixelForge успешно зарегистрирована в Windows!",
        ["Msg_SelectAll"] = "Выделен весь холст (Ctrl+A)",
        ["Msg_Deselect"] = "Выделение снято (Ctrl+D)",
        ["Msg_DeleteSelection"] = "Выделенная область удалена",
        ["Msg_LayerAdded"] = "Создан слой '{0}' (Ctrl+Shift+N)",
        ["Msg_LayerDuplicated"] = "Слой '{0}' продублирован (Ctrl+J)",
        ["Msg_LayerRemoved"] = "Удалён слой '{0}'",
        ["Msg_LayerMergedDown"] = "Слой '{0}' объединён с нижним (Ctrl+E)",
        ["Msg_LayerVisibility"] = "Слой '{0}' теперь {1} (Shift+L)",
        ["Msg_LayerAlphaLock"] = "Блокировка прозрачности для '{0}': {1}",
        ["Msg_ColorsSwapped"] = "Цвета поменялись местами (X)",
        ["Msg_EyedropperPicked"] = "Пипетка: выбран цвет {0}",
        ["Msg_ColorAdded"] = "Цвет {0} добавлен в палитру",
        ["Msg_PaletteNext"] = "Выбран цвет {0} (клавиша .)",
        ["Msg_PalettePrev"] = "Выбран цвет {0} (клавиша ,)",
        ["Msg_BrushSize"] = "Размер кисти: {0} px",
        ["Msg_ZoomSet"] = "Масштаб: {0}%",
        ["Text_Enabled"] = "включена",
        ["Text_Disabled"] = "выключена",
        ["Text_Visible"] = "видимый",
        ["Text_Hidden"] = "скрытый",
        ["Dlg_Hotkeys_ZoomPresets"] = "Пресеты масштаба (100%..800%)",
        ["Dlg_Hotkeys_LayerNew"] = "Создать новый слой",
        ["Dlg_Hotkeys_LayerMerge"] = "Объединить с нижним слоем",
        ["Dlg_Hotkeys_LayerDuplicate"] = "Дублировать активный слой",
        ["Dlg_Hotkeys_LayerVisibility"] = "Скрыть / показать активный слой",
        ["Dlg_Hotkeys_SymmetryX"] = "Переключить симметрию по X",
        ["Dlg_Hotkeys_SymmetryY"] = "Переключить симметрию по Y",
        ["Dlg_Hotkeys_PaletteCycle"] = "Выбор цвета в палитре (, / .)",
        ["Dlg_Hotkeys_BrushPresets"] = "Размер кисти (клавиши 1..8)",
        ["Dlg_Hotkeys_CycleTileGrid"] = "Сменить размер сетки тайлов (Alt+G)",

        // Dialog: New Canvas
        ["Dlg_NewCanvasTitle"] = "Новый холст — PixelForge",
        ["Dlg_NewCanvasHeader"] = "Новый холст",
        ["Dlg_NewCanvasSubheader"] = "Выберите предустановку или укажите произвольный размер",
        ["Dlg_NewCanvasQuickPresets"] = "БЫСТРЫЕ ПРЕСЕТЫ",
        ["Dlg_NewCanvasIcon"] = "Иконка",
        ["Dlg_NewCanvasSprite"] = "Спрайт",
        ["Dlg_NewCanvasDetailed"] = "Детальный арт",
        ["Dlg_NewCanvasCustomSize"] = "ПРОИЗВОЛЬНЫЙ РАЗМЕР (1–4096 px)",
        ["Dlg_NewCanvasWidth"] = "Ширина (px)",
        ["Dlg_NewCanvasHeight"] = "Высота (px)",
        ["Dlg_NewCanvasCreate"] = "Создать холст",
        ["Dlg_NewCanvasCancel"] = "Отмена",
        ["Dlg_NewCanvasError"] = "Размеры холста должны быть от 1 до 4096 пикселей.",

        // Dialog: Resize Canvas
        ["Dlg_ResizeTitle"] = "Размер холста — PixelForge",
        ["Dlg_ResizeHeader"] = "Размер холста",
        ["Dlg_ResizeCurrent"] = "Текущий размер: {0} × {1} px",
        ["Dlg_ResizeDimensions"] = "НОВЫЙ РАЗМЕР (1–4096 px)",
        ["Dlg_ResizeWidth"] = "Ширина (px)",
        ["Dlg_ResizeHeight"] = "Высота (px)",
        ["Dlg_ResizeKeepAspect"] = "Сохранять пропорции (1:1)",
        ["Dlg_ResizePresets"] = "БЫСТРЫЕ ПРЕСЕТЫ И МАСШТАБ",
        ["Dlg_ResizeMode"] = "РЕЖИМ ИЗМЕНЕНИЯ",
        ["Dlg_ResizeAnchorMode"] = "Якорь размещения",
        ["Dlg_ResizeScaleMode"] = "Масштабировать пиксели",
        ["Dlg_ResizeAnchorPoint"] = "Точка привязки (Якорь):",
        ["Dlg_ResizeAnchor_TopLeft"] = "Сверху-слева (расширение вправо и вниз)",
        ["Dlg_ResizeAnchor_TopCenter"] = "Сверху по центру (расширение по бокам и вниз)",
        ["Dlg_ResizeAnchor_TopRight"] = "Сверху-справа (расширение влево и вниз)",
        ["Dlg_ResizeAnchor_MiddleLeft"] = "По центру слева (расширение вправо)",
        ["Dlg_ResizeAnchor_Center"] = "По центру (равномерное расширение во все стороны)",
        ["Dlg_ResizeAnchor_MiddleRight"] = "По центру справа (расширение влево)",
        ["Dlg_ResizeAnchor_BottomLeft"] = "Снизу-слева (расширение вправо и вверх)",
        ["Dlg_ResizeAnchor_BottomCenter"] = "Снизу по центру (расширение по бокам и вверх)",
        ["Dlg_ResizeAnchor_BottomRight"] = "Снизу-справа (расширение влево и вверх)",
        ["Dlg_ResizeApply"] = "Применить",
        ["Dlg_ResizeCancel"] = "Отмена",
        ["Dlg_ResizeError"] = "Размеры холста должны быть от 1 до 4096 пикселей.",

        // Dialog: Hotkeys
        ["Dlg_HotkeysTitle"] = "Горячие клавиши PixelForge",
        ["Dlg_HotkeysHeader"] = "ГОРЯЧИЕ КЛАВИШИ",
        ["Dlg_HotkeysSubheader"] = "Справочник клавиатурных комбинаций для максимальной скорости работы",
        ["Dlg_HotkeysToolsTab"] = "ИНСТРУМЕНТЫ",
        ["Dlg_HotkeysCanvasTab"] = "РИСОВАНИЕ И ХОЛСТ",
        ["Dlg_HotkeysNavTab"] = "НАВИГАЦИЯ",
        ["Dlg_HotkeysProjectTab"] = "ПРОЕКТ И ИСТОРИЯ",
        ["Dlg_Hotkeys_Pencil"] = "Карандаш",
        ["Dlg_Hotkeys_Eraser"] = "Ластик",
        ["Dlg_Hotkeys_Dither"] = "Дизеринг",
        ["Dlg_Hotkeys_Pattern"] = "Паттерн",
        ["Dlg_Hotkeys_Shade"] = "Кисть светотени",
        ["Dlg_Hotkeys_Spray"] = "Спрей / Шум",
        ["Dlg_Hotkeys_Fill"] = "Заливка",
        ["Dlg_Hotkeys_Wand"] = "Волшебная палочка",
        ["Dlg_Hotkeys_Gradient"] = "Градиент",
        ["Dlg_Hotkeys_Eyedropper"] = "Пипетка",
        ["Dlg_Hotkeys_Line"] = "Линия",
        ["Dlg_Hotkeys_Rect"] = "Прямоугольник",
        ["Dlg_Hotkeys_Ellipse"] = "Эллипс",
        ["Dlg_Hotkeys_Selection"] = "Выделение",
        ["Dlg_Hotkeys_PixelPerfect"] = "Pixel-Perfect режим",
        ["Dlg_Hotkeys_TileGrid"] = "Сетка тайлов",
        ["Dlg_Hotkeys_Tiled"] = "Бесшовный режим",
        ["Dlg_Hotkeys_SwapColors"] = "Поменять цвета местами",
        ["Dlg_Hotkeys_BrushSize"] = "Размер кисти",
        ["Dlg_Hotkeys_CanvasSize"] = "Размер холста",
        ["Dlg_Hotkeys_Grid"] = "Пиксельная сетка",
        ["Dlg_Hotkeys_Pan"] = "Панорама холста",
        ["Dlg_Hotkeys_Zoom"] = "Масштабирование",
        ["Dlg_Hotkeys_Undo"] = "Отмена действия",
        ["Dlg_Hotkeys_Redo"] = "Повтор действия",
        ["Dlg_Hotkeys_Save"] = "Сохранить",
        ["Dlg_Hotkeys_Open"] = "Открыть",
        ["Dlg_Hotkeys_New"] = "Новый холст",
        ["Dlg_Hotkeys_EscNotice"] = "Нажмите Esc для быстрого закрытия",
        ["Dlg_HotkeysClose"] = "Понятно / Закрыть",

        // Dialog: Color Ramp
        ["Dlg_RampTitle"] = "Генератор цветовой рампы — PixelForge",
        ["Dlg_RampHeader"] = "ГЕНЕРАТОР ЦВЕТОВОЙ РАМПЫ",
        ["Dlg_RampSubheader"] = "Художественное построение градиентов со сдвигом оттенка (Hue Shifting)",
        ["Dlg_RampBaseColor"] = "Базовый цвет:",
        ["Dlg_RampUsePrimary"] = "Взять цвет палитры",
        ["Dlg_RampUsePrimaryTip"] = "Взять текущий основной цвет из рабочей палитры",
        ["Dlg_RampPreset"] = "Стиль рампы:",
        ["Dlg_RampPreset_Sunlight"] = "Солнечный свет (Холодная тень ➔ Теплый свет)",
        ["Dlg_RampPreset_Fire"] = "Огонь и лава (Бордовый ➔ Оранжевый ➔ Желтый)",
        ["Dlg_RampPreset_Ice"] = "Лед и холод (Глубокий синий ➔ Циан ➔ Белый)",
        ["Dlg_RampPreset_Toxic"] = "Токсик (Изумрудный ➔ Кислотный лайм)",
        ["Dlg_RampPreset_Gold"] = "Золото и металл (Умбра ➔ Охра ➔ Золотистый)",
        ["Dlg_RampPreset_Custom"] = "Пользовательский (Ручной сдвиг оттенка)",
        ["Dlg_RampSteps"] = "Количество цветов:",
        ["Dlg_RampHueShift"] = "Сдвиг тона (Hue Shift):",
        ["Dlg_RampPreview"] = "ПРЕДПРОСМОТР РАМПЫ",
        ["Dlg_RampApplyToPalette"] = "Добавить в палитру",
        ["Dlg_RampApplyToGradient"] = "Применить в градиент",
        ["Dlg_RampStatus"] = "{0} цветов (Тень ➔ Свет)",
        ["Dlg_RampAdded"] = "Добавлено в палитру!",
        ["Dlg_RampClose"] = "Закрыть",

        // Color Picker & Palettes View
        ["Color_Lmb"] = "ЛКМ",
        ["Color_Rmb"] = "ПКМ",
        ["Color_PrimaryTip"] = "[ЛКМ] Основной цвет — нажмите для выбора",
        ["Color_SecondaryTip"] = "[ПКМ] Вторичный цвет — нажмите для выбора",
        ["Color_Hex"] = "HEX",
        ["Color_AddTip"] = "Добавить цвет в палитру",
        ["Color_TabWheel"] = "Круг",
        ["Color_TabPalette"] = "Палитра",
        ["Color_TabSliders"] = "RGB Слайдеры",
        ["Color_Swatches"] = "ОБРАЗЦЫ ПАЛИТРЫ",
        ["Color_Count"] = "{0} цветов",

        // Dialog: About
        ["Dlg_AboutTitle"] = "О программе — PixelForge v{0}",
        ["Dlg_AboutDescription"] = "Современный и быстрый редактор пиксель-арта на .NET 8 и Avalonia UI с поддержкой SkiaSharp, слоев, симметрии и полупрозрачных референсов.",
        ["Dlg_AboutCopyright"] = "Copyright © 2026 widlily-corp. Лицензия Apache-2.0.",

        // Splash Window Subsystems
        ["Splash_StageRam"] = "Выделение рабочего набора ОЗУ",
        ["Splash_StageRamDetail"] = "Настройка высокоприоритетного пула памяти (1024 МБ)...",
        ["Splash_StageLock"] = "Блокировка сборок в ОЗУ",
        ["Splash_StageLockDetail"] = "Предзагрузка страниц памяти для мгновенного отклика...",
        ["Splash_StageJit"] = "Pre-JIT компиляция горячих путей",
        ["Splash_StageJitDetail"] = "Компиляция методов холста, истории и инструментов...",
        ["Splash_StageSkia"] = "Прогрев графического пайплайна SkiaSharp",
        ["Splash_StageSkiaDetail"] = "Инициализация внеэкранных поверхностей и растеризаторов...",
        ["Splash_StageServices"] = "Построение матрицы сервисов",
        ["Splash_StageServicesDetail"] = "Разрешение контейнера внедрения зависимостей...",
        ["Splash_StageStudio"] = "Подготовка рабочей студии",
        ["Splash_StageStudioDetail"] = "Финализация холста, палитр и слоев...",
        ["Splash_Feature1"] = "Аппаратный SkiaSharp конвейер отрисовки (60+ FPS)",
        ["Splash_Feature2"] = "Полная загрузка рабочего набора в ОЗУ (Zero-latency)",
        ["Splash_Feature3"] = "Разреженная дельта-история Undo/Redo и поддержка слоёв",
        ["Splash_TelemetryRam"] = "ОЗУ пул 1024 МБ",
        ["Splash_TelemetryJit"] = "Pre-JIT компиляция",
        ["Splash_TelemetrySkia"] = "SkiaSharp 60+ FPS",
        ["Splash_TelemetryUndo"] = "Zero-Alloc дельта",
        ["Splash_TelemetryOnline"] = "В СЕТИ",
        ["Splash_SystemTag"] = "x64 • .NET 8 • Skia 2.88",

        // Dither Patterns
        ["Dither_None"] = "Без дизеринга",
        ["Dither_Checker50"] = "Шахматка 50%",
        ["Dither_Bayer2x2"] = "Байер 2×2",
        ["Dither_Bayer4x4"] = "Байер 4×4",
        ["Dither_Bayer8x8"] = "Байер 8×8",
        ["Dither_Dots25"] = "Точки 25%",
        ["Dither_Dots75"] = "Точки 75%",
        ["Dither_HorizontalLines"] = "Горизонтальные линии",
        ["Dither_VerticalLines"] = "Вертикальные линии",
        ["Dither_DiagonalLines"] = "Диагональные линии"
    };

    // ==========================================
    // ENGLISH DICTIONARY
    // ==========================================
    private static readonly Dictionary<string, string> StringsEn = new(StringComparer.OrdinalIgnoreCase)
    {
        // Window & Title
        ["App_Title"] = "PixelForge",
        ["App_Untitled"] = "Untitled",
        ["App_Subtitle"] = "PROFESSIONAL PIXEL ART STUDIO",

        // Top Menu: File
        ["Menu_File"] = "File",
        ["Menu_New"] = "New Canvas...",
        ["Menu_Open"] = "Open Project...",
        ["Menu_RecentFiles"] = "Recent Projects",
        ["Menu_NoRecentFiles"] = "No Recent Projects",
        ["Menu_ClearRecentFiles"] = "Clear Recent Projects",
        ["Menu_Save"] = "Save",
        ["Menu_SaveAs"] = "Save As...",
        ["Menu_ExportPng"] = "Export to PNG...",
        ["Menu_ExportBmp"] = "Export to BMP...",
        ["Menu_AssociateFiles"] = "Associate .pxproj files with PixelForge",
        ["Menu_Exit"] = "Exit",

        // Top Menu: Edit
        ["Menu_Edit"] = "Edit",
        ["Menu_Undo"] = "Undo",
        ["Menu_Redo"] = "Redo",
        ["Menu_Cut"] = "Cut",
        ["Menu_Copy"] = "Copy",
        ["Menu_Paste"] = "Paste",
        ["Menu_SelectAll"] = "Select All",
        ["Menu_Deselect"] = "Deselect",
        ["Menu_DeleteSelection"] = "Delete Selection",

        // Top Menu: Canvas
        ["Menu_Canvas"] = "Canvas",
        ["Menu_ResizeCanvas"] = "Canvas Size...",
        ["Menu_LayerTransform"] = "Layer Transform",
        ["Menu_FlipLayerH"] = "Flip Layer Horizontally (H)",
        ["Menu_FlipLayerV"] = "Flip Layer Vertically (V)",
        ["Menu_RotateLayerCW"] = "Rotate Layer 90° CW",
        ["Menu_RotateLayerCCW"] = "Rotate Layer 90° CCW",
        ["Menu_RotateLayer180"] = "Rotate Layer 180°",
        ["Menu_CanvasTransform"] = "Canvas Transform",
        ["Menu_FlipCanvasH"] = "Flip Canvas Horizontally",
        ["Menu_FlipCanvasV"] = "Flip Canvas Vertically",
        ["Menu_RotateCanvasCW"] = "Rotate Canvas 90° CW",
        ["Menu_RotateCanvasCCW"] = "Rotate Canvas 90° CCW",
        ["Menu_RotateCanvas180"] = "Rotate Canvas 180°",
        ["Menu_ClearActiveLayer"] = "Clear Active Layer",
        ["Menu_SmartTools"] = "Smart Tools",
        ["Menu_AddOutline"] = "Add Outline...",
        ["Menu_AddShadow"] = "Add Drop Shadow...",
        ["Menu_ReplaceColor"] = "Replace Color...",
        ["Menu_ColorRamp"] = "Color Ramp Generator...",
        ["Menu_TiledMode"] = "Tiled Mode (Seamless)",
        ["Menu_ReferenceImage"] = "Reference Image",
        ["Menu_LoadReference"] = "Load Reference Image...",
        ["Menu_ToggleReference"] = "Toggle Reference Visibility",
        ["Menu_ClearReference"] = "Clear Reference Image",

        // Top Menu: View
        ["Menu_View"] = "View",
        ["Menu_Grid"] = "Pixel Grid",
        ["Menu_TileGrid"] = "Tile Grid",
        ["Menu_SymmetryX"] = "Horizontal Symmetry (X)",
        ["Menu_SymmetryY"] = "Vertical Symmetry (Y)",
        ["Menu_ZoomIn"] = "Zoom In",
        ["Menu_ZoomOut"] = "Zoom Out",
        ["Menu_ResetZoom"] = "Reset Zoom (100%)",
        ["Menu_ToggleTheme"] = "Toggle Theme (Dark/Light)",
        ["Menu_Language"] = "Language (Язык)",
        ["Menu_LangRu"] = "Russian (Русский)",
        ["Menu_LangEn"] = "English (Английский)",

        // Top Menu: Help
        ["Menu_Help"] = "Help",
        ["Menu_Hotkeys"] = "Keyboard Shortcuts... (F1)",
        ["Menu_CheckUpdates"] = "Check for Updates...",
        ["Menu_About"] = "About PixelForge...",

        // Quick Toolbar Tooltips
        ["Tip_NewCanvas"] = "New Canvas (Ctrl+N)",
        ["Tip_ResizeCanvas"] = "Change Canvas Size (Ctrl+Alt+C)",
        ["Tip_OpenProject"] = "Open Project (Ctrl+O)",
        ["Tip_SaveProject"] = "Save Project (Ctrl+S)",
        ["Tip_QuickExport"] = "Quick Export (PNG / BMP)",
        ["Tip_FlipH"] = "Flip active layer horizontally (Shift+H)",
        ["Tip_FlipV"] = "Flip active layer vertically (V)",
        ["Tip_RotateCW"] = "Rotate active layer 90° CW",
        ["Tip_SmartTools"] = "Smart Tools (FX) & Effects",
        ["Tip_Undo"] = "Undo (Ctrl+Z)",
        ["Tip_Redo"] = "Redo (Ctrl+Y)",
        ["Tip_Grid"] = "Pixel Grid (Ctrl+G)",
        ["Tip_TileGrid"] = "Tile Grid (Ctrl+Shift+G)",
        ["Tip_SymX"] = "Symmetry X (Horizontal Mirror)",
        ["Tip_SymY"] = "Symmetry Y (Vertical Mirror)",
        ["Tip_Tiled"] = "Seamless Tiled Mode (F10)",
        ["Tip_ZoomOut"] = "Zoom Out (Ctrl+-)",
        ["Tip_ZoomIn"] = "Zoom In (Ctrl++)",
        ["Tip_ZoomFit"] = "100% Zoom",
        ["Tip_ToggleTheme"] = "Toggle Theme",
        ["Tip_ToggleLanguage"] = "Switch Interface Language (RU / EN)",

        // Toolbar Button Labels
        ["Label_Grid"] = "Grid",
        ["Label_TileGrid"] = "Tile Grid",
        ["Label_SymX"] = "Symm. X",
        ["Label_SymY"] = "Symm. Y",
        ["Label_Tiled"] = "Tiles",

        // Update Banner
        ["Banner_UpdateNow"] = "Update Now",
        ["Banner_Website"] = "Website",
        ["Banner_UpdateTooltip"] = "Automatically download, install, and restart PixelForge",

        // Tool Strip Sections
        ["ToolSection_Drawing"] = "DRAWING",
        ["ToolSection_DrawingTip"] = "Freehand drawing tools",
        ["ToolSection_Fill"] = "FILL & COLOR",
        ["ToolSection_FillTip"] = "Filling and color sampling tools",
        ["ToolSection_Shapes"] = "SHAPES",
        ["ToolSection_ShapesTip"] = "Geometric shape tools",
        ["ToolSection_Select"] = "SELECTION",
        ["ToolSection_SelectTip"] = "Region selection tools",

        // Tool Names & Tooltips
        ["Tool_Pencil"] = "Pencil",
        ["Tool_PencilTip"] = "Pencil (B)",
        ["Tool_Eraser"] = "Eraser",
        ["Tool_EraserTip"] = "Eraser (E)",
        ["Tool_DitherBrush"] = "Dither Brush",
        ["Tool_DitherBrushTip"] = "Dither Brush (D)",
        ["Tool_PatternBrush"] = "Pattern Brush 8x8",
        ["Tool_PatternBrushTip"] = "8x8 Pattern Brush (P)",
        ["Tool_ShadeBrush"] = "Shade Brush",
        ["Tool_ShadeBrushTip"] = "Shade Brush (H) - Left-click darker, Right-click lighter",
        ["Tool_SprayBrush"] = "Spray / Noise Brush",
        ["Tool_SprayBrushTip"] = "Spray / Noise Brush (A)",
        ["Tool_Fill"] = "Paint Bucket",
        ["Tool_FillTip"] = "Paint Bucket (G)",
        ["Tool_MagicWand"] = "Magic Wand",
        ["Tool_MagicWandTip"] = "Magic Wand (W)",
        ["Tool_Gradient"] = "Gradient",
        ["Tool_GradientTip"] = "Multi-Stop Gradient (T)",
        ["Tool_Eyedropper"] = "Eyedropper",
        ["Tool_EyedropperTip"] = "Eyedropper (I)",
        ["Tool_Line"] = "Line",
        ["Tool_LineTip"] = "Line (L)",
        ["Tool_Rectangle"] = "Rectangle",
        ["Tool_RectangleTip"] = "Rectangle (R / U)",
        ["Tool_Ellipse"] = "Ellipse",
        ["Tool_EllipseTip"] = "Ellipse (C)",
        ["Tool_Selection"] = "Selection",
        ["Tool_SelectionTip"] = "Selection (S / M)",

        // Brush Size Widget
        ["Brush_Header"] = "BRUSH",
        ["Brush_SizeCurrentTip"] = "Current brush size (1–32 px)",
        ["Brush_DecreaseTip"] = "Decrease brush size ([)",
        ["Brush_IncreaseTip"] = "Increase brush size (])",

        // Context Bar
        ["Context_Size"] = "Size:",
        ["Context_BrushTip1"] = "1 px Brush",
        ["Context_BrushTip2"] = "2 px Brush",
        ["Context_BrushTip3"] = "3 px Brush",
        ["Context_BrushTip4"] = "4 px Brush",
        ["Context_BrushTip8"] = "8 px Brush",
        ["Context_PixelPerfect"] = "Pixel-Perfect",
        ["Context_PixelPerfectTip"] = "Pixel-Perfect: eliminates jagged edges in 1px lines (Shift+P)",
        ["Context_TileGrid"] = "Tile Grid",
        ["Context_TileSize"] = "Tile Size",
        ["Context_SprayRadius"] = "Radius:",
        ["Context_SprayDensity"] = "Density:",
        ["Context_Stabilizer"] = "Stabilizer:",
        ["Context_Texture8x8"] = "8x8 Texture:",
        ["Context_Dither"] = "Dithering:",
        ["Context_Tolerance"] = "Tolerance:",
        ["Context_Contiguous"] = "Contiguous",
        ["Context_ContiguousTip"] = "Fill only contiguous connected pixels or all matching colors",
        ["Context_Gradient"] = "Gradient:",
        ["Context_Preset"] = "Preset:",
        ["Context_PresetTip"] = "Multi-stop gradient presets and color ramps",
        ["Context_Ramp"] = "Ramp...",
        ["Context_RampTip"] = "Color ramp generator with Hue Shifting (from shadow to light)",
        ["Context_ShapeFill"] = "Fill Shape",
        ["Context_Zoom"] = "Zoom:",

        // Canvas Floating HUD
        ["Hud_Lmb"] = "LMB",
        ["Hud_Draw"] = "Draw",
        ["Hud_Rmb"] = "RMB",
        ["Hud_EraseSecondary"] = "Erase / Secondary",
        ["Hud_SpaceDrag"] = "Space+Drag",
        ["Hud_Pan"] = "Pan Canvas",
        ["Hud_CtrlWheel"] = "Ctrl+Wheel",
        ["Hud_Zoom"] = "Zoom",

        // Right Inspector: Live Preview
        ["Panel_LivePreview"] = "LIVE PREVIEW",
        ["Panel_TogglePreview"] = "Collapse / expand preview",
        ["Preview_Scale"] = "Scale:",

        // Right Inspector: Color Picker & Palette
        ["Panel_ColorPicker"] = "PALETTE & COLOR",
        ["Color_Primary"] = "Primary",
        ["Color_Secondary"] = "Secondary",
        ["Color_SwapTip"] = "Swap Colors (X)",
        ["Color_AddColorTip"] = "Add current color to active palette",
        ["Color_RemoveColorTip"] = "Remove selected color from palette",
        ["Color_PresetsTip"] = "Select preset palette (DawnBringer, PICO-8, etc.)",

        // Right Inspector: Layers
        ["Panel_Layers"] = "LAYERS",
        ["Layer_AddTip"] = "Add New Layer",
        ["Layer_DuplicateTip"] = "Duplicate Layer",
        ["Layer_MoveUpTip"] = "Move Layer Up",
        ["Layer_MoveDownTip"] = "Move Layer Down",
        ["Layer_MergeDownTip"] = "Merge Down",
        ["Layer_DeleteTip"] = "Delete Layer",
        ["Layer_Opacity"] = "Opacity",
        ["Layer_VisibilityTip"] = "Toggle Layer Visibility",
        ["Layer_AlphaLockTip"] = "Lock Transparent Pixels (Alpha Lock)",

        // Right Inspector: Reference Image Overlay
        ["Panel_Reference"] = "REFERENCE",
        ["Ref_Load"] = "Load...",
        ["Ref_LoadTip"] = "Load reference image (PNG, JPG, BMP)",
        ["Ref_Clear"] = "Clear",
        ["Ref_ClearTip"] = "Clear reference overlay from canvas",
        ["Ref_Opacity"] = "Opacity:",
        ["Ref_VisibilityTip"] = "Show or hide reference overlay",
        ["Ref_FitCanvas"] = "Fit to Canvas",

        // Status Bar
        ["Status_Ready"] = "Ready",
        ["Status_DimensionsTip"] = "Canvas dimensions — click to resize (Ctrl+Alt+C)",
        ["Status_TileGridTip"] = "Tile Grid (click to cycle size)",
        ["Status_Zoom"] = "Zoom:",
        ["Status_ZoomTip"] = "Zoom: {0}% — Click to cycle, Ctrl+1..4 for presets",
        ["Status_LangButton"] = "EN 🇬🇧",
        ["Status_LangButtonTip"] = "Switch language (Current: English) / Сменить язык",
        ["Status_Selection"] = "Selection: {0}",
        ["Status_SelectionTip"] = "Selection area: {0} — Click to deselect (Ctrl+D)",
        ["Status_TiledActiveTip"] = "Seamless 3x3 Tiled Mode active (F10)",
        ["Status_SymmetryTip"] = "Symmetry: {0} (Shift+X / Shift+Y)",
        ["Status_PixelPerfectTip"] = "Pixel-Perfect mode active (Shift+P)",
        ["Status_AlphaLockTip"] = "Alpha Lock active on current layer",
        ["Status_ColorUnderCursorTip"] = "Color under cursor: {0}",

        // Dynamic Status Messages
        ["Msg_LoadedProject"] = "Loaded project {0}",
        ["Msg_SavedProject"] = "Saved to {0}",
        ["Msg_NewCanvas"] = "New canvas ({0}×{1})",
        ["Msg_Autosaved"] = "Autosaved at {0:HH:mm:ss}",
        ["Msg_AutosaveError"] = "Autosave error: {0}",
        ["Msg_ResizeSuccess"] = "Canvas resized to {0} × {1} px",
        ["Msg_LayerFlippedH"] = "{0} flipped horizontally",
        ["Msg_LayerFlippedV"] = "{0} flipped vertically",
        ["Msg_CanvasFlippedH"] = "Canvas flipped horizontally",
        ["Msg_CanvasFlippedV"] = "Canvas flipped vertically",
        ["Msg_LayerRotated"] = "{0} rotated {1}",
        ["Msg_CanvasRotated"] = "Canvas rotated {1}",
        ["Msg_LayerCleared"] = "Layer '{0}' cleared",
        ["Msg_ReferenceLoaded"] = "Reference image loaded: {0}",
        ["Msg_ReferenceCleared"] = "Reference image cleared",
        ["Msg_AssociationDone"] = ".pxproj association registered in Windows successfully!",
        ["Msg_SelectAll"] = "Selected all (Ctrl+A)",
        ["Msg_Deselect"] = "Selection cleared (Ctrl+D)",
        ["Msg_DeleteSelection"] = "Selection deleted",
        ["Msg_LayerAdded"] = "Created layer '{0}' (Ctrl+Shift+N)",
        ["Msg_LayerDuplicated"] = "Duplicated layer '{0}' (Ctrl+J)",
        ["Msg_LayerRemoved"] = "Removed layer '{0}'",
        ["Msg_LayerMergedDown"] = "Merged layer '{0}' down (Ctrl+E)",
        ["Msg_LayerVisibility"] = "Layer '{0}' is now {1} (Shift+L)",
        ["Msg_LayerAlphaLock"] = "Alpha Lock for '{0}': {1}",
        ["Msg_ColorsSwapped"] = "Colors swapped (X)",
        ["Msg_EyedropperPicked"] = "Eyedropper: picked {0}",
        ["Msg_ColorAdded"] = "Added {0} to palette",
        ["Msg_PaletteNext"] = "Selected palette color {0} (key .)",
        ["Msg_PalettePrev"] = "Selected palette color {0} (key ,)",
        ["Msg_BrushSize"] = "Brush size: {0} px",
        ["Msg_ZoomSet"] = "Zoom: {0}%",
        ["Text_Enabled"] = "enabled",
        ["Text_Disabled"] = "disabled",
        ["Text_Visible"] = "visible",
        ["Text_Hidden"] = "hidden",
        ["Dlg_Hotkeys_ZoomPresets"] = "Quick Zoom Presets (100%..800%)",
        ["Dlg_Hotkeys_LayerNew"] = "Create New Layer",
        ["Dlg_Hotkeys_LayerMerge"] = "Merge Layer Down",
        ["Dlg_Hotkeys_LayerDuplicate"] = "Duplicate Active Layer",
        ["Dlg_Hotkeys_LayerVisibility"] = "Toggle Layer Visibility",
        ["Dlg_Hotkeys_SymmetryX"] = "Toggle Symmetry X",
        ["Dlg_Hotkeys_SymmetryY"] = "Toggle Symmetry Y",
        ["Dlg_Hotkeys_PaletteCycle"] = "Cycle Palette Color (, / .)",
        ["Dlg_Hotkeys_BrushPresets"] = "Brush Size Presets (keys 1..8)",
        ["Dlg_Hotkeys_CycleTileGrid"] = "Cycle Tile Grid Size (Alt+G)",

        // Dialog: New Canvas
        ["Dlg_NewCanvasTitle"] = "New Canvas — PixelForge",
        ["Dlg_NewCanvasHeader"] = "New Canvas",
        ["Dlg_NewCanvasSubheader"] = "Choose a preset or enter a custom size",
        ["Dlg_NewCanvasQuickPresets"] = "QUICK PRESETS",
        ["Dlg_NewCanvasIcon"] = "Icon",
        ["Dlg_NewCanvasSprite"] = "Sprite",
        ["Dlg_NewCanvasDetailed"] = "Detailed Art",
        ["Dlg_NewCanvasCustomSize"] = "CUSTOM SIZE (1–4096 px)",
        ["Dlg_NewCanvasWidth"] = "Width (px)",
        ["Dlg_NewCanvasHeight"] = "Height (px)",
        ["Dlg_NewCanvasCreate"] = "Create Canvas",
        ["Dlg_NewCanvasCancel"] = "Cancel",
        ["Dlg_NewCanvasError"] = "Canvas dimensions must be between 1 and 4096 pixels.",

        // Dialog: Resize Canvas
        ["Dlg_ResizeTitle"] = "Canvas Size — PixelForge",
        ["Dlg_ResizeHeader"] = "Canvas Size",
        ["Dlg_ResizeCurrent"] = "Current size: {0} × {1} px",
        ["Dlg_ResizeDimensions"] = "NEW SIZE (1–4096 px)",
        ["Dlg_ResizeWidth"] = "Width (px)",
        ["Dlg_ResizeHeight"] = "Height (px)",
        ["Dlg_ResizeKeepAspect"] = "Maintain aspect ratio (1:1)",
        ["Dlg_ResizePresets"] = "QUICK PRESETS & SCALE",
        ["Dlg_ResizeMode"] = "RESIZE MODE",
        ["Dlg_ResizeAnchorMode"] = "Anchor Positioning",
        ["Dlg_ResizeScaleMode"] = "Scale Pixels (Nearest Neighbor)",
        ["Dlg_ResizeAnchorPoint"] = "Anchor Point:",
        ["Dlg_ResizeAnchor_TopLeft"] = "Top-Left (expands right and down)",
        ["Dlg_ResizeAnchor_TopCenter"] = "Top-Center (expands sides and down)",
        ["Dlg_ResizeAnchor_TopRight"] = "Top-Right (expands left and down)",
        ["Dlg_ResizeAnchor_MiddleLeft"] = "Middle-Left (expands right)",
        ["Dlg_ResizeAnchor_Center"] = "Center (expands equally in all directions)",
        ["Dlg_ResizeAnchor_MiddleRight"] = "Middle-Right (expands left)",
        ["Dlg_ResizeAnchor_BottomLeft"] = "Bottom-Left (expands right and up)",
        ["Dlg_ResizeAnchor_BottomCenter"] = "Bottom-Center (expands sides and up)",
        ["Dlg_ResizeAnchor_BottomRight"] = "Bottom-Right (expands left and up)",
        ["Dlg_ResizeApply"] = "Apply",
        ["Dlg_ResizeCancel"] = "Cancel",
        ["Dlg_ResizeError"] = "Canvas dimensions must be between 1 and 4096 pixels.",

        // Dialog: Hotkeys
        ["Dlg_HotkeysTitle"] = "PixelForge Hotkeys",
        ["Dlg_HotkeysHeader"] = "HOTKEYS REFERENCE",
        ["Dlg_HotkeysSubheader"] = "Keyboard shortcuts reference for maximum productivity",
        ["Dlg_HotkeysToolsTab"] = "TOOLS",
        ["Dlg_HotkeysCanvasTab"] = "DRAWING & CANVAS",
        ["Dlg_HotkeysNavTab"] = "NAVIGATION",
        ["Dlg_HotkeysProjectTab"] = "PROJECT & HISTORY",
        ["Dlg_Hotkeys_Pencil"] = "Pencil",
        ["Dlg_Hotkeys_Eraser"] = "Eraser",
        ["Dlg_Hotkeys_Dither"] = "Dither Brush",
        ["Dlg_Hotkeys_Pattern"] = "Pattern Brush",
        ["Dlg_Hotkeys_Shade"] = "Shade Brush",
        ["Dlg_Hotkeys_Spray"] = "Spray / Noise Brush",
        ["Dlg_Hotkeys_Fill"] = "Fill",
        ["Dlg_Hotkeys_Wand"] = "Magic Wand",
        ["Dlg_Hotkeys_Gradient"] = "Gradient",
        ["Dlg_Hotkeys_Eyedropper"] = "Eyedropper",
        ["Dlg_Hotkeys_Line"] = "Line",
        ["Dlg_Hotkeys_Rect"] = "Rectangle",
        ["Dlg_Hotkeys_Ellipse"] = "Ellipse",
        ["Dlg_Hotkeys_Selection"] = "Selection",
        ["Dlg_Hotkeys_PixelPerfect"] = "Pixel-Perfect Mode",
        ["Dlg_Hotkeys_TileGrid"] = "Tile Grid",
        ["Dlg_Hotkeys_Tiled"] = "Seamless Tiled Mode",
        ["Dlg_Hotkeys_SwapColors"] = "Swap Colors",
        ["Dlg_Hotkeys_BrushSize"] = "Brush Size",
        ["Dlg_Hotkeys_CanvasSize"] = "Canvas Size",
        ["Dlg_Hotkeys_Grid"] = "Pixel Grid",
        ["Dlg_Hotkeys_Pan"] = "Pan Canvas",
        ["Dlg_Hotkeys_Zoom"] = "Zoom",
        ["Dlg_Hotkeys_Undo"] = "Undo",
        ["Dlg_Hotkeys_Redo"] = "Redo",
        ["Dlg_Hotkeys_Save"] = "Save",
        ["Dlg_Hotkeys_Open"] = "Open",
        ["Dlg_Hotkeys_New"] = "New Canvas",
        ["Dlg_Hotkeys_EscNotice"] = "Press Esc to close",
        ["Dlg_HotkeysClose"] = "Got it / Close",

        // Dialog: Color Ramp
        ["Dlg_RampTitle"] = "Color Ramp Generator — PixelForge",
        ["Dlg_RampHeader"] = "COLOR RAMP GENERATOR",
        ["Dlg_RampSubheader"] = "Artistic gradient generation with Hue Shifting",
        ["Dlg_RampBaseColor"] = "Base Color:",
        ["Dlg_RampUsePrimary"] = "Pick Palette Color",
        ["Dlg_RampUsePrimaryTip"] = "Pick current primary color from active palette",
        ["Dlg_RampPreset"] = "Ramp Style:",
        ["Dlg_RampPreset_Sunlight"] = "Sunlight (Cool Shadow ➔ Warm Light)",
        ["Dlg_RampPreset_Fire"] = "Fire & Lava (Maroon ➔ Orange ➔ Yellow)",
        ["Dlg_RampPreset_Ice"] = "Ice & Frost (Deep Blue ➔ Cyan ➔ White)",
        ["Dlg_RampPreset_Toxic"] = "Toxic (Emerald ➔ Acid Lime)",
        ["Dlg_RampPreset_Gold"] = "Gold & Metal (Umber ➔ Ochre ➔ Golden)",
        ["Dlg_RampPreset_Custom"] = "Custom (Manual Hue Shift)",
        ["Dlg_RampSteps"] = "Color count:",
        ["Dlg_RampHueShift"] = "Hue Shift:",
        ["Dlg_RampPreview"] = "RAMP PREVIEW",
        ["Dlg_RampApplyToPalette"] = "Add to Palette",
        ["Dlg_RampApplyToGradient"] = "Apply to Gradient",
        ["Dlg_RampStatus"] = "{0} colors (Shadow ➔ Light)",
        ["Dlg_RampAdded"] = "Added to palette!",
        ["Dlg_RampClose"] = "Close",

        // Color Picker & Palettes View
        ["Color_Lmb"] = "LMB",
        ["Color_Rmb"] = "RMB",
        ["Color_PrimaryTip"] = "[LMB] Primary color — click to select",
        ["Color_SecondaryTip"] = "[RMB] Secondary color — click to select",
        ["Color_Hex"] = "HEX",
        ["Color_AddTip"] = "Add color to palette",
        ["Color_TabWheel"] = "Wheel",
        ["Color_TabPalette"] = "Palette",
        ["Color_TabSliders"] = "RGB Sliders",
        ["Color_Swatches"] = "PALETTE SWATCHES",
        ["Color_Count"] = "{0} colors",

        // Dialog: About
        ["Dlg_AboutTitle"] = "About — PixelForge v{0}",
        ["Dlg_AboutDescription"] = "Modern, fast pixel art editor built with .NET 8, Avalonia UI, and SkiaSharp with layers, symmetry, and reference overlays.",
        ["Dlg_AboutCopyright"] = "Copyright © 2026 widlily-corp. Licensed under Apache-2.0.",

        // Splash Window Subsystems
        ["Splash_StageRam"] = "Allocating RAM Working Set",
        ["Splash_StageRamDetail"] = "Configuring high-priority memory pool (1024 MB)...",
        ["Splash_StageLock"] = "Locking Assemblies into RAM",
        ["Splash_StageLockDetail"] = "Pre-faulting memory pages for zero disk-paging latency...",
        ["Splash_StageJit"] = "Pre-JIT Compiling Hot Paths",
        ["Splash_StageJitDetail"] = "Compiling canvas methods, history, and tools...",
        ["Splash_StageSkia"] = "Warming Up SkiaSharp Graphics",
        ["Splash_StageSkiaDetail"] = "Initializing off-screen surfaces and rasterizer pipelines...",
        ["Splash_StageServices"] = "Building Service Matrix",
        ["Splash_StageServicesDetail"] = "Resolving dependency injection container...",
        ["Splash_StageStudio"] = "Preparing Studio Workspace",
        ["Splash_StageStudioDetail"] = "Finalizing canvas, palettes, and layer composite...",
        ["Splash_Feature1"] = "Hardware-Accelerated 60+ FPS SkiaSharp Render Pipeline",
        ["Splash_Feature2"] = "Full RAM Working Set resident execution (Zero disk-paging latency)",
        ["Splash_Feature3"] = "Sparse delta Undo/Redo history and multi-layer compositing",
        ["Splash_TelemetryRam"] = "RAM Pool 1024 MB",
        ["Splash_TelemetryJit"] = "Pre-JIT Engine",
        ["Splash_TelemetrySkia"] = "SkiaSharp 60+ FPS",
        ["Splash_TelemetryUndo"] = "Zero-Alloc Delta",
        ["Splash_TelemetryOnline"] = "ONLINE",
        ["Splash_SystemTag"] = "x64 • .NET 8 • Skia 2.88",

        // Dither Patterns
        ["Dither_None"] = "No Dithering",
        ["Dither_Checker50"] = "Checkerboard 50%",
        ["Dither_Bayer2x2"] = "Bayer 2×2",
        ["Dither_Bayer4x4"] = "Bayer 4×4",
        ["Dither_Bayer8x8"] = "Bayer 8×8",
        ["Dither_Dots25"] = "Sparse Dots 25%",
        ["Dither_Dots75"] = "Dense Dots 75%",
        ["Dither_HorizontalLines"] = "Horizontal Lines",
        ["Dither_VerticalLines"] = "Vertical Lines",
        ["Dither_DiagonalLines"] = "Diagonal Lines"
    };
}
