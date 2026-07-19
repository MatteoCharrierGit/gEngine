using gEngine.Log;

namespace gEngine.Tests.Log;

/// <summary>
/// Sink che tiene quel che riceve, per poterlo asserire. È il "guardo cosa è stato loggato"
/// dei test, e sta in un file suo perché serve a chiunque verifichi un componente che parla —
/// non solo ai test del logger.
/// </summary>
internal sealed class SpySink : ILogSink
{
    public List<LogMessage> Messaggi { get; } = [];

    public void Write(in LogMessage message)
    {
        Messaggi.Add(message);
    }
}
