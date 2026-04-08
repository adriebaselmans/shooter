using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace MapEditor.App.Services;

/// <summary>Writes session-scoped diagnostic entries to a unique log file for the current app run.</summary>
public sealed class SessionLogService
{
    private readonly object _sync = new();

    public SessionLogService()
        : this(GetDefaultLogDirectory(), DateTimeOffset.Now)
    {
    }

    internal SessionLogService(string logDirectory, DateTimeOffset sessionStart)
    {
        Directory.CreateDirectory(logDirectory);

        SessionStart = sessionStart;
        LogFilePath = Path.Combine(
            logDirectory,
            $"mapeditor-{sessionStart:yyyyMMdd-HHmmss-fff}.log");

        File.WriteAllText(
            LogFilePath,
            $"Session started {sessionStart:O}{Environment.NewLine}",
            Encoding.UTF8);
    }

    public DateTimeOffset SessionStart { get; }

    public string LogFilePath { get; }

    public string LogFileName => Path.GetFileName(LogFilePath);

    public void WriteInfo(string message) =>
        Append("INFO", message);

    public void WriteException(string source, Exception exception) =>
        WriteException(source, exception, null);

    public void WriteException(string source, Exception? exception, string? details)
    {
        var builder = new StringBuilder()
            .Append("Source: ")
            .AppendLine(source);

        if (!string.IsNullOrWhiteSpace(details))
        {
            builder.Append("Details: ")
                .AppendLine(details);
        }

        if (exception is null)
        {
            builder.AppendLine("Exception: <none>");
        }
        else
        {
            builder.Append("Exception: ")
                .AppendLine(exception.GetType().FullName);
            builder.Append("Message: ")
                .AppendLine(exception.Message);
            builder.AppendLine("StackTrace:");
            builder.AppendLine(exception.ToString());
        }

        Append("ERROR", builder.ToString().TrimEnd());
    }

    private void Append(string level, string message)
    {
        var entry = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}");

        try
        {
            lock (_sync)
            {
                File.AppendAllText(LogFilePath, entry, Encoding.UTF8);
            }
        }
        catch (IOException ioException)
        {
            Debug.WriteLine($"Failed to write log entry to '{LogFilePath}': {ioException}");
        }
        catch (UnauthorizedAccessException accessException)
        {
            Debug.WriteLine($"Failed to write log entry to '{LogFilePath}': {accessException}");
        }
    }

    private static string GetDefaultLogDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MapEditor",
            "Logs");
}
