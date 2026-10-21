using FluentAssertions;
using PixelForge.Core.Algorithms;
using Xunit;

namespace PixelForge.Core.Tests;

public class PixelPerfectTests
{
    #region 1. PixelPerfectAlgorithm.IsCornerJaggie Tests

    [Theory]
    [InlineData(0, 0, 1, 0, 1, 1)] // (0,0) -> (1,0) -> (1,1) : right then down
    [InlineData(0, 0, 0, 1, 1, 1)] // (0,0) -> (0,1) -> (1,1) : down then right
    [InlineData(1, 0, 0, 0, 0, 1)] // (1,0) -> (0,0) -> (0,1) : left then down
    [InlineData(1, 1, 1, 0, 0, 0)] // (1,1) -> (1,0) -> (0,0) : up then left
    [InlineData(0, 1, 1, 1, 1, 0)] // (0,1) -> (1,1) -> (1,0) : right then up
    [InlineData(10, 10, 11, 10, 11, 11)] // Arbitrary offset
    public void IsCornerJaggie_WhenEndpointsAreDiagonalAndMiddleSharesAxis_ReturnsTrue(
        int x0, int y0, int x1, int y1, int x2, int y2)
    {
        // Act
        bool isJaggie = PixelPerfectAlgorithm.IsCornerJaggie(x0, y0, x1, y1, x2, y2);

        // Assert
        isJaggie.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 0, 1, 0, 2, 0)] // Horizontal straight line
    [InlineData(0, 0, 0, 1, 0, 2)] // Vertical straight line
    [InlineData(0, 0, 1, 1, 2, 2)] // Collinear diagonal line (dx=2, dy=2)
    [InlineData(0, 0, 2, 1, 4, 2)] // Non-adjacent jump
    [InlineData(0, 0, 1, 2, 2, 4)] // Large jump
    public void IsCornerJaggie_WhenEndpointsAreNotDiagonalNeighbors_ReturnsFalse(
        int x0, int y0, int x1, int y1, int x2, int y2)
    {
        // Act
        bool isJaggie = PixelPerfectAlgorithm.IsCornerJaggie(x0, y0, x1, y1, x2, y2);

        // Assert
        isJaggie.Should().BeFalse();
    }

    [Fact]
    public void IsCornerJaggie_WhenMiddlePointIsUnrelated_ReturnsFalse()
    {
        // Arrange & Act
        bool isJaggie = PixelPerfectAlgorithm.IsCornerJaggie(0, 0, 5, 5, 1, 1);

        // Assert
        isJaggie.Should().BeFalse();
    }

    [Fact]
    public void IsCornerJaggie_WhenPointsAreIdentical_ReturnsFalse()
    {
        // Arrange & Act
        bool resultAllSame = PixelPerfectAlgorithm.IsCornerJaggie(1, 1, 1, 1, 1, 1);
        bool resultP0SameP1 = PixelPerfectAlgorithm.IsCornerJaggie(0, 0, 0, 0, 1, 1);
        bool resultP1SameP2 = PixelPerfectAlgorithm.IsCornerJaggie(0, 0, 1, 1, 1, 1);

        // Assert
        resultAllSame.Should().BeFalse();
        resultP0SameP1.Should().BeFalse();
        resultP1SameP2.Should().BeFalse();
    }

    #endregion

    #region 2. PixelPerfectAlgorithm.FilterStroke Tests

    [Fact]
    public void FilterStroke_ThrowsOnNull()
    {
        // Act
        Action act = () => PixelPerfectAlgorithm.FilterStroke(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FilterStroke_EmptyAndSmallStrokes_HandledGracefully()
    {
        // Empty
        var empty = new List<(int, int)>();
        PixelPerfectAlgorithm.FilterStroke(empty).Should().BeEmpty();

        // 1 point
        var one = new List<(int, int)> { (5, 5) };
        PixelPerfectAlgorithm.FilterStroke(one).Should().Equal([(5, 5)]);

        // 2 points
        var two = new List<(int, int)> { (1, 1), (2, 2) };
        PixelPerfectAlgorithm.FilterStroke(two).Should().Equal([(1, 1), (2, 2)]);
    }

    [Fact]
    public void FilterStroke_SingleCornerJaggie_RemovesElbowPixel()
    {
        // Arrange: (0, 0) -> (1, 0) -> (1, 1) has elbow at (1, 0)
        var stroke = new List<(int, int)> { (0, 0), (1, 0), (1, 1) };

        // Act
        var filtered = PixelPerfectAlgorithm.FilterStroke(stroke);

        // Assert: (1, 0) is skipped
        filtered.Should().Equal([(0, 0), (1, 1)]);
    }

    [Fact]
    public void FilterStroke_AlternativeCornerJaggie_RemovesElbowPixel()
    {
        // Arrange: (0, 0) -> (0, 1) -> (1, 1) has elbow at (0, 1)
        var stroke = new List<(int, int)> { (0, 0), (0, 1), (1, 1) };

        // Act
        var filtered = PixelPerfectAlgorithm.FilterStroke(stroke);

        // Assert: (0, 1) is skipped
        filtered.Should().Equal([(0, 0), (1, 1)]);
    }

    [Fact]
    public void FilterStroke_MultiStepStaircase_FiltersAllElbows()
    {
        // Arrange: a 2-step staircase (0,0) -> (1,0) -> (1,1) -> (2,1) -> (2,2)
        var stroke = new List<(int, int)>
        {
            (0, 0),
            (1, 0),
            (1, 1),
            (2, 1),
            (2, 2)
        };

        // Act
        var filtered = PixelPerfectAlgorithm.FilterStroke(stroke);

        // Assert: perfectly clean diagonal line
        filtered.Should().Equal([(0, 0), (1, 1), (2, 2)]);
    }

    [Fact]
    public void FilterStroke_StraightLines_PreservesAllPoints()
    {
        // Arrange
        var horizontal = new List<(int, int)> { (0, 0), (1, 0), (2, 0), (3, 0) };
        var vertical = new List<(int, int)> { (5, 0), (5, 1), (5, 2), (5, 3) };

        // Act
        var filteredH = PixelPerfectAlgorithm.FilterStroke(horizontal);
        var filteredV = PixelPerfectAlgorithm.FilterStroke(vertical);

        // Assert
        filteredH.Should().Equal(horizontal);
        filteredV.Should().Equal(vertical);
    }

    [Fact]
    public void FilterStroke_WithDestinationBuffer_ReusesBufferWithoutAllocations()
    {
        // Arrange
        var stroke = new List<(int, int)> { (0, 0), (1, 0), (1, 1) };
        var dest = new List<(int, int)>();

        // Act
        PixelPerfectAlgorithm.FilterStroke(stroke, dest);

        // Assert
        dest.Should().Equal([(0, 0), (1, 1)]);
    }

    [Fact]
    public void FilterStroke_WithConsecutiveDuplicates_HandlesCorrectly()
    {
        // Arrange: stroke with duplicate points
        var stroke = new List<(int, int)> { (0, 0), (0, 0), (1, 0), (1, 1) };

        // Act
        var filtered = PixelPerfectAlgorithm.FilterStroke(stroke);

        // Assert
        filtered.Should().Equal([(0, 0), (1, 1)]);
    }

    #endregion

    #region 3. PixelPerfectAlgorithm.AddPoint Streaming Tests

    [Fact]
    public void AddPoint_WhenFormsJaggie_ReplacesPreviousPoint()
    {
        // Arrange
        var stroke = new List<(int, int)>();

        // Act & Assert
        bool added0 = PixelPerfectAlgorithm.AddPoint(stroke, 0, 0);
        added0.Should().BeFalse();
        stroke.Should().Equal([(0, 0)]);

        bool added1 = PixelPerfectAlgorithm.AddPoint(stroke, 1, 0);
        added1.Should().BeFalse();
        stroke.Should().Equal([(0, 0), (1, 0)]);

        bool replaced = PixelPerfectAlgorithm.AddPoint(stroke, 1, 1);
        replaced.Should().BeTrue();
        stroke.Should().Equal([(0, 0), (1, 1)]);
    }

    [Fact]
    public void AddPoint_WhenDuplicatePoint_DoesNotDuplicate()
    {
        // Arrange
        var stroke = new List<(int, int)> { (3, 3) };

        // Act
        bool result = PixelPerfectAlgorithm.AddPoint(stroke, 3, 3);

        // Assert
        result.Should().BeFalse();
        stroke.Should().Equal([(3, 3)]);
    }

    #endregion

    #region 4. StrokeStabilizer Tests

    [Fact]
    public void StrokeStabilizer_StrengthZero_ReturnsRawCoordinates()
    {
        // Arrange
        var stabilizer = new StrokeStabilizer(0);

        // Act
        var p1 = stabilizer.Process(10, 10);
        var p2 = stabilizer.Process(20, 25);
        var p3 = stabilizer.Process(50, 40);

        // Assert
        p1.Should().Be((10, 10));
        p2.Should().Be((20, 25));
        p3.Should().Be((50, 40));
    }

    [Fact]
    public void StrokeStabilizer_FirstPoint_InitializesPositionWithoutLag()
    {
        // Arrange
        var stabilizer = new StrokeStabilizer(10);
        stabilizer.HasStarted.Should().BeFalse();

        // Act
        var p1 = stabilizer.Process(100, 200);

        // Assert: first point always locks onto raw coordinate
        p1.Should().Be((100, 200));
        stabilizer.HasStarted.Should().BeTrue();
        stabilizer.CurrentPosition.Should().Be((100f, 200f));
    }

    [Fact]
    public void StrokeStabilizer_HigherStrength_ProducesMoreSmoothing()
    {
        // Arrange
        var light = new StrokeStabilizer(2);
        var heavy = new StrokeStabilizer(10);

        light.Process(0, 0);
        heavy.Process(0, 0);

        // Act: sudden jump to (100, 100)
        var pLight = light.Process(100, 100);
        var pHeavy = heavy.Process(100, 100);

        // Assert: light smoothing reaches closer to 100 than heavy smoothing
        pLight.X.Should().BeGreaterThan(pHeavy.X);
        pLight.Y.Should().BeGreaterThan(pHeavy.Y);
    }

    [Fact]
    public void StrokeStabilizer_Strength_ClampedBetweenZeroAndTen()
    {
        // Arrange
        var stabilizer = new StrokeStabilizer();

        // Act & Assert
        stabilizer.Strength = -5;
        stabilizer.Strength.Should().Be(0);

        stabilizer.Strength = 15;
        stabilizer.Strength.Should().Be(10);

        stabilizer.Strength = 7;
        stabilizer.Strength.Should().Be(7);
    }

    [Fact]
    public void StrokeStabilizer_Reset_ClearsActiveState()
    {
        // Arrange
        var stabilizer = new StrokeStabilizer(5);
        stabilizer.Process(50, 50);
        stabilizer.HasStarted.Should().BeTrue();

        // Act
        stabilizer.Reset();

        // Assert
        stabilizer.HasStarted.Should().BeFalse();
        var next = stabilizer.Process(10, 10);
        next.Should().Be((10, 10));
    }

    [Fact]
    public void StrokeStabilizer_ProcessFloat_PerformsSmoothCalculations()
    {
        // Arrange
        var stabilizer = new StrokeStabilizer(4);
        stabilizer.Process(0f, 0f);

        // Act
        var (x, y) = stabilizer.Process(10f, 20f);

        // Assert
        x.Should().BeGreaterThan(0f).And.BeLessThan(10f);
        y.Should().BeGreaterThan(0f).And.BeLessThan(20f);
    }

    #endregion
}
