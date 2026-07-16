using gEngine.Assets;
using gEngine.Ecs.Base;
using gEngine.Scenes;

namespace gEngine.Editor;

/// <summary>
/// La scena su cui l'editor sta lavorando: il file, come leggerlo/scriverlo, e da cosa
/// veniva. È il pezzo che l'editor <b>non può indovinare</b> — il registry dei componenti
/// lo popola il gioco (i suoi componenti custom l'engine non li conosce) e il path della
/// scena pure. Il gioco lo costruisce e lo passa all'<see cref="EditorHost"/>.
///
/// Senza questo, i pannelli avrebbero dovuto avere un riferimento al gioco: qui invece
/// dipendono da quattro dati.
/// </summary>
public sealed class SceneDocument
{
    public required World World { get; init; }
    public required SceneComponentRegistry Registry { get; init; }
    public required AssetManager Assets { get; init; }

    /// <summary>
    /// File da cui si carica e su cui si salva.
    ///
    /// Scrivibile e non <c>init</c> da quando il menu File apre altre scene: aprire <b>è</b>
    /// cambiare il file del documento, e senza questo l'editor potrebbe caricare solo la
    /// scena da cui il gioco è partito.
    ///
    /// ⚠️ Può essere vuota: è come <c>MainMenuBar</c> rappresenta una scena nuova, che un
    /// file non ce l'ha. Non c'è un document model che sappia dire "senza titolo" — chi
    /// legge questa proprietà deve controllare, e <see cref="Save"/> lo fa.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// L'ultima <see cref="Scene"/> letta da disco. Tenuta perché il salvataggio la
    /// rilegge per non buttare via ciò che il World non contiene (i <c>_comment</c>):
    /// vedi <see cref="SceneSerializer.ToScene"/>.
    /// </summary>
    public Scene? Source { get; set; }

    public string Name => Source?.Name ?? "scena";

    public void Save()
    {
        // La guardia sta qui e non nella UI: il "senza file" è uno stato del documento, e chi
        // lo interroga per sapere se può salvare finirebbe per dimenticarsene. L'eccezione è
        // il canale giusto perché chi chiama Save già gestisce quelle del disco (vedi
        // MainMenuBar.Run): un errore in più nello stesso posto, non un ramo nuovo.
        if (string.IsNullOrEmpty(Path))
            throw new InvalidOperationException(
                "Nessun percorso: questa scena non viene da un file. Aprine una esistente.");

        var scene = SceneSerializer.ToScene(World, Registry, Assets, Name, Source);
        JsonSceneLoader.Save(scene, Path);

        // Da adesso il file su disco è la nuova origine: se non lo aggiornassimo, un
        // secondo salvataggio ripescherebbe i commenti dalla versione vecchia.
        Source = scene;
    }

    /// <summary>
    /// Ricarica la scena da disco buttando via il World corrente. Le modifiche non salvate
    /// si perdono: è il senso di "Load".
    /// </summary>
    public void Load()
    {
        var scene = JsonSceneLoader.Load(Path);

        // Prima svuotare, poi istanziare. I corpi fisici delle entità appena distrutte non
        // si liberano qui — se ne accorge il PhysicsSystem al prossimo update, che li
        // trova senza entità e li toglie dalla simulazione.
        World.Clear();
        SceneInstantiator.Instantiate(scene, World, Registry, Assets);

        Source = scene;
    }
}
