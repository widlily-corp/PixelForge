using FluentAssertions;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using PixelForge.Core.Tools;
using SkiaSharp;
using Xunit;

namespace PixelForge.Core.Tests;

public class ShadeAndSprayToolTests
{
    private static ToolContext CreateContext(int width = 32, int height = 32)
    {
        var canvas = new PixelCanvas(width, height);
        var history = new UndoRedoService();
        return new ToolContext(canvas, canvas.ActiveLayer!, history, PixelColor.Red, PixelColor.Blue);
    }

    #region 1. ShadeBrushTool Tests

    [Fact]
    public void ShadeBrushTool_Metadata_PropertiesAreCorrect()
    {
        // Arrange
        var tool = new ShadeBrushTool();

        // Assert
        tool.Name.Should().Be("Shade Brush");
        tool.Tooltip.Should().Be("Shade / Tint Brush (Left-click darker, Right-click lighter)");
        tool.Shortcut.Should().Be("H");
        tool.IconKey.Should().Be("IconShadeBrush");
        tool.IsPrimary.Should().BeTrue();
        tool.GetPreviewPixels(CreateContext()).Should().BeEmpty();
    }

    [Fact]
    public void ShadeBrushTool_WithActiveColorRamp_ShiftsDarkerOnLeftClick()
    {
        // Arrange
        var ctx = CreateContext();
        var ramp = new[]
        {
            PixelColor.Black,
            PixelColor.DarkGray,
            PixelColor.Gray,
            PixelColor.LightGray,
            PixelColor.White
        };
        ctx.ActiveColorRamp = ramp;
        ctx.ActiveLayer.SetPixel(5, 5, PixelColor.Gray);

        var tool = new ShadeBrushTool { IsPrimary = true };

        // Act
        tool.OnPointerDown(ctx, 5, 5);
        tool.OnPointerUp(ctx, 5, 5);

        // Assert: Gray shifted one step darker along ramp is DarkGray
        ctx.ActiveLayer.GetPixel(5, 5).Should().Be(PixelColor.DarkGray);
    }

    [Fact]
    public void ShadeBrushTool_WithActiveColorRamp_ShiftsLighterOnRightClick()
    {
        // Arrange
        var ctx = CreateContext();
        var ramp = new[]
        {
            PixelColor.Black,
            PixelColor.DarkGray,
            PixelColor.Gray,
            PixelColor.LightGray,
            PixelColor.White
        };
        ctx.ActiveColorRamp = ramp;
        ctx.ActiveLayer.SetPixel(5, 5, PixelColor.Gray);

        var tool = new ShadeBrushTool();

        // Act: Right-click (isPrimary = false)
        tool.OnPointerDown(ctx, 5, 5, isPrimary: false);
        tool.OnPointerUp(ctx, 5, 5);

        // Assert: Gray shifted one step lighter along ramp is LightGray
        ctx.ActiveLayer.GetPixel(5, 5).Should().Be(PixelColor.LightGray);
    }

    [Fact]
    public void ShadeBrushTool_WithActiveColorRamp_BoundsClamp_DarkerDoesNotUnderflow()
    {
        // Arrange: pixel is already at the darkest end of ramp
        var ctx = CreateContext();
        var ramp = new[]
        {
            PixelColor.Black,
            PixelColor.DarkGray,
            PixelColor.Gray,
            PixelColor.LightGray,
            PixelColor.White
        };
        ctx.ActiveColorRamp = ramp;
        ctx.ActiveLayer.SetPixel(5, 5, PixelColor.Black);

        var tool = new ShadeBrushTool { IsPrimary = true };

        // Act
        tool.OnPointerDown(ctx, 5, 5);
        tool.OnPointerUp(ctx, 5, 5);

        // Assert: remains Black (clamped)
        ctx.ActiveLayer.GetPixel(5, 5).Should().Be(PixelColor.Black);
    }

    [Fact]
    public void ShadeBrushTool_WithActiveColorRamp_BoundsClamp_LighterDoesNotOverflow()
    {
        // Arrange: pixel is already at the lightest end of ramp
        var ctx = CreateContext();
        var ramp = new[]
        {
            PixelColor.Black,
            PixelColor.DarkGray,
            PixelColor.Gray,
            PixelColor.LightGray,
            PixelColor.White
        };
        ctx.ActiveColorRamp = ramp;
        ctx.ActiveLayer.SetPixel(5, 5, PixelColor.White);

        var tool = new ShadeBrushTool();

        // Act
        tool.OnPointerDown(ctx, 5, 5, isPrimary: false);
        tool.OnPointerUp(ctx, 5, 5);

        // Assert: remains White (clamped)
        ctx.ActiveLayer.GetPixel(5, 5).Should().Be(PixelColor.White);
    }

    [Fact]
    public void ShadeBrushTool_WithReversedRamp_DarkerAndLighterTransitionsWorkCorrectly()
    {
        // Arrange: ramp ordered from lightest to darkest
        var ctx = CreateContext();
        var reversedRamp = new[]
        {
            PixelColor.White,
            PixelColor.LightGray,
            PixelColor.Gray,
            PixelColor.DarkGray,
            PixelColor.Black
        };
        ctx.ActiveColorRamp = reversedRamp;
        var tool = new ShadeBrushTool();

        // Act & Assert 1: Darker from Gray -> DarkGray
        ctx.ActiveLayer.SetPixel(2, 2, PixelColor.Gray);
        tool.OnPointerDown(ctx, 2, 2, isPrimary: true);
        tool.OnPointerUp(ctx, 2, 2);
        ctx.ActiveLayer.GetPixel(2, 2).Should().Be(PixelColor.DarkGray);

        // Act & Assert 2: Lighter from Gray -> LightGray
        ctx.ActiveLayer.SetPixel(3, 3, PixelColor.Gray);
        tool.OnPointerDown(ctx, 3, 3, isPrimary: false);
        tool.OnPointerUp(ctx, 3, 3);
        ctx.ActiveLayer.GetPixel(3, 3).Should().Be(PixelColor.LightGray);

        // Act & Assert 3: Clamp Black (darkest) darker -> Black
        ctx.ActiveLayer.SetPixel(4, 4, PixelColor.Black);
        tool.OnPointerDown(ctx, 4, 4, isPrimary: true);
        tool.OnPointerUp(ctx, 4, 4);
        ctx.ActiveLayer.GetPixel(4, 4).Should().Be(PixelColor.Black);

        // Act & Assert 4: Clamp White (lightest) lighter -> White
        ctx.ActiveLayer.SetPixel(5, 5, PixelColor.White);
        tool.OnPointerDown(ctx, 5, 5, isPrimary: false);
        tool.OnPointerUp(ctx, 5, 5);
        ctx.ActiveLayer.GetPixel(5, 5).Should().Be(PixelColor.White);
    }

    [Fact]
    public void ShadeBrushTool_Fallback_WithoutRamp_ShiftsRgbBrightnessDarker()
    {
        // Arrange: no ramp configured, RGB brightness shift -10%
        var ctx = CreateContext();
        ctx.ActiveColorRamp = null;
        ctx.ActiveLayer.SetPixel(10, 10, new PixelColor(100, 100, 100, 255));

        var tool = new ShadeBrushTool { IsPrimary = true };

        // Act
        tool.OnPointerDown(ctx, 10, 10);
        tool.OnPointerUp(ctx, 10, 10);

        // Assert: 100 * 0.9 = 90
        ctx.ActiveLayer.GetPixel(10, 10).Should().Be(new PixelColor(90, 90, 90, 255));
    }

    [Fact]
    public void ShadeBrushTool_Fallback_WithoutRamp_ShiftsRgbBrightnessLighter()
    {
        // Arrange: no ramp configured, RGB brightness shift +10%
        var ctx = CreateContext();
        ctx.ActiveColorRamp = null;
        ctx.ActiveLayer.SetPixel(10, 10, new PixelColor(100, 100, 100, 255));

        var tool = new ShadeBrushTool();

        // Act: Right-click (lighter)
        tool.OnPointerDown(ctx, 10, 10, isPrimary: false);
        tool.OnPointerUp(ctx, 10, 10);

        // Assert: 100 * 1.1 = 110
        ctx.ActiveLayer.GetPixel(10, 10).Should().Be(new PixelColor(110, 110, 110, 255));
    }

    [Fact]
    public void ShadeBrushTool_Fallback_ColorNotInRamp_ShiftsRgbBrightness()
    {
        // Arrange: Ramp contains Red shades, but pixel is Blue
        var ctx = CreateContext();
        ctx.ActiveColorRamp = new[] { new PixelColor(100, 0, 0), new PixelColor(200, 0, 0) };
        ctx.ActiveLayer.SetPixel(10, 10, new PixelColor(50, 50, 100, 255));

        var tool = new ShadeBrushTool { IsPrimary = true };

        // Act
        tool.OnPointerDown(ctx, 10, 10);
        tool.OnPointerUp(ctx, 10, 10);

        // Assert: shifted by -10% brightness fallback: (45, 45, 90)
        ctx.ActiveLayer.GetPixel(10, 10).Should().Be(new PixelColor(45, 45, 90, 255));
    }

    [Fact]
    public void ShadeBrushTool_Fallback_BoundsClamp_BlackDarkerAndWhiteLighter()
    {
        // Arrange
        var ctx = CreateContext();
        ctx.ActiveColorRamp = null;
        ctx.ActiveLayer.SetPixel(0, 0, PixelColor.Black);
        ctx.ActiveLayer.SetPixel(1, 1, PixelColor.White);

        var tool = new ShadeBrushTool();

        // Act: Darker on Black
        tool.OnPointerDown(ctx, 0, 0, isPrimary: true);
        tool.OnPointerUp(ctx, 0, 0);

        // Act: Lighter on White
        tool.OnPointerDown(ctx, 1, 1, isPrimary: false);
        tool.OnPointerUp(ctx, 1, 1);

        // Assert
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Black);
        ctx.ActiveLayer.GetPixel(1, 1).Should().Be(PixelColor.White);
    }

    [Fact]
    public void ShadeBrushTool_PointerMove_AppliesAlongStrokeAndSupportsUndoRedo()
    {
        // Arrange
        var ctx = CreateContext();
        ctx.ActiveColorRamp = null;
        for (int x = 2; x <= 5; x++)
        {
            ctx.ActiveLayer.SetPixel(x, 2, new PixelColor(100, 100, 100));
        }

        var tool = new ShadeBrushTool { IsPrimary = true };

        // Act: Drag across pixels (2,2) to (5,2)
        tool.OnPointerDown(ctx, 2, 2);
        tool.OnPointerMove(ctx, 5, 2);
        tool.OnPointerUp(ctx, 5, 2);

        // Assert: All 4 pixels are shaded
        for (int x = 2; x <= 5; x++)
        {
            ctx.ActiveLayer.GetPixel(x, 2).Should().Be(new PixelColor(90, 90, 90));
        }
        ctx.History.CanUndo.Should().BeTrue();

        // Undo
        ctx.History.Undo();
        for (int x = 2; x <= 5; x++)
        {
            ctx.ActiveLayer.GetPixel(x, 2).Should().Be(new PixelColor(100, 100, 100));
        }

        // Redo
        ctx.History.Redo();
        for (int x = 2; x <= 5; x++)
        {
            ctx.ActiveLayer.GetPixel(x, 2).Should().Be(new PixelColor(90, 90, 90));
        }
    }

    [Fact]
    public void ShadeBrushTool_WithSymmetry_ShadesSymmetricPixels()
    {
        // Arrange
        var ctx = CreateContext(10, 10);
        ctx.SymmetryX = true;
        ctx.ActiveColorRamp = null;
        ctx.ActiveLayer.SetPixel(1, 1, new PixelColor(80, 80, 80));
        ctx.ActiveLayer.SetPixel(8, 1, new PixelColor(80, 80, 80));

        var tool = new ShadeBrushTool { IsPrimary = true };

        // Act
        tool.OnPointerDown(ctx, 1, 1);
        tool.OnPointerUp(ctx, 1, 1);

        // Assert: Both sides shaded
        ctx.ActiveLayer.GetPixel(1, 1).Should().Be(new PixelColor(72, 72, 72));
        ctx.ActiveLayer.GetPixel(8, 1).Should().Be(new PixelColor(72, 72, 72));
    }

    [Fact]
    public void ShadeBrushTool_WithSelection_RestrictsShadingToSelection()
    {
        // Arrange
        var ctx = CreateContext(16, 16);
        ctx.ActiveColorRamp = null;
        ctx.Selection.Select(new SKRectI(2, 2, 5, 5));
        ctx.ActiveLayer.SetPixel(2, 2, new PixelColor(100, 100, 100));
        ctx.ActiveLayer.SetPixel(10, 10, new PixelColor(100, 100, 100));

        var tool = new ShadeBrushTool { IsPrimary = true };

        // Act on pixel inside selection
        tool.OnPointerDown(ctx, 2, 2);
        tool.OnPointerUp(ctx, 2, 2);

        // Act on pixel outside selection
        tool.OnPointerDown(ctx, 10, 10);
        tool.OnPointerUp(ctx, 10, 10);

        // Assert
        ctx.ActiveLayer.GetPixel(2, 2).Should().Be(new PixelColor(90, 90, 90));
        ctx.ActiveLayer.GetPixel(10, 10).Should().Be(new PixelColor(100, 100, 100)); // unchanged
    }

    [Fact]
    public void ShadeBrushTool_AlphaLock_SkipsTransparentPixels()
    {
        // Arrange
        var ctx = CreateContext();
        ctx.ActiveLayer.IsAlphaLocked = true;
        ctx.ActiveLayer.SetPixel(5, 5, PixelColor.Transparent);

        var tool = new ShadeBrushTool { IsPrimary = true };

        // Act
        tool.OnPointerDown(ctx, 5, 5);
        tool.OnPointerUp(ctx, 5, 5);

        // Assert: Transparent pixel unmodified
        ctx.ActiveLayer.GetPixel(5, 5).Should().Be(PixelColor.Transparent);
        ctx.History.CanUndo.Should().BeFalse();
    }

    #endregion

    #region 2. SprayBrushTool Tests

    [Fact]
    public void SprayBrushTool_Metadata_PropertiesAreCorrect()
    {
        // Arrange
        var tool = new SprayBrushTool();

        // Assert
        tool.Name.Should().Be("Spray Brush");
        tool.Tooltip.Should().Be("Texture Spray / Noise Brush");
        tool.Shortcut.Should().Be("A");
        tool.IconKey.Should().Be("IconSprayBrush");
        tool.Radius.Should().Be(4);
        tool.Density.Should().Be(0.25f);
        tool.IsPrimary.Should().BeTrue();
        tool.GetPreviewPixels(CreateContext()).Should().BeEmpty();
    }

    [Fact]
    public void SprayBrushTool_ScattersPixelsStrictlyWithinRadius()
    {
        // Arrange
        var ctx = CreateContext(64, 64);
        int centerX = 32;
        int centerY = 32;
        int radius = 5;

        var tool = new SprayBrushTool(seed: 42)
        {
            Radius = radius,
            Density = 0.5f
        };

        // Act
        tool.OnPointerDown(ctx, centerX, centerY);
        tool.OnPointerUp(ctx, centerX, centerY);

        // Assert: at least one pixel is scattered
        int modifiedCount = 0;
        int r2 = radius * radius;

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                if (ctx.ActiveLayer.GetPixel(x, y) == ctx.PrimaryColor)
                {
                    modifiedCount++;
                    int dx = x - centerX;
                    int dy = y - centerY;
                    (dx * dx + dy * dy).Should().BeLessThanOrEqualTo(r2,
                        $"Scattered pixel at ({x},{y}) must be within radius {radius} of ({centerX},{centerY})");
                }
            }
        }

        modifiedCount.Should().BeGreaterThan(0, "A spray stroke with 0.5f density should scatter pixels");
    }

    [Fact]
    public void SprayBrushTool_DensityOne_FillsEntireDiskWithinRadius()
    {
        // Arrange
        var ctx = CreateContext(32, 32);
        int cx = 15;
        int cy = 15;
        int radius = 3;
        int r2 = radius * radius;

        var tool = new SprayBrushTool
        {
            Radius = radius,
            Density = 1.0f
        };

        // Act
        tool.OnPointerDown(ctx, cx, cy);
        tool.OnPointerUp(ctx, cx, cy);

        // Assert: every pixel in circle must be painted, outside must remain transparent
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                int dx = x - cx;
                int dy = y - cy;
                bool insideCircle = (dx * dx + dy * dy) <= r2;

                if (insideCircle)
                {
                    ctx.ActiveLayer.GetPixel(x, y).Should().Be(ctx.PrimaryColor,
                        $"Pixel at ({x},{y}) inside circle should be filled with PrimaryColor");
                }
                else
                {
                    ctx.ActiveLayer.GetPixel(x, y).Should().Be(PixelColor.Transparent,
                        $"Pixel at ({x},{y}) outside circle should not be modified");
                }
            }
        }
    }

    [Fact]
    public void SprayBrushTool_DensityZero_PlotsNoPixels()
    {
        // Arrange
        var ctx = CreateContext(32, 32);
        var tool = new SprayBrushTool
        {
            Radius = 4,
            Density = 0.0f
        };

        // Act
        tool.OnPointerDown(ctx, 10, 10);
        tool.OnPointerUp(ctx, 10, 10);

        // Assert
        ctx.History.CanUndo.Should().BeFalse();
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                ctx.ActiveLayer.GetPixel(x, y).Should().Be(PixelColor.Transparent);
            }
        }
    }

    [Fact]
    public void SprayBrushTool_RadiusClamping_ClampsBetweenOneAndSixteen()
    {
        // Arrange
        var tool = new SprayBrushTool();

        // Act & Assert
        tool.Radius = -5;
        tool.Radius.Should().Be(1);

        tool.Radius = 50;
        tool.Radius.Should().Be(16);

        tool.Radius = 8;
        tool.Radius.Should().Be(8);
    }

    [Fact]
    public void SprayBrushTool_DensityClamping_ClampsBetweenZeroAndOne()
    {
        // Arrange
        var tool = new SprayBrushTool();

        // Act & Assert
        tool.Density = -0.5f;
        tool.Density.Should().Be(0f);

        tool.Density = 1.8f;
        tool.Density.Should().Be(1f);

        tool.Density = 0.75f;
        tool.Density.Should().Be(0.75f);
    }

    [Fact]
    public void SprayBrushTool_ContextRadiusAndDensity_UsedWhenNotExplicitlyOverridden()
    {
        // Arrange
        var ctx = CreateContext(32, 32);
        ctx.SprayRadius = 2;
        ctx.SprayDensity = 1.0f;

        var tool = new SprayBrushTool(); // tool.Radius not set explicitly

        // Act
        tool.OnPointerDown(ctx, 10, 10);
        tool.OnPointerUp(ctx, 10, 10);

        // Assert: radius 2 disk filled
        ctx.ActiveLayer.GetPixel(10, 10).Should().Be(ctx.PrimaryColor);
        ctx.ActiveLayer.GetPixel(10 + 2, 10).Should().Be(ctx.PrimaryColor);
        ctx.ActiveLayer.GetPixel(10 + 3, 10).Should().Be(PixelColor.Transparent); // radius 2 limit
    }

    [Fact]
    public void SprayBrushTool_PrimaryAndSecondaryColor_LeftClickPrimary_RightClickSecondary()
    {
        // Arrange
        var ctx = CreateContext(32, 32);
        ctx.PrimaryColor = PixelColor.Red;
        ctx.SecondaryColor = PixelColor.Blue;

        var tool = new SprayBrushTool
        {
            Radius = 2,
            Density = 1.0f
        };

        // Act 1: Left click (primary)
        tool.OnPointerDown(ctx, 5, 5, isPrimary: true);
        tool.OnPointerUp(ctx, 5, 5);

        // Act 2: Right click (secondary)
        tool.OnPointerDown(ctx, 20, 20, isPrimary: false);
        tool.OnPointerUp(ctx, 20, 20);

        // Assert
        ctx.ActiveLayer.GetPixel(5, 5).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(20, 20).Should().Be(PixelColor.Blue);
    }

    [Fact]
    public void SprayBrushTool_PointerMove_InterpolatesAlongStroke()
    {
        // Arrange
        var ctx = CreateContext(32, 32);
        var tool = new SprayBrushTool(seed: 123)
        {
            Radius = 2,
            Density = 0.8f
        };

        // Act: drag line from (5, 5) to (15, 5)
        tool.OnPointerDown(ctx, 5, 5);
        tool.OnPointerMove(ctx, 15, 5);
        tool.OnPointerUp(ctx, 15, 5);

        // Assert: pixels scattered near start, middle, and end
        bool hasStart = false, hasMiddle = false, hasEnd = false;
        for (int y = 3; y <= 7; y++)
        {
            for (int x = 4; x <= 6; x++) if (ctx.ActiveLayer.GetPixel(x, y) == ctx.PrimaryColor) hasStart = true;
            for (int x = 9; x <= 11; x++) if (ctx.ActiveLayer.GetPixel(x, y) == ctx.PrimaryColor) hasMiddle = true;
            for (int x = 14; x <= 16; x++) if (ctx.ActiveLayer.GetPixel(x, y) == ctx.PrimaryColor) hasEnd = true;
        }

        hasStart.Should().BeTrue();
        hasMiddle.Should().BeTrue();
        hasEnd.Should().BeTrue();
    }

    [Fact]
    public void SprayBrushTool_WithSymmetry_MirrorsScatteredParticles()
    {
        // Arrange
        var ctx = CreateContext(16, 16);
        ctx.SymmetryX = true;

        var tool = new SprayBrushTool(seed: 999)
        {
            Radius = 3,
            Density = 0.5f
        };

        // Act
        tool.OnPointerDown(ctx, 4, 8);
        tool.OnPointerUp(ctx, 4, 8);

        // Assert: whatever is scattered at x on the left is reflected at (15 - x) on the right
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                var leftColor = ctx.ActiveLayer.GetPixel(x, y);
                var rightColor = ctx.ActiveLayer.GetPixel(15 - x, y);

                leftColor.Should().Be(rightColor, $"Pixel at ({x},{y}) should match symmetric pixel at ({15 - x},{y})");
            }
        }
    }

    [Fact]
    public void SprayBrushTool_WithSelection_RestrictsParticlesToSelection()
    {
        // Arrange
        var ctx = CreateContext(32, 32);
        ctx.Selection.Select(new SKRectI(10, 10, 15, 15));

        var tool = new SprayBrushTool
        {
            Radius = 6,
            Density = 1.0f
        };

        // Act: spray centered at (10, 10)
        tool.OnPointerDown(ctx, 10, 10);
        tool.OnPointerUp(ctx, 10, 10);

        // Assert: all modified pixels must be inside selection
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                if (ctx.ActiveLayer.GetPixel(x, y) == ctx.PrimaryColor)
                {
                    ctx.Selection.Contains(x, y).Should().BeTrue(
                        $"Modified pixel at ({x},{y}) must be inside active selection");
                }
            }
        }
    }

    [Fact]
    public void SprayBrushTool_SupportsUndoAndRedo()
    {
        // Arrange
        var ctx = CreateContext(32, 32);
        var tool = new SprayBrushTool(seed: 777)
        {
            Radius = 3,
            Density = 0.5f
        };

        // Act
        tool.OnPointerDown(ctx, 10, 10);
        tool.OnPointerUp(ctx, 10, 10);

        ctx.History.CanUndo.Should().BeTrue();

        // Undo
        ctx.History.Undo();
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                ctx.ActiveLayer.GetPixel(x, y).Should().Be(PixelColor.Transparent);
            }
        }

        // Redo
        ctx.History.Redo();
        int restoredCount = 0;
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                if (ctx.ActiveLayer.GetPixel(x, y) == ctx.PrimaryColor) restoredCount++;
            }
        }
        restoredCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ShadeBrushTool_ShiftRamp_PreservesCustomAlphaChannel()
    {
        // Arrange
        var ctx = CreateContext();
        var ramp = new[]
        {
            PixelColor.Black,
            PixelColor.DarkGray,
            PixelColor.Gray,
            PixelColor.LightGray,
            PixelColor.White
        };
        ctx.ActiveColorRamp = ramp;
        var semiTransparentGray = new PixelColor(128, 128, 128, 180);
        ctx.ActiveLayer.SetPixel(3, 3, semiTransparentGray);

        var tool = new ShadeBrushTool { IsPrimary = true };

        // Act
        tool.OnPointerDown(ctx, 3, 3);
        tool.OnPointerUp(ctx, 3, 3);

        // Assert: RGB is DarkGray, but Alpha is preserved at 180
        var result = ctx.ActiveLayer.GetPixel(3, 3);
        result.R.Should().Be(PixelColor.DarkGray.R);
        result.G.Should().Be(PixelColor.DarkGray.G);
        result.B.Should().Be(PixelColor.DarkGray.B);
        result.A.Should().Be(180);
    }

    [Fact]
    public void Tools_OutOfBoundsCoordinates_DoNotThrowExceptions()
    {
        // Arrange
        var ctx = CreateContext(16, 16);
        var shadeTool = new ShadeBrushTool();
        var sprayTool = new SprayBrushTool { Radius = 4, Density = 0.5f };

        // Act & Assert: Shade tool outside bounds
        var actShade = () =>
        {
            shadeTool.OnPointerDown(ctx, -5, -5);
            shadeTool.OnPointerMove(ctx, 25, 25);
            shadeTool.OnPointerUp(ctx, 25, 25);
        };
        actShade.Should().NotThrow();

        // Act & Assert: Spray tool outside bounds
        var actSpray = () =>
        {
            sprayTool.OnPointerDown(ctx, -10, -10);
            sprayTool.OnPointerMove(ctx, 30, 30);
            sprayTool.OnPointerUp(ctx, 30, 30);
        };
        actSpray.Should().NotThrow();
    }

    #endregion
}
