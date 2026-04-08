using FluentAssertions;
using MapEditor.App.Services;
using System.IO;

namespace MapEditor.App.Tests;

public sealed class SessionLogServiceTests
{
    [Fact]
    public void Constructor_CreatesTimestampedLogFile()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            var service = new SessionLogService(
                tempDirectory,
                new DateTimeOffset(2026, 4, 8, 14, 55, 6, TimeSpan.Zero));

            File.Exists(service.LogFilePath).Should().BeTrue();
            service.LogFileName.Should().Be("mapeditor-20260408-145506-000.log");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void WriteException_AppendsExceptionDetails()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            var service = new SessionLogService(
                tempDirectory,
                new DateTimeOffset(2026, 4, 8, 14, 55, 6, TimeSpan.Zero));

            service.WriteException("TestSource", new InvalidOperationException("boom"));

            string contents = File.ReadAllText(service.LogFilePath);
            contents.Should().Contain("TestSource");
            contents.Should().Contain("InvalidOperationException");
            contents.Should().Contain("boom");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mapeditor-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
