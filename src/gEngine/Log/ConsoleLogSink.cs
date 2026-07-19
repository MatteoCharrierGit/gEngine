namespace gEngine.Log;

/// <summary>
/// Il sink che scrive sullo <b>stdout</b> del processo, colorato per livello.
///
/// ⚠️ "Console" qui è <c>System.Console</c>, <b>non</b> la console in-editor: quella è un
/// pannello, quindi un sink diverso registrato sullo stesso <see cref="Logger"/>. Il tipo si
/// chiamava <c>ConsoleLogger</c> e il nome sarebbe diventato una trappola nel momento esatto in
/// cui esistono due cose chiamate "console" — di cui una sola è questa.
/// </summary>
public sealed class ConsoleLogSink : ILogSink
{
    public void Write(in LogMessage message)
    {
        Console.ForegroundColor = message.Level switch
        {
            LogLevel.Debug => ConsoleColor.Gray,
            LogLevel.Info => ConsoleColor.White,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,

            _ => ConsoleColor.White
        };

        Console.WriteLine(
            $"[{message.Timestamp:HH:mm:ss}] [{message.Level}] [{message.Category}] {message.Message}"
        );

        Console.ResetColor();
    }
}
