using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using PixelForge.App.Services;
using Xunit;

namespace PixelForge.App.Tests;

public class ThemeStylingTests
{
    private static readonly XNamespace AvaloniaNs = "https://github.com/avaloniaui";
    private static readonly XNamespace XNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string GetSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PixelForge.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new DirectoryNotFoundException("PixelForge.sln directory not found from " + AppContext.BaseDirectory);
    }

    private static string GetSourcePath(string relativePath)
    {
        var root = GetSolutionRoot();
        return Path.Combine(root, relativePath);
    }

    private static XDocument LoadAxaml(string relativePath)
    {
        var fullPath = GetSourcePath(relativePath);
        File.Exists(fullPath).Should().BeTrue($"File {relativePath} should exist at {fullPath}");
        return XDocument.Load(fullPath);
    }

    private static readonly string[] RequiredSemanticColorKeys =
    [
        "SurfaceBaseColor",
        "SurfacePanelColor",
        "SurfaceCardColor",
        "SurfaceCardHoverColor",
        "SurfaceCardSelectedColor",
        "SurfaceInputColor",
        "SurfaceBadgeColor",
        "SurfaceChipColor",
        "BorderSubtleColor",
        "BorderStrongColor",
        "TextPrimaryColor",
        "TextSecondaryColor",
        "TextMutedColor",
        "TextBadgeColor",
        "AccentPrimaryColor",
        "AccentPrimaryHoverColor",
        "AccentSecondaryColor",
        "AccentSuccessColor",
        "AccentDangerColor",
        "AccentWarningColor",
        "AccentGlowColor",
        "CheckerLightColor",
        "CheckerDarkColor"
    ];

    private static readonly string[] RequiredSemanticBrushKeys =
    [
        "SurfaceBaseBrush",
        "SurfacePanelBrush",
        "SurfaceCardBrush",
        "SurfaceCardHoverBrush",
        "SurfaceCardSelectedBrush",
        "SurfaceInputBrush",
        "SurfaceBadgeBrush",
        "SurfaceChipBrush",
        "BorderSubtleBrush",
        "BorderStrongBrush",
        "TextPrimaryBrush",
        "TextSecondaryBrush",
        "TextMutedBrush",
        "TextBadgeBrush",
        "AccentPrimaryBrush",
        "AccentPrimaryHoverBrush",
        "AccentSecondaryBrush",
        "AccentSuccessBrush",
        "AccentDangerBrush",
        "AccentWarningBrush",
        "AccentGlowBrush",
        "CheckerLightBrush",
        "CheckerDarkBrush"
    ];

    [Theory]
    [InlineData("src/PixelForge.App/Styles/DarkTheme.axaml")]
    [InlineData("src/PixelForge.App/Styles/LightTheme.axaml")]
    public void ThemeDictionary_ContainsAllRequiredSemanticColorsAndBrushes(string themePath)
    {
        // Arrange
        var doc = LoadAxaml(themePath);
        var root = doc.Root;
        root.Should().NotBeNull();
        root!.Name.LocalName.Should().Be("ResourceDictionary");

        var colors = root.Elements(AvaloniaNs + "Color")
            .Select(c => c.Attribute(XNs + "Key")?.Value)
            .Where(k => !string.IsNullOrEmpty(k))
            .ToHashSet();

        var brushes = root.Elements(AvaloniaNs + "SolidColorBrush")
            .Select(b => b.Attribute(XNs + "Key")?.Value)
            .Where(k => !string.IsNullOrEmpty(k))
            .ToHashSet();

        // Assert
        foreach (var requiredColor in RequiredSemanticColorKeys)
        {
            colors.Should().Contain(requiredColor, $"Theme {themePath} must contain Color key '{requiredColor}'");
        }

        foreach (var requiredBrush in RequiredSemanticBrushKeys)
        {
            brushes.Should().Contain(requiredBrush, $"Theme {themePath} must contain SolidColorBrush key '{requiredBrush}'");
        }
    }

    [Theory]
    [InlineData("src/PixelForge.App/Styles/DarkTheme.axaml")]
    [InlineData("src/PixelForge.App/Styles/LightTheme.axaml")]
    public void ThemeColors_AreValidHexCodes(string themePath)
    {
        var doc = LoadAxaml(themePath);
        var hexRegex = new Regex(@"^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$");

        var colorElements = doc.Root!.Elements(AvaloniaNs + "Color");
        foreach (var colorEl in colorElements)
        {
            var key = colorEl.Attribute(XNs + "Key")?.Value ?? "unnamed";
            var val = colorEl.Value.Trim();
            hexRegex.IsMatch(val).Should().BeTrue($"Color key '{key}' in {themePath} has invalid hex '{val}'");
        }
    }

    [Fact]
    public void ThemeColors_ProvideSufficientContrast()
    {
        // Helper to parse hex to (R, G, B)
        static (double r, double g, double b) ParseHex(string hex)
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 8) hex = hex.Substring(2); // skip alpha
            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            return (r / 255.0, g / 255.0, b / 255.0);
        }

        // Relative luminance per WCAG
        static double GetLuminance((double r, double g, double b) c)
        {
            static double Linear(double v) => v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            return 0.2126 * Linear(c.r) + 0.7152 * Linear(c.g) + 0.0722 * Linear(c.b);
        }

        static double ContrastRatio(string hex1, string hex2)
        {
            double l1 = GetLuminance(ParseHex(hex1));
            double l2 = GetLuminance(ParseHex(hex2));
            double lighter = Math.Max(l1, l2);
            double darker = Math.Min(l1, l2);
            return (lighter + 0.05) / (darker + 0.05);
        }

        var darkDoc = LoadAxaml("src/PixelForge.App/Styles/DarkTheme.axaml");
        var darkColors = darkDoc.Root!.Elements(AvaloniaNs + "Color")
            .ToDictionary(e => e.Attribute(XNs + "Key")!.Value, e => e.Value.Trim());

        var lightDoc = LoadAxaml("src/PixelForge.App/Styles/LightTheme.axaml");
        var lightColors = lightDoc.Root!.Elements(AvaloniaNs + "Color")
            .ToDictionary(e => e.Attribute(XNs + "Key")!.Value, e => e.Value.Trim());

        // Dark theme: TextPrimary on SurfaceCard & SurfaceBase > 10:1
        ContrastRatio(darkColors["TextPrimaryColor"], darkColors["SurfaceCardColor"]).Should().BeGreaterThan(7.0);
        ContrastRatio(darkColors["TextSecondaryColor"], darkColors["SurfaceCardColor"]).Should().BeGreaterThan(4.5);
        ContrastRatio(darkColors["TextMutedColor"], darkColors["SurfaceCardColor"]).Should().BeGreaterThan(3.0);

        // Light theme: TextPrimary on SurfaceCard & SurfaceBase > 10:1
        ContrastRatio(lightColors["TextPrimaryColor"], lightColors["SurfaceCardColor"]).Should().BeGreaterThan(7.0);
        ContrastRatio(lightColors["TextSecondaryColor"], lightColors["SurfaceCardColor"]).Should().BeGreaterThan(4.5);
        ContrastRatio(lightColors["TextMutedColor"], lightColors["SurfaceCardColor"]).Should().BeGreaterThan(3.5);
    }

    [Fact]
    public void MainWindow_InspectorCardHeader_HasExplicitCheckedAndPointerOverStyles()
    {
        var doc = LoadAxaml("src/PixelForge.App/Views/MainWindow.axaml");
        var styles = doc.Descendants(AvaloniaNs + "Style")
            .Select(s => new
            {
                Selector = s.Attribute("Selector")?.Value ?? "",
                Setters = s.Elements(AvaloniaNs + "Setter")
                    .ToDictionary(set => set.Attribute("Property")?.Value ?? "", set => set.Attribute("Value")?.Value ?? "")
            })
            .ToList();

        // 1. Base style
        var baseStyle = styles.FirstOrDefault(s => s.Selector == "ToggleButton.inspector-card-header");
        baseStyle.Should().NotBeNull("ToggleButton.inspector-card-header style must exist");
        baseStyle!.Setters["Background"].Should().Be("Transparent");
        baseStyle.Setters["BorderThickness"].Should().Be("0");

        // 2. Checked style on control
        var checkedStyle = styles.FirstOrDefault(s => s.Selector == "ToggleButton.inspector-card-header:checked");
        checkedStyle.Should().NotBeNull("ToggleButton.inspector-card-header:checked style must exist");
        checkedStyle!.Setters["Background"].Should().Be("Transparent", "Inspector headers must not have blue background when checked");
        checkedStyle.Setters["BorderThickness"].Should().Be("0");

        // 3. Checked style targeting template ContentPresenter to prevent FluentTheme override
        var checkedTemplateStyle = styles.FirstOrDefault(s => s.Selector.Contains("ToggleButton.inspector-card-header:checked") && s.Selector.Contains("ContentPresenter#PART_ContentPresenter"));
        checkedTemplateStyle.Should().NotBeNull("Template ContentPresenter style for ToggleButton.inspector-card-header:checked must exist");
        checkedTemplateStyle!.Setters["Background"].Should().Be("Transparent");

        // 4. PointerOver styles
        var hoverStyle = styles.FirstOrDefault(s => s.Selector == "ToggleButton.inspector-card-header:pointerover");
        hoverStyle.Should().NotBeNull();
        hoverStyle!.Setters["Background"].Should().Be("{DynamicResource SurfaceCardHoverBrush}");

        var checkedHoverStyle = styles.FirstOrDefault(s => s.Selector == "ToggleButton.inspector-card-header:checked:pointerover");
        checkedHoverStyle.Should().NotBeNull();
        checkedHoverStyle!.Setters["Background"].Should().Be("{DynamicResource SurfaceCardHoverBrush}");
    }

    [Fact]
    public void MainWindow_ToolOptionToggleButtons_HaveAccentGlowAndPrimaryBorderCheckedStyle()
    {
        var doc = LoadAxaml("src/PixelForge.App/Views/MainWindow.axaml");
        var styles = doc.Descendants(AvaloniaNs + "Style")
            .Select(s => new
            {
                Selector = s.Attribute("Selector")?.Value ?? "",
                Setters = s.Elements(AvaloniaNs + "Setter")
                    .ToDictionary(set => set.Attribute("Property")?.Value ?? "", set => set.Attribute("Value")?.Value ?? "")
            })
            .ToList();

        // Check for context-toggle-btn or tool-opt-btn checked style
        var checkedOptStyle = styles.FirstOrDefault(s =>
            (s.Selector.Contains("ToggleButton.context-toggle-btn:checked") || s.Selector.Contains("ToggleButton.tool-opt-btn:checked"))
            && !s.Selector.Contains("ContentPresenter"));

        checkedOptStyle.Should().NotBeNull("Checked style for tool option / context toggle must exist");
        checkedOptStyle!.Setters["Background"].Should().Be("{DynamicResource AccentGlowBrush}");
        checkedOptStyle.Setters["BorderBrush"].Should().Be("{DynamicResource AccentPrimaryBrush}");

        // And template ContentPresenter
        var checkedOptTemplateStyle = styles.FirstOrDefault(s =>
            (s.Selector.Contains("ToggleButton.context-toggle-btn:checked") || s.Selector.Contains("ToggleButton.tool-opt-btn:checked"))
            && s.Selector.Contains("ContentPresenter#PART_ContentPresenter"));

        checkedOptTemplateStyle.Should().NotBeNull("Template ContentPresenter checked style for tool option toggle must exist");
        checkedOptTemplateStyle!.Setters["Background"].Should().Be("{DynamicResource AccentGlowBrush}");
        checkedOptTemplateStyle.Setters["BorderBrush"].Should().Be("{DynamicResource AccentPrimaryBrush}");
    }

    [Fact]
    public void MainWindow_AccordionPanels_UseInspectorCardHeaderClass()
    {
        var doc = LoadAxaml("src/PixelForge.App/Views/MainWindow.axaml");

        var toggles = doc.Descendants(AvaloniaNs + "ToggleButton")
            .Where(t => t.Attribute("Name") != null || t.Attribute(XNs + "Name") != null)
            .ToDictionary(
                t => t.Attribute("Name")?.Value ?? t.Attribute(XNs + "Name")!.Value,
                t => t.Attribute("Classes")?.Value ?? ""
            );

        toggles.Should().ContainKey("PreviewCardToggle");
        toggles["PreviewCardToggle"].Should().Contain("inspector-card-header");

        toggles.Should().ContainKey("PaletteCardToggle");
        toggles["PaletteCardToggle"].Should().Contain("inspector-card-header");

        toggles.Should().ContainKey("ReferenceCardToggle");
        toggles["ReferenceCardToggle"].Should().Contain("inspector-card-header");
    }

    [Fact]
    public void MainWindow_PixelPerfectToggle_HasToolOptionClasses()
    {
        var doc = LoadAxaml("src/PixelForge.App/Views/MainWindow.axaml");

        var pixelPerfectToggle = doc.Descendants(AvaloniaNs + "ToggleButton")
            .FirstOrDefault(t => (t.Attribute("ToolTip.Tip")?.Value ?? "").Contains("PixelPerfect") ||
                                 (t.Attribute("IsChecked")?.Value ?? "").Contains("IsPixelPerfect"));

        pixelPerfectToggle.Should().NotBeNull("Pixel-Perfect toggle button should exist in MainWindow.axaml");
        var classes = pixelPerfectToggle!.Attribute("Classes")?.Value ?? "";
        classes.Should().Contain("context-toggle-btn");
        classes.Should().Contain("tool-opt-btn");
    }

    [Fact]
    public void ThemeService_RuntimeThemeSwitching_TogglesStateAndFiresEvent()
    {
        var service = new ThemeService();
        service.IsDarkTheme.Should().BeTrue();

        bool eventFired = false;
        bool lastState = true;
        service.ThemeChanged += (s, isDark) =>
        {
            eventFired = true;
            lastState = isDark;
        };

        service.SetLightTheme();
        service.IsDarkTheme.Should().BeFalse();
        eventFired.Should().BeTrue();
        lastState.Should().BeFalse();

        eventFired = false;
        service.SetDarkTheme();
        service.IsDarkTheme.Should().BeTrue();
        eventFired.Should().BeTrue();
        lastState.Should().BeTrue();

        eventFired = false;
        service.ToggleTheme();
        service.IsDarkTheme.Should().BeFalse();
        eventFired.Should().BeTrue();
        lastState.Should().BeFalse();
    }
}
