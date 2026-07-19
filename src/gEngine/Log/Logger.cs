namespace gEngine.Log;

/// <summary>
/// Il logger dell'engine: applica la soglia di livello <b>una volta</b> e passa il messaggio a
/// tutti i <see cref="ILogSink"/> registrati.
///
/// PERCHÉ esiste, dato che <c>ConsoleLogger</c> faceva già tutto da solo: perché faceva tutto
/// da solo. Chi logga non deve sapere quanti sono i destinatari — il giorno in cui la console
/// in-editor si aggiunge allo stdout, nessuna riga che chiama <c>Info(...)</c> deve cambiare.
/// È lo stesso ports &amp; adapters di renderer, asset e fisica, applicato al log: qui la porta
/// di uscita è <see cref="ILogSink"/>.
///
/// ⚠️ <b>Senza sink non lancia e non avvisa</b>, per scelta: un logger è un servizio
/// trasversale e far fallire il gioco perché nessuno ascolta sarebbe sproporzionato. È il
/// verso sicuro dello sbaglio, ma è anche il modo in cui "non vedo i miei log" diventa un
/// mistero — se succede, il primo posto da guardare è <b>chi ha chiamato
/// <see cref="AddSink"/></b>, non il chiamante di <c>Info</c>.
///
/// ⚠️ <b>Non è thread-safe, ed è voluto</b>: il gioco è a thread singolo (il game loop di
/// raylib) e i sink si registrano al setup. Il giorno che qualcosa logga da un thread di
/// lavoro, questa è la classe da cambiare — non i chiamanti.
/// </summary>
public sealed class Logger : ILogger
{
    private readonly List<ILogSink> _sinks = [];

    public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

    /// <summary>
    /// Aggiunge un destinatario.
    ///
    /// ⚠️ Un sink registrato <b>dopo</b> non vede quel che è già passato: questo logger non
    /// tiene storia (la soglia è una regola sola e sta qui, la storia è un bisogno di chi
    /// guarda e sta in <see cref="LogHistory"/>). Chi vuole vedere anche l'avvio si registra
    /// prima di <c>InitWindow</c>, come fa il <c>GameLoop</c>.
    ///
    /// ⚠️ <b>Non esiste un <c>RemoveSink</c></b>, ed è voluto: c'era, non lo chiamava nessuno,
    /// ed è stato tolto. Era stato scritto immaginando un pannello che si registrasse e si
    /// sregistrasse aprendosi e chiudendosi — poi la console si è rivelata un <i>lettore</i>
    /// di <see cref="LogHistory"/>, non un sink, e quel bisogno non è mai esistito. Oggi i sink
    /// li attacca il <c>GameLoop</c> all'avvio e vivono quanto il processo. Il giorno che
    /// servisse davvero, sono tre righe — ma vanno scritte con un chiamante vero davanti.
    /// </summary>
    public void AddSink(ILogSink sink)
    {
        _sinks.Add(sink);
    }

    public void Log(LogLevel level, string category, string message)
    {
        if (level < MinimumLevel)
            return;

        var log = new LogMessage(DateTime.Now, level, message, category);

        // Indicizzato e non foreach: un sink che loggasse mentre lo si serve (una console che
        // si lamenta di un messaggio malformato) muoverebbe la lista sotto un enumeratore, e
        // l'eccezione uscirebbe dal punto sbagliato del programma.
        for (var i = 0; i < _sinks.Count; i++)
            _sinks[i].Write(in log);
    }

    public void Debug(string category, string message)
        => Log(LogLevel.Debug, category, message);

    public void Info(string category, string message)
        => Log(LogLevel.Info, category, message);

    public void Warn(string category, string message)
        => Log(LogLevel.Warning, category, message);

    public void Error(string category, string message)
        => Log(LogLevel.Error, category, message);
}
