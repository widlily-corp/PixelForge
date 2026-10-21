using FluentAssertions;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using PixelForge.Core.Tools;
using SkiaSharp;
using Xunit;

namespace PixelForge.Core.Tests;

public class SmartToolsTests
{
    // ==========================================
    // 1. Layer.IsAlphaLocked Tests
    // ==========================================

    [Fact]
    public void Layer_IsAlphaLocked_DefaultsToFalse()
    {
        // Arrange & Act
        var layer = new Layer("Test", 10, 10);

        // Assert
        layer.IsAlphaLocked.Should().BeFalse();
    }

    [Fact]
    public void Layer_IsAlphaLocked_PropertyNotification_FiresOnStateChange()
    {
        // Arrange
        var layer = new Layer("Test", 10, 10);
        string? changedProp = null;
        layer.PropertyChanged += (s, e) => changedProp = e.PropertyName;

        // Act
        layer.IsAlphaLocked = true;

        // Assert
        layer.IsAlphaLocked.Should().BeTrue();
        changedProp.Should().Be(nameof(Layer.IsAlphaLocked));
    }

    [Fact]
    public void Layer_SetPixel_WhenAlphaLocked_ProtectsTransparentPixels()
    {
        // Arrange
        var layer = new Layer("Test", 10, 10);
        layer.SetPixel(2, 2, PixelColor.Transparent);
        layer.IsAlphaLocked = true;

        // Act - Attempt to paint onto transparent pixel
        layer.SetPixel(2, 2, PixelColor.Red);

        // Assert - Pixel must remain transparent
        layer.GetPixel(2, 2).Should().Be(PixelColor.Transparent);
    }

    [Fact]
    public void Layer_SetPixel_WhenAlphaLocked_AllowsModifyingOpaquePixels()
    {
        // Arrange
        var layer = new Layer("Test", 10, 10);
        layer.SetPixel(3, 3, PixelColor.Red); // Initially opaque red
        layer.IsAlphaLocked = true;

        // Act - Repaint the already opaque pixel with blue
        layer.SetPixel(3, 3, PixelColor.Blue);

        // Assert - Pixel color was updated to Blue
        layer.GetPixel(3, 3).Should().Be(PixelColor.Blue);
    }

    [Fact]
    public void Layer_SetPixel_WhenAlphaLockDisabled_AllowsModifyingTransparentPixels()
    {
        // Arrange
        var layer = new Layer("Test", 10, 10);
        layer.IsAlphaLocked = false;

        // Act
        layer.SetPixel(4, 4, PixelColor.Green);

        // Assert
        layer.GetPixel(4, 4).Should().Be(PixelColor.Green);
    }

    [Fact]
    public void Layer_Clear_WhenAlphaLocked_PreservesTransparentPixelsAndClearsOpaque()
    {
        // Arrange
        var layer = new Layer("Test", 4, 4);
        layer.SetPixel(1, 1, PixelColor.Red);
        layer.SetPixel(2, 2, PixelColor.Blue);
        layer.IsAlphaLocked = true;

        // Act - Clear with Yellow fill
        layer.Clear(PixelColor.Yellow);

        // Assert
        // Transparent pixels remain transparent
        layer.GetPixel(0, 0).Should().Be(PixelColor.Transparent);
        layer.GetPixel(3, 3).Should().Be(PixelColor.Transparent);

        // Opaque pixels were filled with Yellow
        layer.GetPixel(1, 1).Should().Be(PixelColor.Yellow);
        layer.GetPixel(2, 2).Should().Be(PixelColor.Yellow);
    }

    [Fact]
    public void Layer_Clone_PreservesIsAlphaLockedState()
    {
        // Arrange
        var layer = new Layer("Test", 8, 8);
        layer.IsAlphaLocked = true;

        // Act
        var clone = layer.Clone();

        // Assert
        clone.IsAlphaLocked.Should().BeTrue();
    }

    // ==========================================
    // 2. SmartTools.GenerateOutline Tests
    // ==========================================

    [Fact]
    public void GenerateOutline_4Way_SinglePixel_Adds4CrossNeighbors()
    {
        // Arrange - 5x5 layer with single red pixel at center (2, 2)
        var layer = new Layer("Test", 5, 5);
        layer.SetPixel(2, 2, PixelColor.Red);

        // Act - 4-way outline, thickness 1
        var deltas = SmartTools.GenerateOutline(layer, PixelColor.Black, is8Way: false, thickness: 1);

        // Assert
        deltas.Should().HaveCount(4);
        // Orthogonal neighbors should be black
        layer.GetPixel(2, 1).Should().Be(PixelColor.Black);
        layer.GetPixel(2, 3).Should().Be(PixelColor.Black);
        layer.GetPixel(1, 2).Should().Be(PixelColor.Black);
        layer.GetPixel(3, 2).Should().Be(PixelColor.Black);

        // Diagonals must remain transparent
        layer.GetPixel(1, 1).Should().Be(PixelColor.Transparent);
        layer.GetPixel(3, 3).Should().Be(PixelColor.Transparent);

        // Shape pixel itself must remain Red
        layer.GetPixel(2, 2).Should().Be(PixelColor.Red);
    }

    [Fact]
    public void GenerateOutline_8Way_SinglePixel_Adds8SurroundingNeighbors()
    {
        // Arrange - 5x5 layer with single red pixel at center (2, 2)
        var layer = new Layer("Test", 5, 5);
        layer.SetPixel(2, 2, PixelColor.Red);

        // Act - 8-way outline, thickness 1
        var deltas = SmartTools.GenerateOutline(layer, PixelColor.Black, is8Way: true, thickness: 1);

        // Assert
        deltas.Should().HaveCount(8);
        // All 8 surrounding pixels are Black
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                {
                    layer.GetPixel(2, 2).Should().Be(PixelColor.Red);
                }
                else
                {
                    layer.GetPixel(2 + dx, 2 + dy).Should().Be(PixelColor.Black);
                }
            }
        }
    }

    [Fact]
    public void GenerateOutline_Thickness2_ExpandsTwoPixelLayersOutward()
    {
        // Arrange - 7x7 layer with single red pixel at center (3, 3)
        var layer = new Layer("Test", 7, 7);
        layer.SetPixel(3, 3, PixelColor.Red);

        // Act - 4-way outline, thickness 2
        var deltas = SmartTools.GenerateOutline(layer, PixelColor.Black, is8Way: false, thickness: 2);

        // Assert
        // Distance 1: (3,2), (3,4), (2,3), (4,3) -> 4 pixels
        // Distance 2: (3,1), (3,5), (1,3), (5,3) and (2,2), (4,2), (2,4), (4,4) -> 8 pixels
        // Total outline pixels: 12
        deltas.Should().HaveCount(12);
        layer.GetPixel(3, 1).Should().Be(PixelColor.Black); // 2 steps up
        layer.GetPixel(3, 5).Should().Be(PixelColor.Black); // 2 steps down
        layer.GetPixel(3, 3).Should().Be(PixelColor.Red);   // original center
    }

    [Fact]
    public void GenerateOutline_WithActiveSelection_OnlyOutlinesSelectedShape()
    {
        // Arrange - 8x8 layer with two red pixels: (2, 2) and (6, 6)
        var layer = new Layer("Test", 8, 8);
        layer.SetPixel(2, 2, PixelColor.Red);
        layer.SetPixel(6, 6, PixelColor.Red);

        // Restrict selection to around (2, 2) only
        var selection = new SelectionModel();
        selection.Select(new SKRectI(1, 1, 4, 4));

        // Act
        var deltas = SmartTools.GenerateOutline(layer, PixelColor.Black, is8Way: false, thickness: 1, selection);

        // Assert - Only (2, 2) got outlined (4 pixels)
        deltas.Should().HaveCount(4);
        layer.GetPixel(2, 1).Should().Be(PixelColor.Black);

        // (6, 6) was outside selection, so its neighbors remain transparent
        layer.GetPixel(6, 5).Should().Be(PixelColor.Transparent);
    }

    [Fact]
    public void GenerateOutline_WhenAlphaLocked_DoesNotModifyTransparentPixels()
    {
        // Arrange
        var layer = new Layer("Test", 5, 5);
        layer.SetPixel(2, 2, PixelColor.Red);
        layer.IsAlphaLocked = true;

        // Act
        var deltas = SmartTools.GenerateOutline(layer, PixelColor.Black, is8Way: false, thickness: 1);

        // Assert
        deltas.Should().BeEmpty();
        layer.GetPixel(2, 1).Should().Be(PixelColor.Transparent);
    }

    [Fact]
    public void GenerateOutline_EmptyLayer_ReturnsZeroDeltas()
    {
        // Arrange
        var layer = new Layer("Test", 5, 5);

        // Act
        var deltas = SmartTools.GenerateOutline(layer, PixelColor.Black);

        // Assert
        deltas.Should().BeEmpty();
    }

    // ==========================================
    // 3. SmartTools.GenerateShadow Tests
    // ==========================================

    [Fact]
    public void GenerateShadow_StandardOffset_CastsShadowAtTargetCoordinates()
    {
        // Arrange - 5x5 layer with red pixel at (2, 2)
        var layer = new Layer("Test", 5, 5);
        layer.SetPixel(2, 2, PixelColor.Red);

        // Act - dx=1, dy=1, full opacity 1.0f
        var deltas = SmartTools.GenerateShadow(layer, PixelColor.Black, dx: 1, dy: 1, opacity: 1.0f);

        // Assert
        deltas.Should().ContainSingle();
        var delta = deltas[0];
        delta.X.Should().Be(3);
        delta.Y.Should().Be(3);
        delta.OldColor.Should().Be(PixelColor.Transparent);
        delta.NewColor.Should().Be(PixelColor.Black);

        // Shape pixel (2, 2) remains Red
        layer.GetPixel(2, 2).Should().Be(PixelColor.Red);
        // Shadow pixel (3, 3) is Black
        layer.GetPixel(3, 3).Should().Be(PixelColor.Black);
    }

    [Fact]
    public void GenerateShadow_WithPartialOpacity_SetsProperAlphaOnShadowPixel()
    {
        // Arrange
        var layer = new Layer("Test", 5, 5);
        layer.SetPixel(2, 2, PixelColor.Red);

        // Act - Opacity 0.5f (50%)
        var deltas = SmartTools.GenerateShadow(layer, PixelColor.Black, dx: 1, dy: 1, opacity: 0.5f);

        // Assert
        deltas.Should().ContainSingle();
        var shadowPixel = layer.GetPixel(3, 3);
        shadowPixel.R.Should().Be(0);
        shadowPixel.G.Should().Be(0);
        shadowPixel.B.Should().Be(0);
        // 255 * 0.5 ~= 128
        shadowPixel.A.Should().Be(128);
    }

    [Fact]
    public void GenerateShadow_ZeroOffset_ProducesZeroDeltasBecauseOccludedByShape()
    {
        // Arrange
        var layer = new Layer("Test", 5, 5);
        layer.SetPixel(2, 2, PixelColor.Red);

        // Act - dx=0, dy=0
        var deltas = SmartTools.GenerateShadow(layer, PixelColor.Black, dx: 0, dy: 0, opacity: 1.0f);

        // Assert
        deltas.Should().BeEmpty();
        layer.GetPixel(2, 2).Should().Be(PixelColor.Red);
    }

    [Fact]
    public void GenerateShadow_NegativeOffset_CastsShadowToUpperLeft()
    {
        // Arrange
        var layer = new Layer("Test", 5, 5);
        layer.SetPixel(3, 3, PixelColor.Red);

        // Act - dx = -1, dy = -1
        var deltas = SmartTools.GenerateShadow(layer, PixelColor.Black, dx: -1, dy: -1, opacity: 1.0f);

        // Assert
        deltas.Should().ContainSingle();
        deltas[0].X.Should().Be(2);
        deltas[0].Y.Should().Be(2);
        layer.GetPixel(2, 2).Should().Be(PixelColor.Black);
    }

    [Fact]
    public void GenerateShadow_OutOfBoundsOffset_ClipsSafely()
    {
        // Arrange - Pixel at right-bottom edge (4, 4) in a 5x5 layer
        var layer = new Layer("Test", 5, 5);
        layer.SetPixel(4, 4, PixelColor.Red);

        // Act - dx=2, dy=2 -> target (6, 6) is out of bounds
        var deltas = SmartTools.GenerateShadow(layer, PixelColor.Black, dx: 2, dy: 2, opacity: 1.0f);

        // Assert
        deltas.Should().BeEmpty();
    }

    [Fact]
    public void GenerateShadow_OnExistingColor_AlphaBlendsProperly()
    {
        // Arrange - Layer with red sprite at (1, 1) and white background pixel at (2, 2)
        var layer = new Layer("Test", 5, 5);
        layer.SetPixel(1, 1, PixelColor.Red);
        layer.SetPixel(2, 2, PixelColor.White);

        // Restrict shadow source shape to (1, 1) so (2, 2) is treated as receiving canvas
        var selection = new SelectionModel();
        selection.Select(new SKRectI(1, 1, 2, 2));

        // Act - Cast semi-transparent black shadow from (1, 1) onto (2, 2)
        var deltas = SmartTools.GenerateShadow(layer, PixelColor.Black, dx: 1, dy: 1, opacity: 0.5f, selection: selection);

        // Assert
        deltas.Should().ContainSingle();
        var blended = layer.GetPixel(2, 2);
        // Blending 50% black onto 100% white should give gray (~127)
        blended.R.Should().BeInRange(126, 128);
        blended.G.Should().BeInRange(126, 128);
        blended.B.Should().BeInRange(126, 128);
        blended.A.Should().Be(255);
    }

    // ==========================================
    // 4. SmartTools.ReplaceColor Tests
    // ==========================================

    [Fact]
    public void ReplaceColor_ExactMatch_ReplacesAllTargetPixels()
    {
        // Arrange
        var layer = new Layer("Test", 4, 4);
        layer.SetPixel(0, 0, PixelColor.Red);
        layer.SetPixel(1, 1, PixelColor.Red);
        layer.SetPixel(2, 2, PixelColor.Blue);

        // Act
        var deltas = SmartTools.ReplaceColor(layer, PixelColor.Red, PixelColor.Green, tolerance: 0);

        // Assert
        deltas.Should().HaveCount(2);
        layer.GetPixel(0, 0).Should().Be(PixelColor.Green);
        layer.GetPixel(1, 1).Should().Be(PixelColor.Green);
        layer.GetPixel(2, 2).Should().Be(PixelColor.Blue); // untouched
    }

    [Fact]
    public void ReplaceColor_WithTolerance_ReplacesSimilarColors()
    {
        // Arrange
        var layer = new Layer("Test", 4, 4);
        var baseRed = new PixelColor(255, 0, 0, 255);
        var nearRed = new PixelColor(245, 5, 5, 255);
        var farBlue = new PixelColor(0, 0, 255, 255);

        layer.SetPixel(0, 0, baseRed);
        layer.SetPixel(1, 1, nearRed);
        layer.SetPixel(2, 2, farBlue);

        // Act - tolerance 20 matches both baseRed and nearRed
        var deltas = SmartTools.ReplaceColor(layer, baseRed, PixelColor.Yellow, tolerance: 20);

        // Assert
        deltas.Should().HaveCount(2);
        layer.GetPixel(0, 0).Should().Be(PixelColor.Yellow);
        layer.GetPixel(1, 1).Should().Be(PixelColor.Yellow);
        layer.GetPixel(2, 2).Should().Be(farBlue);
    }

    [Fact]
    public void ReplaceColor_WithSelection_RestrictsReplacementToSelectionArea()
    {
        // Arrange
        var layer = new Layer("Test", 6, 6);
        layer.SetPixel(1, 1, PixelColor.Red);
        layer.SetPixel(4, 4, PixelColor.Red);

        var selection = new SelectionModel();
        selection.Select(new SKRectI(0, 0, 3, 3)); // Contains (1, 1) only

        // Act
        var deltas = SmartTools.ReplaceColor(layer, PixelColor.Red, PixelColor.Green, tolerance: 0, selection: selection);

        // Assert
        deltas.Should().ContainSingle();
        layer.GetPixel(1, 1).Should().Be(PixelColor.Green);
        layer.GetPixel(4, 4).Should().Be(PixelColor.Red); // Outside selection -> untouched
    }

    // ==========================================
    // 5. SmartToolCommands Tests (Undo & Redo)
    // ==========================================

    [Fact]
    public void OutlineCommand_ExecuteAndUndo_ProperlyModifiesAndRevertsLayer()
    {
        // Arrange
        var history = new UndoRedoService();
        var layer = new Layer("Test", 5, 5);
        layer.SetPixel(2, 2, PixelColor.Red);

        var cmd = new OutlineCommand(layer, PixelColor.Black, is8Way: false, thickness: 1);

        // Act 1: Execute
        history.ExecuteCommand(cmd);

        // Assert 1: Outline applied
        layer.GetPixel(2, 1).Should().Be(PixelColor.Black);
        layer.GetPixel(2, 2).Should().Be(PixelColor.Red);
        history.CanUndo.Should().BeTrue();

        // Act 2: Undo
        history.Undo();

        // Assert 2: Outline reverted
        layer.GetPixel(2, 1).Should().Be(PixelColor.Transparent);
        layer.GetPixel(2, 2).Should().Be(PixelColor.Red);
        history.CanRedo.Should().BeTrue();

        // Act 3: Redo
        history.Redo();

        // Assert 3: Outline re-applied
        layer.GetPixel(2, 1).Should().Be(PixelColor.Black);
    }

    [Fact]
    public void ShadowCommand_ExecuteAndUndo_ProperlyModifiesAndRevertsLayer()
    {
        // Arrange
        var history = new UndoRedoService();
        var layer = new Layer("Test", 5, 5);
        layer.SetPixel(2, 2, PixelColor.Red);

        var cmd = new ShadowCommand(layer, PixelColor.Black, dx: 1, dy: 1, opacity: 1.0f);

        // Act 1: Execute
        history.ExecuteCommand(cmd);

        // Assert 1: Shadow applied
        layer.GetPixel(3, 3).Should().Be(PixelColor.Black);
        layer.GetPixel(2, 2).Should().Be(PixelColor.Red);

        // Act 2: Undo
        history.Undo();

        // Assert 2: Shadow reverted
        layer.GetPixel(3, 3).Should().Be(PixelColor.Transparent);

        // Act 3: Redo
        history.Redo();

        // Assert 3: Shadow re-applied
        layer.GetPixel(3, 3).Should().Be(PixelColor.Black);
    }

    [Fact]
    public void ReplaceColorCommand_ExecuteAndUndo_ProperlyModifiesAndRevertsLayer()
    {
        // Arrange
        var history = new UndoRedoService();
        var layer = new Layer("Test", 4, 4);
        layer.SetPixel(1, 1, PixelColor.Red);
        layer.SetPixel(2, 2, PixelColor.Red);

        var cmd = new ReplaceColorCommand(layer, PixelColor.Red, PixelColor.Blue, tolerance: 0);

        // Act 1: Execute
        history.ExecuteCommand(cmd);

        // Assert 1: Replaced
        layer.GetPixel(1, 1).Should().Be(PixelColor.Blue);
        layer.GetPixel(2, 2).Should().Be(PixelColor.Blue);

        // Act 2: Undo
        history.Undo();

        // Assert 2: Reverted
        layer.GetPixel(1, 1).Should().Be(PixelColor.Red);
        layer.GetPixel(2, 2).Should().Be(PixelColor.Red);

        // Act 3: Redo
        history.Redo();

        // Assert 3: Re-applied
        layer.GetPixel(1, 1).Should().Be(PixelColor.Blue);
        layer.GetPixel(2, 2).Should().Be(PixelColor.Blue);
    }

    [Fact]
    public void SmartToolCommands_Descriptions_AreInformative()
    {
        // Arrange
        var layer = new Layer("Test", 4, 4);

        var outlineCmd = new OutlineCommand(layer, PixelColor.Black);
        var shadowCmd = new ShadowCommand(layer, PixelColor.Black);
        var replaceCmd = new ReplaceColorCommand(layer, PixelColor.Red, PixelColor.Blue);

        // Assert
        outlineCmd.Description.Should().Be("Generate Outline");
        shadowCmd.Description.Should().Be("Generate Shadow");
        replaceCmd.Description.Should().Be("Replace Color");
    }
}
