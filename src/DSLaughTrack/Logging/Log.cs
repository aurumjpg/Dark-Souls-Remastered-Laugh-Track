namespace DSLaughTrack.Logging;

public enum LogLevel { Debug, Info, Warn, Error }

public sealed class Log
{
    private readonly object _lock = new();
    private readonly string? _filePath;
    public LogLevel MinLevel { get; set; } = LogLevel.Info;

    public Log(string? filePath = null)
    {
        _filePath = filePath;
        if (_filePath is not null)
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_filePath))!);
    }

    public void Debug(string msg) => Write(LogLevel.Debug, msg);
    public void Info(string msg) => Write(LogLevel.Info, msg);
    public void Warn(string msg) => Write(LogLevel.Warn, msg);
    public void Error(string msg) => Write(LogLevel.Error, msg);

    private void Write(LogLevel level, string msg)
    {
        if (level < MinLevel) return;
        var line = $"{DateTime.Now:HH:mm:ss.fff} [{level.ToString().ToUpperInvariant(),-5}] {msg}";
        lock (_lock)
        {
            Console.WriteLine(line);
            if (_filePath is not null) File.AppendAllText(_filePath, line + Environment.NewLine);
        }
    }
}
