using Microsoft.Extensions.Logging;
using MediaSorter.Models;
using System.Collections.Concurrent;

namespace MediaSorter.Services.Logging;

public class UiLoggerService : ILoggerService
{
    private readonly int _maxEntries;
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private readonly object _lock = new();

    public UiLoggerService(int maxEntries = 5000)
    {
        _maxEntries = maxEntries;
    }

    public event Action<LogEntry>? EntryAdded;

    public void Log(string message) => Log(new LogEntry(DateTime.Now, LogLevel.Information, message));
    public void LogError(string message) => Log(new LogEntry(DateTime.Now, LogLevel.Error, message));
    public void LogWarning(string message) => Log(new LogEntry(DateTime.Now, LogLevel.Warning, message));
    public void LogInformation(string message) => Log(new LogEntry(DateTime.Now, LogLevel.Information, message));

    public void Log(LogEntry entry)
    {
        lock (_lock)
        {
            _entries.Enqueue(entry);
            
            while (_entries.Count > _maxEntries)
            {
                _entries.TryDequeue(out _);
            }
            
            EntryAdded?.Invoke(entry);
        }
    }

    public IReadOnlyList<LogEntry> GetEntries()
    {
        lock (_lock)
        {
            return _entries.ToArray();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            while (_entries.TryDequeue(out _)) { }
        }
    }
}