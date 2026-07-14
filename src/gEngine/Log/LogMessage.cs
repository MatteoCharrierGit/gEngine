namespace gEngine.Log;

public readonly record struct LogMessage (
        DateTime Timestamp,
        LogLevel Level,
        string Message,
        string Category
    );