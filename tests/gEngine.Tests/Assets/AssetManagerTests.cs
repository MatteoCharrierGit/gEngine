using gEngine.Assets;
using gEngine.Log;
using gEngine.Tests.Log;
using gEngine.Tests.Scenes;

namespace gEngine.Tests.Assets;

/// <summary>
/// L'asset mancante non passa più inosservato.
///
/// ⚠️ È il fallimento silenzioso più caro del progetto, e non è teorico: i binari degli asset
/// sono <b>fuori da git</b>, quindi ogni clone pulito parte già senza modelli e senza audio.
/// raylib in quel caso non lancia — restituisce un handle vuoto — e il gioco si vede senza il
/// modello, si sente in silenzio, e non succede niente che assomigli a un errore.
/// </summary>
public class AssetManagerTests
{
    [Fact]
    public void AssetMancante_Avvisa()
    {
        var (assets, sink, _) = NewAssetManager();

        assets.LoadModel("models/che-non-esiste.glb");

        var messaggio = Assert.Single(sink.Messaggi);
        Assert.Equal(LogLevel.Warning, messaggio.Level);
        Assert.Equal(LogCategories.Assets, messaggio.Category);
    }

    /// <summary>
    /// ⚠️ La metà che fa discriminare il test precedente: senza, passerebbe anche un
    /// <c>AssetManager</c> che avvisa <b>sempre</b> — cioè una console che urla al lupo a ogni
    /// caricamento riuscito, che è un modo diverso di rendere il log inutile.
    /// </summary>
    [Fact]
    public void AssetPresente_NonAvvisa()
    {
        var (assets, sink, root) = NewAssetManager();
        var relPath = ScriviFileVero(root, "models/esiste.glb");

        assets.LoadModel(relPath);

        Assert.Empty(sink.Messaggi);
    }

    /// <summary>
    /// Il path <b>relativo</b> deve stare nel messaggio: è quello che si legge nel file di
    /// scena, quindi è l'unica forma con cui chi legge il log sa cosa andare ad aggiustare.
    /// L'assoluto da solo direbbe dove si è cercato, non cosa correggere.
    /// </summary>
    [Fact]
    public void Avviso_ContieneIlPathRelativo()
    {
        var (assets, sink, _) = NewAssetManager();

        assets.LoadModel("models/lampione/scene.gltf");

        Assert.Contains("models/lampione/scene.gltf", Assert.Single(sink.Messaggi).Message);
    }

    /// <summary>
    /// ⚠️ Un asset mancante chiesto N volte avvisa <b>una volta sola</b>: il secondo giro esce
    /// dalla cache prima di arrivare al controllo. Conta perché un modello mancante viene
    /// richiesto una volta per entità che lo usa — venti lampioni rotti darebbero venti righe
    /// identiche, e una console che ripete venti volte la stessa cosa nasconde le altre
    /// diciannove informazioni che c'erano intorno.
    /// </summary>
    [Fact]
    public void AssetMancante_AvvisaUnaVoltaSola()
    {
        var (assets, sink, _) = NewAssetManager();

        assets.LoadModel("models/che-non-esiste.glb");
        assets.LoadModel("models/che-non-esiste.glb");
        assets.LoadModel("models/che-non-esiste.glb");

        Assert.Single(sink.Messaggi);
    }

    /// <summary>
    /// Vale per tutti i generi, non solo per i modelli: l'audio mancante è il caso vivo
    /// dell'altro asset fuori da git.
    /// </summary>
    [Fact]
    public void AudioMancante_Avvisa()
    {
        var (assets, sink, _) = NewAssetManager();

        assets.LoadMusicStream("audio/che-non-esiste.mp3");

        Assert.Single(sink.Messaggi);
    }

    // ------ HELPER ----------------------------------------------------------------------

    /// <summary>
    /// Radice in una cartella temporanea <b>unica per test</b>: due test che condividessero la
    /// radice condividerebbero anche i file scritti da <see cref="ScriviFileVero"/>, e "l'asset
    /// non c'è" smetterebbe di essere vero a seconda dell'ordine di esecuzione — cioè il test
    /// diventerebbe rosso a caso, che è peggio di non averlo.
    /// </summary>
    private static (AssetManager Assets, SpySink Sink, string Root) NewAssetManager()
    {
        var logger = new Logger();
        var sink = new SpySink();
        logger.AddSink(sink);

        var root = Path.Combine(Path.GetTempPath(), "gEngineTests", Guid.NewGuid().ToString("N"));
        return (new AssetManager(root, "assets", new FakeAssetBackend(), logger), sink, root);
    }

    /// <summary>
    /// Crea un file vero dove l'AssetManager andrà a cercarlo, e ne torna il path relativo.
    ///
    /// ⚠️ La composizione <c>root/assets/relPath</c> è ricopiata da <c>AssetManager</c>, ed è
    /// l'unico modo: la radice risolta è un campo privato. Se un giorno quella regola cambia,
    /// questo test diventa rosso — ed è il verso giusto, perché rosso lo si guarda.
    /// </summary>
    private static string ScriviFileVero(string root, string relPath)
    {
        var fullPath = Path.Combine(root, "assets", relPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "non e' un modello vero: al backend finto non interessa");

        return relPath;
    }
}
