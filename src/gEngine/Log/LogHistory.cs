namespace gEngine.Log;

/// <summary>
/// Il sink che <b>ricorda</b>: tiene gli ultimi N messaggi, così chi arriva dopo può ancora
/// leggerli. È ciò su cui si appoggia la console dell'editor.
///
/// PERCHÉ non sta dentro <see cref="Logger"/>, che sarebbe stato più corto: perché la soglia è
/// una regola <b>sola</b> e la storia è un bisogno di <b>chi guarda</b>. Un buffer nel logger
/// avrebbe imposto una dimensione a tutti per comodo di uno, e avrebbe fatto pagare la memoria
/// anche a un gioco spedito che i log li scrive e basta.
///
/// ⚠️ <b>Sta nell'engine e non nell'editor, e non è un caso.</b> Il pannello si costruisce
/// quando l'editor parte, cioè dentro <c>IGame.Init</c> — a quel punto la finestra è già stata
/// aperta e ha già loggato. Se la storia nascesse col pannello, la console mostrerebbe tutto
/// <i>tranne</i> l'avvio, che è il tratto di vita in cui è più probabile che qualcosa sia
/// andato storto. Vive qui perché il <c>GameLoop</c> possa attaccarla <b>prima</b> di
/// <c>InitWindow</c>, insieme al logger.
///
/// ⚠️ Non è thread-safe, come <see cref="Logger"/> e per lo stesso motivo.
/// </summary>
/// <param name="capacity">
/// Quanti messaggi tenere. Oltre, il più vecchio esce.
///
/// PERCHÉ un limite e non "tienili tutti": una lista senza tetto è una perdita di memoria con
/// un altro nome — lenta abbastanza da non vedersi in una sessione di prova e sicura in una
/// partita lunga. Il default è largo per un log a eventi (l'engine parla quando succede
/// qualcosa, non per frame) e piccolo in memoria: 500 messaggi sono qualche decina di KB.
/// </param>
public sealed class LogHistory(int capacity = 500) : ILogSink
{
    private readonly Queue<LogMessage> _messages = new();

    public int Capacity { get; } = capacity;

    /// <summary>
    /// I messaggi tenuti, dal più vecchio al più recente.
    ///
    /// ⚠️ È la coda viva, non una copia: chi la scorre mentre qualcuno logga si prende
    /// l'eccezione di collezione modificata. In pratica non succede — si legge dal disegno di
    /// un frame e si scrive dal gioco, che è lo stesso thread e non sono mai in mezzo l'uno
    /// all'altro — ma è il genere di cosa che, quando cambia, cambia in silenzio.
    /// </summary>
    public IReadOnlyCollection<LogMessage> Messages => _messages;

    /// <summary>
    /// Quanti <see cref="LogLevel.Error"/> sono passati <b>da sempre</b>, non quanti ce ne
    /// sono adesso nel buffer.
    ///
    /// Serve a chi vuole accorgersi di un errore <b>nuovo</b>: contare quelli presenti non
    /// funziona, perché un errore vecchio esce dalla coda quando la coda gira e il conteggio
    /// scenderebbe — cioè "sono aumentati" tornerebbe vero due volte per lo stesso errore. È
    /// monotòno apposta.
    /// </summary>
    public int TotalErrors { get; private set; }

    public void Write(in LogMessage message)
    {
        if (message.Level == LogLevel.Error)
            TotalErrors++;

        _messages.Enqueue(message);

        // Uno solo: si entra da un Enqueue alla volta, quindi non si accumula mai un arretrato.
        if (_messages.Count > Capacity)
            _messages.Dequeue();
    }

    /// <summary>
    /// Svuota il buffer. ⚠️ Non azzera <see cref="TotalErrors"/>: quello conta cosa è successo,
    /// non cosa è ancora visibile — e azzerandolo un "pulisci" farebbe riscattare l'apertura
    /// automatica della console al primo errore successivo, come se fosse il primo di sempre.
    /// </summary>
    public void Clear()
    {
        _messages.Clear();
    }
}
