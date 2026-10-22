using FluentAssertions;
using PixelForge.Core.Algorithms;
using PixelForge.Core.History;
using PixelForge.Core.Models;
using PixelForge.Core.Tools;
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

    #region 5. PixelPerfectHelper Tests

    [Theory]
    [InlineData(0, 0, 1, 0, 1, 1)] // right then down
    [InlineData(0, 0, 0, 1, 1, 1)] // down then right
    [InlineData(1, 0, 0, 0, 0, 1)] // left then down
    [InlineData(1, 1, 1, 0, 0, 0)] // up then left
    [InlineData(0, 1, 1, 1, 1, 0)] // right then up
    [InlineData(15, 20, 16, 20, 16, 21)] // offset
    public void PixelPerfectHelper_IsCorner_WhenLCornerJaggie_ReturnsTrue(
        int x0, int y0, int x1, int y1, int x2, int y2)
    {
        // Act
        bool result = PixelPerfectHelper.IsCorner(x0, y0, x1, y1, x2, y2);
        bool resultTuple = PixelPerfectHelper.IsCorner((x0, y0), (x1, y1), (x2, y2));
        bool resultShouldRemove = PixelPerfectHelper.ShouldRemovePrevious(x0, y0, x1, y1, x2, y2);

        // Assert
        result.Should().BeTrue();
        resultTuple.Should().BeTrue();
        resultShouldRemove.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 0, 1, 0, 2, 0)] // Horizontal straight line
    [InlineData(0, 0, 0, 1, 0, 2)] // Vertical straight line
    [InlineData(0, 0, 1, 1, 2, 2)] // Diagonal straight line
    [InlineData(0, 0, 3, 3, 1, 1)] // Non-adjacent
    public void PixelPerfectHelper_IsCorner_WhenNotLCorner_ReturnsFalse(
        int x0, int y0, int x1, int y1, int x2, int y2)
    {
        // Act
        bool result = PixelPerfectHelper.IsCorner(x0, y0, x1, y1, x2, y2);
        bool resultTuple = PixelPerfectHelper.IsCorner((x0, y0), (x1, y1), (x2, y2));

        // Assert
        result.Should().BeFalse();
        resultTuple.Should().BeFalse();
    }

    [Fact]
    public void PixelPerfectHelper_AreDiagonallyAdjacent_ChecksAdjacencyCorrectly()
    {
        // Assert
        PixelPerfectHelper.AreDiagonallyAdjacent(0, 0, 1, 1).Should().BeTrue();
        PixelPerfectHelper.AreDiagonallyAdjacent(1, 1, 0, 0).Should().BeTrue();
        PixelPerfectHelper.AreDiagonallyAdjacent(0, 0, 1, 0).Should().BeFalse();
        PixelPerfectHelper.AreDiagonallyAdjacent(0, 0, 0, 1).Should().BeFalse();
        PixelPerfectHelper.AreDiagonallyAdjacent(0, 0, 2, 2).Should().BeFalse();
    }

    [Fact]
    public void PixelPerfectHelper_FilterStroke_StraightHorizontalAndVerticalLines_PreservesAllPoints()
    {
        // Arrange
        var horizontal = new List<(int, int)> { (0, 0), (1, 0), (2, 0), (3, 0), (4, 0) };
        var vertical = new List<(int, int)> { (2, 0), (2, 1), (2, 2), (2, 3), (2, 4) };

        // Act
        var filteredH = PixelPerfectHelper.FilterStroke(horizontal);
        var filteredV = PixelPerfectHelper.FilterStroke(vertical);

        // Assert
        filteredH.Should().Equal(horizontal);
        filteredV.Should().Equal(vertical);
    }

    [Fact]
    public void PixelPerfectHelper_FilterStroke_DiagonalStaircase_EliminatesLCornerJaggies()
    {
        // Arrange: Bresenham staircase for diagonal line from (0,0) to (3,3)
        var staircase = new List<(int, int)>
        {
            (0, 0),
            (1, 0), // elbow 1
            (1, 1),
            (2, 1), // elbow 2
            (2, 2),
            (3, 2), // elbow 3
            (3, 3)
        };

        // Act
        var filtered = PixelPerfectHelper.FilterStroke(staircase);

        // Assert: all elbow pixels removed, forming a clean 1px diagonal
        filtered.Should().Equal([(0, 0), (1, 1), (2, 2), (3, 3)]);
    }

    [Fact]
    public void PixelPerfectHelper_FilterStroke_PreservesStrokeContinuityAnd8Connectivity()
    {
        // Arrange: irregular curve with jaggies
        var stroke = new List<(int, int)>
        {
            (0, 0),
            (1, 0),
            (1, 1),
            (2, 1),
            (2, 2),
            (3, 2),
            (4, 2),
            (4, 3),
            (5, 4)
        };

        // Act
        var filtered = PixelPerfectHelper.FilterStroke(stroke);

        // Assert: start and end points are preserved
        filtered.First().Should().Be(stroke.First());
        filtered.Last().Should().Be(stroke.Last());

        // Assert: every consecutive pair preserves 8-connectivity (max delta <= 1)
        for (int i = 0; i < filtered.Count - 1; i++)
        {
            int dx = Math.Abs(filtered[i + 1].X - filtered[i].X);
            int dy = Math.Abs(filtered[i + 1].Y - filtered[i].Y);

            dx.Should().BeLessThanOrEqualTo(1, $"step {i} dx should be <= 1");
            dy.Should().BeLessThanOrEqualTo(1, $"step {i} dy should be <= 1");
            (dx + dy).Should().BeGreaterThan(0, $"step {i} should not be identical points");
        }
    }

    [Fact]
    public void PixelPerfectHelper_AddPoint_StreamingRealTimeFiltering_ReplacesCornerPixel()
    {
        // Arrange
        var stroke = new List<(int, int)>();

        // Act & Assert
        PixelPerfectHelper.AddPoint(stroke, 0, 0).Should().BeFalse();
        stroke.Should().Equal([(0, 0)]);

        PixelPerfectHelper.AddPoint(stroke, 1, 0).Should().BeFalse();
        stroke.Should().Equal([(0, 0), (1, 0)]);

        // Adding (1, 1) forms L-corner with (0,0) and (1,0) -> (1,0) is replaced with (1,1)
        PixelPerfectHelper.AddPoint(stroke, 1, 1).Should().BeTrue();
        stroke.Should().Equal([(0, 0), (1, 1)]);

        // Duplicate point ignored
        PixelPerfectHelper.AddPoint(stroke, 1, 1).Should().BeFalse();
        stroke.Should().Equal([(0, 0), (1, 1)]);

        PixelPerfectHelper.AddPoint(stroke, 2, 1).Should().BeFalse();
        stroke.Should().Equal([(0, 0), (1, 1), (2, 1)]);

        // Next diagonal step replaces (2, 1) with (2, 2)
        PixelPerfectHelper.AddPoint(stroke, 2, 2).Should().BeTrue();
        stroke.Should().Equal([(0, 0), (1, 1), (2, 2)]);
    }

    [Fact]
    public void PixelPerfectHelper_FilterStroke_WithDestinationList_AvoidsAllocations()
    {
        // Arrange
        var stroke = new List<(int, int)> { (0, 0), (1, 0), (1, 1) };
        var dest = new List<(int, int)>();

        // Act
        PixelPerfectHelper.FilterStroke(stroke, dest);

        // Assert
        dest.Should().Equal([(0, 0), (1, 1)]);
    }

    #endregion

    #region 6. PencilTool Pixel-Perfect Integration Tests

    [Fact]
    public void PencilTool_WhenPixelPerfectDisabled_DrawsStandardBresenhamWithJaggies()
    {
        // Arrange
        var canvas = new PixelCanvas(16, 16);
        var ctx = new ToolContext(canvas, canvas.ActiveLayer!, new UndoRedoService(), PixelColor.Red, PixelColor.Black)
        {
            IsPixelPerfect = false
        };
        var tool = new PencilTool();

        // Act: Draw freehand L-turn from (0, 0) -> (1, 0) -> (1, 1)
        tool.OnPointerDown(ctx, 0, 0);
        tool.OnPointerMove(ctx, 1, 0);
        tool.OnPointerMove(ctx, 1, 1);
        tool.OnPointerUp(ctx, 1, 1);

        // Assert: Standard drawing preserves the elbow pixel (1, 0)
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(1, 0).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(1, 1).Should().Be(PixelColor.Red);
    }

    [Fact]
    public void PencilTool_WhenPixelPerfectEnabled_EliminatesLCornerJaggies()
    {
        // Arrange
        var canvas = new PixelCanvas(16, 16);
        var ctx = new ToolContext(canvas, canvas.ActiveLayer!, new UndoRedoService(), PixelColor.Red, PixelColor.Black)
        {
            IsPixelPerfect = true
        };
        var tool = new PencilTool();

        // Act: Draw freehand L-turn from (0, 0) -> (1, 0) -> (1, 1)
        tool.OnPointerDown(ctx, 0, 0);
        tool.OnPointerMove(ctx, 1, 0);
        tool.OnPointerMove(ctx, 1, 1);
        tool.OnPointerUp(ctx, 1, 1);

        // Assert: Pixel-perfect eliminates elbow pixel (1, 0) while keeping endpoints
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Red);
        ctx.ActiveLayer.GetPixel(1, 0).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(1, 1).Should().Be(PixelColor.Red);
    }

    [Fact]
    public void PencilTool_WhenPixelPerfectEnabled_StraightLinesPreserveAllPoints()
    {
        // Arrange
        var canvas = new PixelCanvas(16, 16);
        var ctx = new ToolContext(canvas, canvas.ActiveLayer!, new UndoRedoService(), PixelColor.Blue, PixelColor.White)
        {
            IsPixelPerfect = true
        };
        var tool = new PencilTool();

        // Act: Draw straight horizontal line
        tool.OnPointerDown(ctx, 1, 3);
        tool.OnPointerMove(ctx, 4, 3);
        tool.OnPointerUp(ctx, 4, 3);

        // Assert: All 4 points along horizontal line are plotted
        for (int x = 1; x <= 4; x++)
        {
            ctx.ActiveLayer.GetPixel(x, 3).Should().Be(PixelColor.Blue);
        }
    }

    [Fact]
    public void PencilTool_WithPixelPerfectAndSymmetry_MirrorsCleanStroke()
    {
        // Arrange
        var canvas = new PixelCanvas(10, 10);
        var ctx = new ToolContext(canvas, canvas.ActiveLayer!, new UndoRedoService(), PixelColor.Yellow, PixelColor.Black)
        {
            IsPixelPerfect = true,
            SymmetryX = true
        };
        var tool = new PencilTool();

        // Act: Draw freehand L-turn from (0, 0) -> (1, 0) -> (1, 1)
        tool.OnPointerDown(ctx, 0, 0);
        tool.OnPointerMove(ctx, 1, 0);
        tool.OnPointerMove(ctx, 1, 1);
        tool.OnPointerUp(ctx, 1, 1);

        // Assert: Original side clean (elbow removed)
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Yellow);
        ctx.ActiveLayer.GetPixel(1, 0).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(1, 1).Should().Be(PixelColor.Yellow);

        // Assert: Mirrored side clean (x' = 9 - x, elbow at (8, 0) removed)
        ctx.ActiveLayer.GetPixel(9, 0).Should().Be(PixelColor.Yellow);
        ctx.ActiveLayer.GetPixel(8, 0).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(8, 1).Should().Be(PixelColor.Yellow);
    }

    [Fact]
    public void PencilTool_WithPixelPerfect_SupportsUndoAndRedo()
    {
        // Arrange
        var canvas = new PixelCanvas(16, 16);
        var history = new UndoRedoService();
        var ctx = new ToolContext(canvas, canvas.ActiveLayer!, history, PixelColor.Green, PixelColor.Black)
        {
            IsPixelPerfect = true
        };
        var tool = new PencilTool();

        // Act: Draw stroke
        tool.OnPointerDown(ctx, 0, 0);
        tool.OnPointerMove(ctx, 1, 0);
        tool.OnPointerMove(ctx, 1, 1);
        tool.OnPointerUp(ctx, 1, 1);

        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Green);
        ctx.ActiveLayer.GetPixel(1, 0).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(1, 1).Should().Be(PixelColor.Green);
        history.CanUndo.Should().BeTrue();

        // Undo
        history.Undo();
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(1, 0).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(1, 1).Should().Be(PixelColor.Transparent);

        // Redo
        history.Redo();
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Green);
        ctx.ActiveLayer.GetPixel(1, 0).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(1, 1).Should().Be(PixelColor.Green);
    }

    [Fact]
    public void PencilTool_OutOfBoundsCoordinates_DoesNotThrowExceptions()
    {
        // Arrange
        var canvas = new PixelCanvas(16, 16);
        var ctx = new ToolContext(canvas, canvas.ActiveLayer!, new UndoRedoService(), PixelColor.Red, PixelColor.Black)
        {
            IsPixelPerfect = true,
            SymmetryX = true,
            SymmetryY = true
        };
        var tool = new PencilTool();

        // Act & Assert
        var act = () =>
        {
            tool.OnPointerDown(ctx, -5, -5);
            tool.OnPointerMove(ctx, 25, 25);
            tool.OnPointerUp(ctx, 25, 25);
        };
        act.Should().NotThrow();
    }

    [Fact]
    public void PencilTool_WithPixelPerfectAndDualSymmetry_MirrorsCleanStrokeAcrossBothAxes()
    {
        // Arrange
        var canvas = new PixelCanvas(10, 10);
        var ctx = new ToolContext(canvas, canvas.ActiveLayer!, new UndoRedoService(), PixelColor.Cyan, PixelColor.Black)
        {
            IsPixelPerfect = true,
            SymmetryX = true,
            SymmetryY = true
        };
        var tool = new PencilTool();

        // Act: Draw freehand L-turn from (0, 0) -> (1, 0) -> (1, 1)
        tool.OnPointerDown(ctx, 0, 0);
        tool.OnPointerMove(ctx, 1, 0);
        tool.OnPointerMove(ctx, 1, 1);
        tool.OnPointerUp(ctx, 1, 1);

        // Assert: Primary corner (1, 0) is eliminated
        ctx.ActiveLayer.GetPixel(0, 0).Should().Be(PixelColor.Cyan);
        ctx.ActiveLayer.GetPixel(1, 0).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(1, 1).Should().Be(PixelColor.Cyan);

        // Assert: Symmetry across X (8, 0) is eliminated
        ctx.ActiveLayer.GetPixel(9, 0).Should().Be(PixelColor.Cyan);
        ctx.ActiveLayer.GetPixel(8, 0).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(8, 1).Should().Be(PixelColor.Cyan);

        // Assert: Symmetry across Y (1, 9) is eliminated
        ctx.ActiveLayer.GetPixel(0, 9).Should().Be(PixelColor.Cyan);
        ctx.ActiveLayer.GetPixel(1, 9).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(1, 8).Should().Be(PixelColor.Cyan);

        // Assert: Symmetry across XY (8, 9) is eliminated
        ctx.ActiveLayer.GetPixel(9, 9).Should().Be(PixelColor.Cyan);
        ctx.ActiveLayer.GetPixel(8, 9).Should().Be(PixelColor.Transparent);
        ctx.ActiveLayer.GetPixel(8, 8).Should().Be(PixelColor.Cyan);
    }

    #endregion
}
