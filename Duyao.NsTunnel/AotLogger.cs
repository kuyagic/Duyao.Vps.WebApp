namespace Duyao.NsTunnel;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Fatal
}

public static class AotSimpleLogger
{
    private static int MinLog = 0;
    public static void SetLogLevel(int level){MinLog = level;}
    private static readonly Dictionary<LogLevel, (ConsoleColor, string)> LevelConfig = new()
    {
        { LogLevel.Debug, (ConsoleColor.Gray, "DBG") },
        { LogLevel.Info, (ConsoleColor.Green, "INF") },
        { LogLevel.Warning, (ConsoleColor.Yellow, "WRN") },
        { LogLevel.Error, (ConsoleColor.Red, "ERR") },
        { LogLevel.Fatal, (ConsoleColor.Magenta, "FTL") }
    };

    private static void Log(LogLevel level, string message)
    {
        if ((int)level < MinLog)
        {
            return;
        }
        var (color, levelName) = LevelConfig[level];
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logLine = $"[{timestamp}][{Thread.CurrentThread.ManagedThreadId}][{levelName}] {message}";

        Console.ForegroundColor = color;
        Console.WriteLine(logLine);
        Console.ResetColor();
    }

    public static void Debug(string message) => Log(LogLevel.Debug, message);
    public static void Info(string message) => Log(LogLevel.Info, message);
    public static void Warning(string message) => Log(LogLevel.Warning, message);
    public static void Error(string message) => Log(LogLevel.Error, message);
    public static void Fatal(string message) => Log(LogLevel.Fatal, message);
}