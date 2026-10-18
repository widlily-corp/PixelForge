using System.Text.Json;
using FluentAssertions;
using PixelForge.App.Services;
using Xunit;

namespace PixelForge.App.Tests;

public class CrashReportServiceTests : IDisposable
{
    private readonly string _testCrashDir;

    public CrashReportServiceTests()
    {
        _testCrashDir = Path.Combine(Path.GetTempPath(), $"PixelForge_CrashTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testCrashDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testCrashDir))
            {
                Directory.Delete(_testCrashDir, true);
            }
        }
        catch
        {
            // Ignore test cleanup exceptions
        }
    }

    [Fact]
    public void Constructor_ShouldInitializeCrashDirectory()
    {
        // Act
        var service = new CrashReportService(_testCrashDir);

        // Assert
        service.CrashDirectory.Should().Be(_testCrashDir);
        Directory.Exists(_testCrashDir).Should().BeTrue();
    }

    [Fact]
    public void CaptureCrash_WithException_ShouldCreateJsonReportAndDump()
    {
        // Arrange
        var service = new CrashReportService(_testCrashDir);
        var testException = new InvalidOperationException("Simulation failure for unit test", new ArgumentNullException("param"));

        // Act
        var reportInfo = service.CaptureCrash(testException, "UnitTestContext", isTerminating: false);

        // Assert
        reportInfo.Should().NotBeNull();
        reportInfo!.Message.Should().Be("Simulation failure for unit test");
        reportInfo.ExceptionType.Should().Be(typeof(InvalidOperationException).FullName);
        File.Exists(reportInfo.ReportFilePath).Should().BeTrue();

        // Verify JSON contents
        string json = File.ReadAllText(reportInfo.ReportFilePath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("Context").GetString().Should().Be("UnitTestContext");
        root.GetProperty("IsTerminating").GetBoolean().Should().BeFalse();
        root.GetProperty("OperatingSystem").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("ProcessId").GetInt32().Should().BeGreaterThan(0);

        var exElement = root.GetProperty("Exception");
        exElement.GetProperty("Type").GetString().Should().Be(typeof(InvalidOperationException).FullName);
        exElement.GetProperty("Message").GetString().Should().Be("Simulation failure for unit test");

        var innerExElement = exElement.GetProperty("InnerException");
        innerExElement.GetProperty("Type").GetString().Should().Be(typeof(ArgumentNullException).FullName);
    }

    [Fact]
    public void GetCrashReports_ShouldReturnAllSavedReportsOrderedByDate()
    {
        // Arrange
        var service = new CrashReportService(_testCrashDir);
        var ex1 = new Exception("Error 1");
        var ex2 = new Exception("Error 2");

        // Act
        service.CaptureCrash(ex1, "Context1");
        Thread.Sleep(50); // Ensure distinct timestamps
        service.CaptureCrash(ex2, "Context2");

        var reports = service.GetCrashReports();

        // Assert
        reports.Should().HaveCount(2);
        reports[0].Message.Should().Be("Error 2");
        reports[1].Message.Should().Be("Error 1");
        reports[0].Timestamp.Should().BeOnOrAfter(reports[1].Timestamp);
    }

    [Fact]
    public void PruneOldReports_ShouldLimitTotalStoredFiles()
    {
        // Arrange
        var service = new CrashReportService(_testCrashDir);

        for (int i = 0; i < 6; i++)
        {
            service.CaptureCrash(new Exception($"Crash {i}"), $"Context {i}");
            Thread.Sleep(10);
        }

        // Act - prune to keep only 2 reports
        service.PruneOldReports(maxRetained: 2);

        // Assert
        var remainingReports = service.GetCrashReports();
        remainingReports.Should().HaveCountLessOrEqualTo(2);
    }
}
