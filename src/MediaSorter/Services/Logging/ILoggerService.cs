using Microsoft.Extensions.Logging;
using MediaSorter.Models;

namespace MediaSorter.Services.Logging;

public interface ILoggerService
{
    void Log(string message);
    void LogError(string message);
    void LogWarning(string message);
    void LogInformation(string message);
    
    event Action<LogEntry>? EntryAdded;
}

public record LogEntry(
    DateTime Timestamp,
    LogLevel Level,
    string Message,
    string? FileName = null,
    FileStatus? Status = null,
    DateSource? DateSource = null,
    Exception? Exception = null
)
{
    public string LevelText => Level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRITICAL",
        _ => Level.ToString()
    };
}