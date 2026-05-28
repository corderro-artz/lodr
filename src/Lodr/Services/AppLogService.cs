using Microsoft.Extensions.Logging;

namespace Lodr.Services;

public record LogEntry(
    LogLevel Level,
    string Source,
    string Message,
    DateTime Timestamp
);

public class AppLogService : ILoggerProvider
{
    private const int MaxEntries = 500;
    private readonly Queue<LogEntry> _entries = new();

    public event Action? OnChange;

    public IReadOnlyList<LogEntry> Entries => _entries.ToList();
    public int WarningCount => _entries.Count(e => e.Level >= LogLevel.Warning);

    public ILogger CreateLogger(string categoryName) =>
        new AppLogger(categoryName, this);

    public void AddEntry(LogLevel level, string source, string message)
    {
        if (_entries.Count >= MaxEntries) _entries.Dequeue();
        _entries.Enqueue(new LogEntry(level, source, message, DateTime.UtcNow));

        if (level >= LogLevel.Warning)
            Console.Error.WriteLine($"[{level}] {source}: {message}");

        OnChange?.Invoke();
    }

    public string ExportText() =>
        string.Join('\n', _entries.Select(e =>
            $"[{e.Timestamp:HH:mm:ss}] [{e.Level}] {e.Source}: {e.Message}"));

    public void Dispose() { }

    private sealed class AppLogger(string category, AppLogService service) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Information;

        public void Log<TState>(
            LogLevel level, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(level)) return;
            var msg = formatter(state, exception);
            if (exception is not null) msg += $"\n{exception}";
            service.AddEntry(level, category, msg);
        }
    }
}
