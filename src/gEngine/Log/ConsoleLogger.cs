namespace gEngine.Log;

public sealed class ConsoleLogger : ILogger
{
    public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;
    
    public void Log(LogLevel level, string category, string message)
    {
        if (level < MinimumLevel)
            return;

        var log = new LogMessage(
            DateTime.Now,
            level,
            category,
            message
        );

        Write(log);
    }

    public void Debug(string category, string message)
        => Log(LogLevel.Debug, category, message);

    public void Info(string category, string message)
        =>  Log(LogLevel.Info, category, message);

    public void Warn(string category, string message)
        =>  Log(LogLevel.Warning, category, message);

    public void Error(string category, string message)
        =>  Log(LogLevel.Error, category, message);

    private void Write(LogMessage log)
    {
        Console.ForegroundColor = log.Level switch
        {
            LogLevel.Debug => ConsoleColor.Gray,
            LogLevel.Info => ConsoleColor.White,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,

            _ => ConsoleColor.White
        };

        Console.WriteLine(
            $"[{log.Timestamp:HH:mm:ss}] [{log.Level}] [{log.Category}] {log.Message}"
        );
        
        Console.ResetColor();
    }
}