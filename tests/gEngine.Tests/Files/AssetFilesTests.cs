using gEngine.Editor.Files;

namespace gEngine.Tests.Files;

/// <summary>
/// Le mutazioni su disco del pannello File system.
///
/// Il grosso di questi test guarda una cosa sola: <b>niente esce dalla radice</b>. Il pannello
/// dice "assets" e deve mantenere la promessa — e queste sono le uniche operazioni dell'editor
/// che toccano file veri dell'utente, cioè le uniche in cui sbagliare non si annulla.
///
/// Girano su una cartella temporanea vera e non su un file system finto: il rischio qui è
/// tutto nella <b>risoluzione dei percorsi</b> (i <c>..</c>, le maiuscole, il separatore
/// finale), che è esattamente la parte che un finto reimplementerebbe a modo suo — e
/// verificherebbe la finzione invece del codice.
/// </summary>
public class AssetFilesTests : IDisposable
{
    private readonly string _root;

    public AssetFilesTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"gengine-files-{Guid.NewGuid():N}", "assets");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        // Il test pulisce il proprio: Directory.Delete e non il cestino, perche' questa e' roba
        // del test in una cartella temporanea, non un file dell'utente.
        var parent = Directory.GetParent(_root)?.FullName;
        if (parent is not null && Directory.Exists(parent))
            Directory.Delete(parent, recursive: true);
    }

    // ------ IL CONFINE: NIENTE ESCE DALLA RADICE ------------------------------------------

    [Theory]
    [InlineData("..")]
    [InlineData("../fuori")]
    [InlineData(@"..\fuori")]
    [InlineData(@"..\..\Windows")]
    [InlineData("/etc")]
    [InlineData(@"C:\Windows")]
    [InlineData("sotto/ancora")]
    public void CreareUnaCartella_ConUnNomeCheEUnPercorso_Rifiuta(string nome)
    {
        var esito = AssetFiles.CreateFolder(_root, _root, nome, out var creato);

        Assert.False(esito.Ok);
        Assert.Equal(string.Empty, creato);

        // ⚠️ Non basta che l'operazione dica di no: bisogna che non abbia creato niente.
        // Un test che guarda solo il valore di ritorno passerebbe anche con una
        // CreateDirectory eseguita e poi dichiarata fallita.
        Assert.Empty(Directory.EnumerateDirectories(_root));
    }

    /// <summary>
    /// Il caso peggiore: mandare al cestino qualcosa che sta fuori dalla cartella asset.
    /// Il fatto che il cestino sia una rete non autorizza a puntarlo altrove.
    /// </summary>
    [Fact]
    public void Eliminare_FuoriDallaRadice_Rifiuta_ENonChiamaIlCestino()
    {
        var trash = new SpyTrash();
        var fuori = Path.Combine(Directory.GetParent(_root)!.FullName, "fuori.txt");
        File.WriteAllText(fuori, "non toccarmi");

        var esito = AssetFiles.Delete(_root, fuori, trash);

        Assert.False(esito.Ok);
        Assert.Empty(trash.Sent);
        Assert.True(File.Exists(fuori));
    }

    [Fact]
    public void Eliminare_LaRadiceStessa_Rifiuta()
    {
        var trash = new SpyTrash();

        var esito = AssetFiles.Delete(_root, _root, trash);

        Assert.False(esito.Ok);
        Assert.Empty(trash.Sent);
    }

    /// <summary>
    /// ⚠️ Il controllo per prefisso di stringa senza separatore finale direbbe che
    /// <c>assets2</c> sta dentro <c>assets</c>. È il modo classico in cui questo controllo
    /// sembra funzionare e non funziona.
    /// </summary>
    [Fact]
    public void UnaCartellaSorellaColNomeCheIniziaUguale_NonEDentroLaRadice()
    {
        var sorella = _root + "2";
        Directory.CreateDirectory(sorella);

        Assert.False(AssetFiles.IsInside(_root, sorella));
        Assert.False(AssetFiles.IsInside(_root, Path.Combine(sorella, "dentro.txt")));
    }

    [Fact]
    public void LaRadiceEDentroSeStessa_EIFigliAncheInProfondita()
    {
        Assert.True(AssetFiles.IsInside(_root, _root));
        Assert.True(AssetFiles.IsInside(_root, Path.Combine(_root, "models", "a", "b.gltf")));
    }

    /// <summary>
    /// Un percorso che <b>risolve</b> dentro la radice va bene anche se scritto con dei
    /// <c>..</c>: si confronta il percorso normalizzato, non la stringa.
    /// </summary>
    [Fact]
    public void UnPercorsoConPuntiPuntiCheRientra_EConsideratoDentro()
    {
        var giroLungo = Path.Combine(_root, "models", "..", "scenes");

        Assert.True(AssetFiles.IsInside(_root, giroLungo));
    }

    // ------ CREARE ------------------------------------------------------------------------

    [Fact]
    public void CreareUnaCartella_LaCrea()
    {
        var esito = AssetFiles.CreateFolder(_root, _root, "materiali", out var creato);

        Assert.True(esito.Ok, esito.Error);
        Assert.True(Directory.Exists(creato));
        Assert.Equal(Path.Combine(_root, "materiali"), creato);
    }

    [Fact]
    public void CreareUnaCartella_ConUnNomeGiaPreso_Rifiuta()
    {
        Directory.CreateDirectory(Path.Combine(_root, "materiali"));

        var esito = AssetFiles.CreateFolder(_root, _root, "materiali", out _);

        Assert.False(esito.Ok);
        Assert.Contains("Esiste", esito.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    public void CreareUnaCartella_ConUnNomeVuotoODegenere_Rifiuta(string nome)
    {
        Assert.False(AssetFiles.CreateFolder(_root, _root, nome, out _).Ok);
    }

    [Fact]
    public void CreareUnaCartella_ToglieGliSpaziAiBordi()
    {
        var esito = AssetFiles.CreateFolder(_root, _root, "  materiali  ", out var creato);

        Assert.True(esito.Ok, esito.Error);
        Assert.Equal("materiali", Path.GetFileName(creato));
    }

    // ------ RINOMINARE --------------------------------------------------------------------

    [Fact]
    public void Rinominare_UnFile_LoRinominaLasciandoloDovEra()
    {
        var file = Path.Combine(_root, "vecchio.json");
        File.WriteAllText(file, "{}");

        var esito = AssetFiles.Rename(_root, file, "nuovo.json");

        Assert.True(esito.Ok, esito.Error);
        Assert.False(File.Exists(file));
        Assert.True(File.Exists(Path.Combine(_root, "nuovo.json")));
    }

    [Fact]
    public void Rinominare_UnaCartellaConDentroDelleCose_SePortaDietroIlContenuto()
    {
        var cartella = Path.Combine(_root, "vecchia");
        Directory.CreateDirectory(cartella);
        File.WriteAllText(Path.Combine(cartella, "dentro.txt"), "ci sono");

        var esito = AssetFiles.Rename(_root, cartella, "nuova");

        Assert.True(esito.Ok, esito.Error);
        Assert.True(File.Exists(Path.Combine(_root, "nuova", "dentro.txt")));
    }

    [Fact]
    public void Rinominare_SopraUnFileCheEsisteGia_Rifiuta_ENonLoSovrascrive()
    {
        var origine = Path.Combine(_root, "a.json");
        var bersaglio = Path.Combine(_root, "b.json");
        File.WriteAllText(origine, "sono a");
        File.WriteAllText(bersaglio, "sono b");

        var esito = AssetFiles.Rename(_root, origine, "b.json");

        Assert.False(esito.Ok);
        Assert.Equal("sono b", File.ReadAllText(bersaglio));
        Assert.True(File.Exists(origine));
    }

    /// <summary>
    /// ⚠️ Su Windows il file system è insensibile alle maiuscole, quindi "esiste già" direbbe
    /// di sì al file stesso e vieterebbe il cambio di sole maiuscole — che è uno dei motivi
    /// più comuni per rinominare.
    /// </summary>
    [Fact]
    public void Rinominare_CambiandoSoloLeMaiuscole_EPermesso()
    {
        var file = Path.Combine(_root, "Texture.png");
        File.WriteAllText(file, "dati");

        var esito = AssetFiles.Rename(_root, file, "texture.png");

        Assert.True(esito.Ok, esito.Error);
        Assert.Single(Directory.EnumerateFiles(_root));
    }

    [Fact]
    public void Rinominare_LaRadice_Rifiuta()
    {
        Assert.False(AssetFiles.Rename(_root, _root, "altro").Ok);
    }

    [Theory]
    [InlineData("../fuori.json")]
    [InlineData(@"..\fuori.json")]
    public void Rinominare_VersoFuoriDallaRadice_Rifiuta(string nuovoNome)
    {
        var file = Path.Combine(_root, "dentro.json");
        File.WriteAllText(file, "{}");

        var esito = AssetFiles.Rename(_root, file, nuovoNome);

        Assert.False(esito.Ok);
        Assert.True(File.Exists(file));
    }

    // ------ ELIMINARE ---------------------------------------------------------------------

    [Fact]
    public void Eliminare_DentroLaRadice_PassaDalCestino()
    {
        var trash = new SpyTrash();
        var file = Path.Combine(_root, "da-buttare.json");
        File.WriteAllText(file, "{}");

        var esito = AssetFiles.Delete(_root, file, trash);

        Assert.True(esito.Ok, esito.Error);
        Assert.Equal(file, Assert.Single(trash.Sent));
    }

    /// <summary>
    /// ⚠️⚠️ Senza cestino non si elimina, e non si elimina <b>davvero</b>: il file deve restare
    /// lì. È la riga che in un pomeriggio di fretta diventa "tanto è lo stesso" — questo test
    /// esiste per far fallire quel pomeriggio.
    /// </summary>
    [Fact]
    public void SenzaCestino_NonSiEliminaAffatto()
    {
        var trash = new SpyTrash { Available = false };
        var file = Path.Combine(_root, "da-buttare.json");
        File.WriteAllText(file, "{}");

        var esito = AssetFiles.Delete(_root, file, trash);

        Assert.False(esito.Ok);
        Assert.True(File.Exists(file));
        Assert.Empty(trash.Sent);
    }

    /// <summary>
    /// I messaggi finiscono sotto ImGui, che col font di default copre solo Latin-1: tutto il
    /// resto esce come '?'. Si controllano scandendo, non rileggendo.
    /// </summary>
    [Fact]
    public void IMessaggiDErrore_StannoDentroLatin1()
    {
        var trash = new SpyTrash { Available = false };

        var messaggi = new[]
        {
            AssetFiles.CreateFolder(_root, _root, "../fuori", out _).Error,
            AssetFiles.CreateFolder(_root, _root, "", out _).Error,
            AssetFiles.CreateFolder(_root, _root, "a\0b", out _).Error,
            AssetFiles.Rename(_root, _root, "altro").Error,
            AssetFiles.Delete(_root, _root, new SpyTrash()).Error,
            AssetFiles.Delete(_root, Path.Combine(_root, "x"), trash).Error,
            FileTrash.ForCurrentPlatform().UnavailableReason
        };

        Assert.All(messaggi, messaggio => Assert.DoesNotContain(messaggio, c => c > 0xFF));
    }

    /// <summary>Un cestino che annota invece di buttare: il test non tocca il cestino vero.</summary>
    private sealed class SpyTrash : IFileTrash
    {
        public bool Available { get; init; } = true;

        public string UnavailableReason => Available ? string.Empty : "Nessun cestino qui.";

        public List<string> Sent { get; } = [];

        public FileResult Send(string absolutePath)
        {
            Sent.Add(absolutePath);
            return FileResult.Success;
        }
    }
}
