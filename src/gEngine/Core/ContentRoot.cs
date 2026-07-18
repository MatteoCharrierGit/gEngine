namespace gEngine.Core;

/// <summary>
/// Dove stanno gli asset <b>adesso</b>: la cartella del progetto mentre si sviluppa, quella
/// accanto all'eseguibile in un gioco spedito.
///
/// Nasce da un problema concreto e più grosso di come si presentava. Il csproj copia
/// <c>assets/**</c> nell'output (<c>CopyToOutputDirectory</c>), quindi l'eseguibile leggeva una
/// <b>copia</b> fatta al momento della build. Le conseguenze erano due, e la seconda è peggiore
/// della prima:
/// <list type="number">
///   <item>un file messo nella cartella del progetto <b>non si vedeva</b> — e non bastava
///   riavviare, serviva ricompilare. Il pannello File system non aveva colpa: rilegge il disco
///   a ogni frame, ma stava guardando l'altra cartella;</item>
///   <item><b>il Salva dell'editor scriveva dentro <c>bin/</c></b>. Il lavoro d'autore finiva
///   nell'output di compilazione invece che nel file versionato — cioè si perdeva alla prima
///   pulizia, e intanto git non vedeva niente.</item>
/// </list>
///
/// La regola adottata è quella di Unity: <b>la cartella di progetto È la cartella asset</b>.
/// In sviluppo si risale dall'eseguibile fino a trovare la cartella del progetto, e si lavora
/// lì. La copia nell'output resta e serve ancora: è quella che un gioco spedito si porta dietro,
/// dove un <c>.csproj</c> non esiste e la ricerca fallisce da sé.
///
/// ⚠️ Il valore si calcola <b>una volta</b>: cambiare radice a metà sessione vorrebbe dire
/// handle di asset che puntano a due alberi diversi.
/// </summary>
public static class ContentRoot
{
    private static readonly Lazy<string> Resolved = new(Resolve);

    /// <summary>
    /// La cartella che <b>contiene</b> <c>assets/</c>. Non è la cartella asset: chi la usa ci
    /// compone dentro il nome, com'è sempre stato con <c>AppContext.BaseDirectory</c> — così
    /// resta un rimpiazzo alla pari nei punti che già lo facevano.
    /// </summary>
    public static string Path => Resolved.Value;

    /// <summary>
    /// Risale da <c>bin/Debug/netX.0/</c> cercando una cartella che abbia <b>sia</b> un
    /// <c>.csproj</c> <b>sia</b> una <c>assets/</c>.
    ///
    /// ⚠️ Servono entrambi. Solo il <c>.csproj</c> troverebbe un progetto qualunque risalendo
    /// (in una soluzione ce ne sono diversi); solo <c>assets/</c> potrebbe agganciare la copia
    /// nell'output, che è esattamente quella da cui si sta scappando.
    ///
    /// Il limite di risalita non è prudenza: senza, in un percorso strano si arriverebbe alla
    /// radice del disco cercando cartelle a ogni livello.
    /// </summary>
    private static string Resolve()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        for (var depth = 0; depth < 8 && directory is not null; depth++, directory = directory.Parent)
        {
            if (directory.GetFiles("*.csproj").Length > 0 &&
                directory.GetDirectories("assets").Length > 0)
                return directory.FullName;
        }

        // Nessun progetto sopra di noi: è un gioco spedito (o un test), e la cartella giusta è
        // quella accanto all'eseguibile. Non è un ripiego, è l'altro caso previsto.
        return AppContext.BaseDirectory;
    }
}
