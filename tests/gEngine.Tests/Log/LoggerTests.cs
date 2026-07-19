using gEngine.Core;
using gEngine.Log;

namespace gEngine.Tests.Log;

/// <summary>
/// Il logger a più destinatari, che è il prerequisito della console in-editor.
///
/// Perché è la prima cosa testata di questo pezzo: il logger era <b>codice morto</b> — un
/// campo di <c>GameLoop</c> che nessuno leggeva, e <c>LogCategories</c> mai citato. Un servizio
/// che nessuno usa non ha modo di regredire; uno che scrive su due sink e ne perde uno regredisce
/// <b>in silenzio</b>, perché "il log si vede" resta vero guardando l'altro.
/// </summary>
public class LoggerTests
{
    /// <summary>
    /// La ragione di esistere dell'intera classe: due destinatari, un messaggio solo, e chi
    /// logga che non sa quanti sono. Con lo stdout e la console dell'editor insieme, questo è
    /// il caso vivo.
    /// </summary>
    [Fact]
    public void Log_ArrivaATuttiISink()
    {
        var logger = new Logger();
        var primo = new SpySink();
        var secondo = new SpySink();
        logger.AddSink(primo);
        logger.AddSink(secondo);

        logger.Info(LogCategories.Engine, "ciao");

        Assert.Single(primo.Messaggi);
        Assert.Single(secondo.Messaggi);
    }

    [Fact]
    public void Log_PortaLivelloCategoriaETesto()
    {
        var logger = new Logger();
        var sink = new SpySink();
        logger.AddSink(sink);

        logger.Warn(LogCategories.Physics, "corpo orfano");

        var messaggio = Assert.Single(sink.Messaggi);
        Assert.Equal(LogLevel.Warning, messaggio.Level);
        Assert.Equal(LogCategories.Physics, messaggio.Category);
        Assert.Equal("corpo orfano", messaggio.Message);
    }

    /// <summary>
    /// ⚠️ Il test che discrimina davvero: senza la seconda metà passerebbe anche con la soglia
    /// ignorata del tutto (un logger che lascia passare tutto supera "l'Error arriva"). Le due
    /// asserzioni insieme dicono che la soglia <b>c'è ed è quella</b>.
    /// </summary>
    [Fact]
    public void MinimumLevel_ScartaSottoSogliaELasciaPassareSopra()
    {
        var logger = new Logger { MinimumLevel = LogLevel.Warning };
        var sink = new SpySink();
        logger.AddSink(sink);

        logger.Debug(LogCategories.Engine, "scartato");
        logger.Info(LogCategories.Engine, "scartato");
        logger.Warn(LogCategories.Engine, "passa");
        logger.Error(LogCategories.Engine, "passa");

        Assert.Equal(2, sink.Messaggi.Count);
        Assert.All(sink.Messaggi, m => Assert.True(m.Level >= LogLevel.Warning));
    }

    /// <summary>
    /// La soglia è <b>una sola</b> e sta nel logger: i sink ricevono già filtrato. Se un giorno
    /// qualcuno rifiltrasse per sink, due pannelli mostrerebbero cose diverse e non ci sarebbe
    /// un posto dove leggere qual è la regola.
    /// </summary>
    [Fact]
    public void MinimumLevel_FiltraUnaVoltaSolaPerTuttiISink()
    {
        var logger = new Logger { MinimumLevel = LogLevel.Error };
        var primo = new SpySink();
        var secondo = new SpySink();
        logger.AddSink(primo);
        logger.AddSink(secondo);

        logger.Info(LogCategories.Engine, "scartato");

        Assert.Empty(primo.Messaggi);
        Assert.Empty(secondo.Messaggi);
    }

    /// <summary>
    /// Serve a chi ha vita più corta del gioco: un pannello che si chiude. Senza, il logger lo
    /// terrebbe vivo e continuerebbe a scriverci dentro.
    /// </summary>
    [Fact]
    public void RemoveSink_SmetteDiRicevere()
    {
        var logger = new Logger();
        var sink = new SpySink();
        logger.AddSink(sink);
        logger.Info(LogCategories.Engine, "prima");

        Assert.True(logger.RemoveSink(sink));
        logger.Info(LogCategories.Engine, "dopo");

        var messaggio = Assert.Single(sink.Messaggi);
        Assert.Equal("prima", messaggio.Message);
    }

    /// <summary>
    /// ⚠️ Senza sink <b>non lancia</b>, ed è una scelta: far cadere il gioco perché nessuno
    /// ascolta i log sarebbe sproporzionato. Il test c'è perché è anche il modo in cui "non
    /// vedo i miei log" diventa un mistero — se qualcuno un giorno lo rende fatale, questo
    /// test glielo dice invece di lasciarglielo scoprire a runtime.
    /// </summary>
    [Fact]
    public void SenzaSink_NonLancia()
    {
        var logger = new Logger();

        logger.Error(LogCategories.Engine, "nel vuoto");
    }

    /// <summary>
    /// ⚠️ Documenta un <b>limite</b>, non una funzione: il logger non tiene storia, quindi un
    /// sink registrato dopo non vede quel che è già passato. Conta per la console dell'editor,
    /// che si apre a partita avviata: o si registra al setup, o la storia se la tiene lei.
    /// </summary>
    [Fact]
    public void SinkAggiuntoDopo_NonVedeLaStoria()
    {
        var logger = new Logger();
        logger.Info(LogCategories.Engine, "prima che qualcuno ascoltasse");

        var tardivo = new SpySink();
        logger.AddSink(tardivo);

        Assert.Empty(tardivo.Messaggi);
    }

    /// <summary>
    /// ⚠️ La trappola vera del collegamento: <c>Resources.Add</c> usa <c>typeof(T)</c> e non il
    /// tipo dell'istanza. Registrare con <c>Add(logger)</c> lo metterebbe sotto <c>Logger</c>, e
    /// ogni <c>Get&lt;ILogger&gt;()</c> — compreso quello di <c>ScriptDiscovery</c> quando
    /// riempie il costruttore di un system — fallirebbe. Il test fissa il verso giusto.
    /// </summary>
    [Fact]
    public void RegistratoSottoLaPorta_SiRileggeComeILogger()
    {
        var resources = new Resources();
        var logger = new Logger();
        var sink = new SpySink();
        logger.AddSink(sink);

        resources.Add<ILogger>(logger);

        resources.Get<ILogger>().Info(LogCategories.Engine, "via");
        Assert.Single(sink.Messaggi);
    }
}
