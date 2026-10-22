using FluentAssertions;
using PixelForge.Core.Models;
using SkiaSharp;
using Xunit;

namespace PixelForge.Core.Tests;

/// <summary>
/// Unit tests validating the TileGridSettings model, clamping, cycling sequences, and coordinate snapping math.
/// </summary>
public class TileGridTests
{
    [Fact]
    public void Defaults_ShouldMatchSpecifications()
    {
        // Arrange & Act
        var settings = new TileGridSettings();

        // Assert
        settings.IsVisible.Should().BeFalse();
        settings.TileSize.Should().Be(16);
        settings.TileGridText.Should().Be("16 × 16 px");
        settings.GridColor.Should().Be(TileGridSettings.DefaultGridColor);
        settings.GridColor.Alpha.Should().BeInRange(40, 70);
        TileGridSettings.AvailableSizes.Should().Equal([8, 16, 24, 32, 48, 64]);
    }

    [Fact]
    public void Constructor_WithCustomArguments_ShouldInitializeCorrectly()
    {
        // Arrange
        var customColor = new SKColor(255, 200, 100, 50);

        // Act
        var settings = new TileGridSettings(32, true, customColor);

        // Assert
        settings.TileSize.Should().Be(32);
        settings.IsVisible.Should().BeTrue();
        settings.GridColor.Should().Be(customColor);
        settings.TileGridText.Should().Be("32 × 32 px");
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(0, 2)]
    [InlineData(-5, 2)]
    [InlineData(-100, 2)]
    public void TileSize_WhenSetBelowMinimum_ShouldClampToTwo(int inputSize, int expectedSize)
    {
        // Arrange
        var settings = new TileGridSettings();

        // Act
        settings.TileSize = inputSize;

        // Assert
        settings.TileSize.Should().Be(expectedSize);
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(8, 8)]
    [InlineData(16, 16)]
    [InlineData(24, 24)]
    [InlineData(32, 32)]
    [InlineData(48, 48)]
    [InlineData(64, 64)]
    [InlineData(128, 128)]
    public void TileSize_WhenSetToValidSize_ShouldRetainValue(int inputSize, int expectedSize)
    {
        // Arrange
        var settings = new TileGridSettings();

        // Act
        settings.TileSize = inputSize;

        // Assert
        settings.TileSize.Should().Be(expectedSize);
        settings.TileGridText.Should().Be($"{expectedSize} × {expectedSize} px");
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(4, 8)]
    [InlineData(8, 8)]
    [InlineData(10, 8)]
    [InlineData(15, 16)]
    [InlineData(16, 16)]
    [InlineData(19, 16)]
    [InlineData(21, 24)]
    [InlineData(24, 24)]
    [InlineData(29, 32)]
    [InlineData(32, 32)]
    [InlineData(42, 48)]
    [InlineData(48, 48)]
    [InlineData(58, 64)]
    [InlineData(64, 64)]
    [InlineData(100, 64)]
    public void ClampToAvailableSize_ShouldReturnNearestStandardSize(int input, int expected)
    {
        // Act
        int result = TileGridSettings.ClampToAvailableSize(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(8, 16)]
    [InlineData(16, 24)]
    [InlineData(24, 32)]
    [InlineData(32, 48)]
    [InlineData(48, 64)]
    [InlineData(64, 8)]
    public void GetNextSize_StandardSequence_ShouldCycleCorrectly(int current, int expectedNext)
    {
        // Act
        int next = TileGridSettings.GetNextSize(current);

        // Assert
        next.Should().Be(expectedNext);
    }

    [Fact]
    public void CycleNextSize_InstanceMethod_ShouldLoopThroughAllAvailableSizes()
    {
        // Arrange
        var settings = new TileGridSettings(8);

        // Act & Assert Full Loop
        settings.CycleNextSize();
        settings.TileSize.Should().Be(16);

        settings.CycleNextSize();
        settings.TileSize.Should().Be(24);

        settings.CycleNextSize();
        settings.TileSize.Should().Be(32);

        settings.CycleNextSize();
        settings.TileSize.Should().Be(48);

        settings.CycleNextSize();
        settings.TileSize.Should().Be(64);

        settings.CycleNextSize();
        settings.TileSize.Should().Be(8);
    }

    [Theory]
    [InlineData(10, 16)]
    [InlineData(25, 32)]
    [InlineData(50, 64)]
    [InlineData(100, 8)]
    [InlineData(1, 8)]
    public void GetNextSize_FromNonStandardSize_ShouldAdvanceOrWrap(int current, int expectedNext)
    {
        // Act
        int next = TileGridSettings.GetNextSize(current);

        // Assert
        next.Should().Be(expectedNext);
    }

    [Theory]
    [InlineData(0, 16, 0)]
    [InlineData(5, 16, 0)]
    [InlineData(15, 16, 0)]
    [InlineData(16, 16, 16)]
    [InlineData(17, 16, 16)]
    [InlineData(31, 16, 16)]
    [InlineData(32, 16, 32)]
    [InlineData(-1, 16, -16)]
    [InlineData(-16, 16, -16)]
    [InlineData(-17, 16, -32)]
    public void SnapToTile_1D_ShouldSnapToContainingTileOrigin(int coordinate, int tileSize, int expected)
    {
        // Act
        int snapped = TileGridSettings.SnapToTile(coordinate, tileSize);

        // Assert
        snapped.Should().Be(expected);
    }

    [Fact]
    public void SnapToTile_2D_ShouldSnapBothAxes()
    {
        // Arrange
        int tileSize = 16;

        // Act
        var snapped = TileGridSettings.SnapToTile(25, 45, tileSize);

        // Assert
        snapped.X.Should().Be(16);
        snapped.Y.Should().Be(32);
    }

    [Theory]
    [InlineData(0, 0, 16, 0, 0)]
    [InlineData(15, 15, 16, 0, 0)]
    [InlineData(16, 31, 16, 1, 1)]
    [InlineData(32, 64, 16, 2, 4)]
    [InlineData(48, 12, 24, 2, 0)]
    public void GetTileIndex_ShouldReturnCorrectColumnAndRow(int px, int py, int tileSize, int expectedTx, int expectedTy)
    {
        // Act
        var (tx, ty) = TileGridSettings.GetTileIndex(px, py, tileSize);

        // Assert
        tx.Should().Be(expectedTx);
        ty.Should().Be(expectedTy);
    }

    [Theory]
    [InlineData(0, 0, 16, 0, 0, 16, 16)]
    [InlineData(1, 2, 16, 16, 32, 32, 48)]
    [InlineData(3, 1, 32, 96, 32, 128, 64)]
    public void GetTileBounds_ShouldReturnExpectedRectangle(int tx, int ty, int tileSize, int expL, int expT, int expR, int expB)
    {
        // Act
        var bounds = TileGridSettings.GetTileBounds(tx, ty, tileSize);

        // Assert
        bounds.Left.Should().Be(expL);
        bounds.Top.Should().Be(expT);
        bounds.Right.Should().Be(expR);
        bounds.Bottom.Should().Be(expB);
    }

    [Fact]
    public void PropertyChanged_ShouldTriggerOnPropertyMutations()
    {
        // Arrange
        var settings = new TileGridSettings();
        var changedProperties = new List<string>();
        settings.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                changedProperties.Add(e.PropertyName);
            }
        };

        // Act
        settings.IsVisible = true;
        settings.TileSize = 32;
        settings.GridColor = new SKColor(255, 255, 255, 50);

        // Assert
        changedProperties.Should().Contain(nameof(TileGridSettings.IsVisible));
        changedProperties.Should().Contain(nameof(TileGridSettings.TileSize));
        changedProperties.Should().Contain(nameof(TileGridSettings.TileGridText));
        changedProperties.Should().Contain(nameof(TileGridSettings.GridColor));
    }

    [Fact]
    public void Clone_ShouldCreateIndependentCopy()
    {
        // Arrange
        var original = new TileGridSettings(32, true, new SKColor(10, 20, 30, 40));

        // Act
        var clone = original.Clone();

        // Assert
        clone.Should().NotBeSameAs(original);
        clone.TileSize.Should().Be(original.TileSize);
        clone.IsVisible.Should().Be(original.IsVisible);
        clone.GridColor.Should().Be(original.GridColor);

        // Mutating clone does not mutate original
        clone.TileSize = 48;
        original.TileSize.Should().Be(32);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void SnapToTile_WhenTileSizeZeroOrNegative_ShouldReturnOriginalCoordinate(int invalidTileSize)
    {
        // Act & Assert
        TileGridSettings.SnapToTile(42, invalidTileSize).Should().Be(42);
        var snapped2D = TileGridSettings.SnapToTile(15, 27, invalidTileSize);
        snapped2D.X.Should().Be(15);
        snapped2D.Y.Should().Be(27);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void GetTileIndex_WhenTileSizeZeroOrNegative_ShouldReturnZeroZero(int invalidTileSize)
    {
        // Act
        var (tx, ty) = TileGridSettings.GetTileIndex(100, 200, invalidTileSize);

        // Assert
        tx.Should().Be(0);
        ty.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-16)]
    public void GetTileBounds_WhenTileSizeZeroOrNegative_ShouldReturnZeros(int invalidTileSize)
    {
        // Act
        var bounds = TileGridSettings.GetTileBounds(5, 5, invalidTileSize);

        // Assert
        bounds.Should().Be((0, 0, 0, 0));
    }

    [Theory]
    [InlineData(-100, 8)]
    [InlineData(0, 8)]
    [InlineData(500, 64)]
    public void ClampToAvailableSize_WithExtremeValues_ShouldClampToBoundaries(int input, int expected)
    {
        // Act & Assert
        TileGridSettings.ClampToAvailableSize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(-50, 8)]
    [InlineData(0, 8)]
    [InlineData(1000, 8)]
    public void GetNextSize_WithExtremeValues_ShouldFallbackToFirstAvailableSize(int current, int expectedNext)
    {
        // Act & Assert
        TileGridSettings.GetNextSize(current).Should().Be(expectedNext);
    }
}
