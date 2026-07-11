using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using MediaSorter.Models;
using System.Text.Json;

namespace MediaSorter.Services.Logging;

public class FileLoggerService : ILoggerService
{
    private readonly string _logFilePath;
    private readonly string _logDir;
    private readonly object _lock = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };
    private const long MaxLogSize = 10L * 1024 * 1024; // 10 MB
    private static readonly TimeSpan LogRetention = TimeSpan.FromDays(14);

    public FileLoggerService()
    {
        _logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MediaSorter", "logs");
        Directory.CreateDirectory(_logDir);
        
        var fileName = $"MediaSorter_{DateTime.Now:yyyyMMdd}.log";
        _logFilePath = Path.Combine(_logDir, fileName);

        CleanupOldLogs();
    }

    private void CleanupOldLogs()
    {
        try
        {
            var cutoff = DateTime.UtcNow - LogRetention;
            foreach (var file in Directory.GetFiles(_logDir, "MediaSorter_*.log"))
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to clean old logs: {ex.Message}");
        }
    }

    public event Action<LogEntry>? EntryAdded;

    public void Log(string message) => Log(new LogEntry(DateTime.Now, LogLevel.Information, message));
    public void LogError(string message) => Log(new LogEntry(DateTime.Now, LogLevel.Error, message));
    public void LogWarning(string message) => Log(new LogEntry(DateTime.Now, LogLevel.Warning, message));
    public void LogInformation(string message) => Log(new LogEntry(DateTime.Now, LogLevel.Information, message));

    public void Log(LogEntry entry)
    {
        try
        {
            var json = JsonSerializer.Serialize(entry, _jsonOptions);
            lock (_lock)
            {
                var fileInfo = new FileInfo(_logFilePath);
                if (fileInfo.Exists && fileInfo.Length > MaxLogSize)
                {
                    var archivePath = _logFilePath + ".old";
                    if (File.Exists(archivePath))
                        File.Delete(archivePath);
                    File.Move(_logFilePath, archivePath);
                }

                File.AppendAllText(_logFilePath, json + Environment.NewLine);
            }
            EntryAdded?.Invoke(entry);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to write log entry: {ex.Message}");
        }
    }
}