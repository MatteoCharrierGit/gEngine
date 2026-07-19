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
    /// I sink registrati, in ordine di registrazione.
    ///
    /// ⚠️ Un sink registrato <b>dopo</b> non vede quel che è già passato: <see cref="Logger"/>
    /// non tiene storia. Chi ne ha bisogno — una console che si apre a metà partita e vuole
    /// mostrare anche l'avvio — se la tiene per conto suo, oppure si registra prima. È la
    /// stessa ragione per cui il filtro sta qui e il buffer no: la soglia è una regola sola,
    /// la storia è un bisogno di chi guarda.
    /// </summary>
    public IReadOnlyList<ILogSink> Sinks => _sinks;

    public void AddSink(ILogSink sink)
    {
        _sinks.Add(sink);
    }

    /// <summary>
    /// Toglie un sink. Serve a chi ha un ciclo di vita più corto del gioco — un pannello
    /// dell'editor che si chiude, per dire: senza, il logger lo terrebbe vivo per sempre e
    /// continuerebbe a scriverci dentro.
    /// </summary>
    public bool RemoveSink(ILogSink sink)
    {
        return _sinks.Remove(sink);
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
