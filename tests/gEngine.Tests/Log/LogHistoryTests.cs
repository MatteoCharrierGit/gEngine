using gEngine.Log;

namespace gEngine.Tests.Log;

/// <summary>
/// Il buffer che ricorda, cioè ciò su cui si appoggia la console dell'editor.
///
/// Due comportamenti sbagliabili in silenzio: <b>cosa si butta via</b> quando è pieno (buttare
/// il più recente invece del più vecchio darebbe una console che smette di aggiornarsi, e
/// sembrerebbe un gioco fermo) e <b>come si conta un errore nuovo</b>.
/// </summary>
public class LogHistoryTests
{
    [Fact]
    public void Tiene_IMessaggiInOrdine()
    {
        var (logger, history) = NewHistory(capacity: 10);

        logger.Info(LogCategories.Engine, "primo");
        logger.Info(LogCategories.Engine, "secondo");
        logger.Info(LogCategories.Engine, "terzo");

        Assert.Equal(
            ["primo", "secondo", "terzo"],
            history.Messages.Select(m => m.Message));
    }

    /// <summary>
    /// ⚠️ Il test che dice <b>quale</b> messaggio si perde. Senza, passerebbe anche un buffer
    /// che scarta l'ultimo arrivato invece del più vecchio: il conteggio resterebbe giusto e
    /// la console sembrerebbe congelata su messaggi vecchi, che assomiglia a un gioco piantato.
    /// </summary>
    [Fact]
    public void OltreLaCapacita_EsceIlPiuVecchio()
    {
        var (logger, history) = NewHistory(capacity: 3);

        logger.Info(LogCategories.Engine, "uno");
        logger.Info(LogCategories.Engine, "due");
        logger.Info(LogCategories.Engine, "tre");
        logger.Info(LogCategories.Engine, "quattro");

        Assert.Equal(
            ["due", "tre", "quattro"],
            history.Messages.Select(m => m.Message));
    }

    [Fact]
    public void NonSupera_MaiLaCapacita()
    {
        var (logger, history) = NewHistory(capacity: 5);

        for (var i = 0; i < 100; i++)
            logger.Info(LogCategories.Engine, $"messaggio {i}");

        Assert.Equal(5, history.Messages.Count);
    }

    /// <summary>
    /// ⚠️ Il conteggio degli errori è <b>monotòno</b> e non "quanti ce ne sono ora": la console
    /// lo usa per accorgersi di un errore <i>nuovo</i>. Se scendesse quando il buffer gira,
    /// il pannello si riaprirebbe da sé per un errore già visto.
    /// </summary>
    [Fact]
    public void TotalErrors_NonScendeQuandoIlBufferGira()
    {
        var (logger, history) = NewHistory(capacity: 2);

        logger.Error(LogCategories.Engine, "primo guasto");
        logger.Info(LogCategories.Engine, "riempi");
        logger.Info(LogCategories.Engine, "riempi");
        logger.Info(LogCategories.Engine, "riempi");

        // L'errore e' uscito dal buffer...
        Assert.DoesNotContain(history.Messages, m => m.Level == LogLevel.Error);
        // ...ma il fatto che sia successo no.
        Assert.Equal(1, history.TotalErrors);
    }

    /// <summary>
    /// ⚠️ <c>Clear</c> svuota il buffer ma <b>non</b> azzera il conteggio: azzerandolo, un
    /// "Pulisci" farebbe riscattare l'apertura automatica della console al primo errore
    /// successivo come se fosse il primo di sempre.
    /// </summary>
    [Fact]
    public void Clear_SvuotaIlBufferMaNonIlConteggioDegliErrori()
    {
        var (logger, history) = NewHistory(capacity: 10);
        logger.Error(LogCategories.Engine, "guasto");

        history.Clear();

        Assert.Empty(history.Messages);
        Assert.Equal(1, history.TotalErrors);
    }

    [Fact]
    public void ContaSoloGliErrori_NonGliAvvisi()
    {
        var (logger, history) = NewHistory(capacity: 10);

        logger.Warn(LogCategories.Engine, "avviso");
        logger.Info(LogCategories.Engine, "info");
        logger.Error(LogCategories.Engine, "guasto");

        Assert.Equal(1, history.TotalErrors);
    }

    private static (Logger Logger, LogHistory History) NewHistory(int capacity)
    {
        var logger = new Logger();
        var history = new LogHistory(capacity);
        logger.AddSink(history);

        return (logger, history);
    }
}
