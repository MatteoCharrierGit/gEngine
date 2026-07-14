namespace gEngine.Log;

public interface ILogger
{
    LogLevel MinimumLevel { get; set; }
    
    void Log(LogLevel level, string category, string message);
    
    void Debug(string category, string message);
    void Info(string category, string message);
    void Warn(string category, string message);
    void Error(string category, string message);
}