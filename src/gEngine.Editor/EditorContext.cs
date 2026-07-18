using gEngine.Assets;
using gEngine.Core;
using gEngine.Ecs;
using gEngine.Ecs.Base;
using gEngine.Editor.Scripting;
using gEngine.Editor.Undo;
using gEngine.Scenes;

namespace gEngine.Editor;

/// <summary>
/// Stato condiviso fra i pannelli dell'editor. Oggi contiene la selezione e l'infrastruttura
/// del gioco, ma è il posto dove finisce tutto ciò che due pannelli devono vedere allo stesso
/// modo (la Hierarchy sceglie l'entità, l'Inspector la mostra, i gizmi la manipolano).
///
/// Volutamente separato dai pannelli: la selezione sopravvive alla singola finestra e
/// non deve appartenere a nessuna di esse in particolare.
/// </summary>
public class EditorContext
{
    /// <summary>
    /// Entità attualmente selezionata, o <c>null</c> se non c'è selezione.
    /// </summary>
    public Entity? Selected { get; set; }

    /// <summary>
    /// La pila dell'annulla, condivisa da tutti i pannelli.
    ///
    /// Sta qui e non nell'host per il motivo per cui esiste questa classe: l'annulla non
    /// appartiene a nessun pannello e li riguarda tutti — la Hierarchy elimina, l'Inspector
    /// modifica, i gizmi trascinano, e Ctrl+Z deve disfare l'ultima di quelle azioni chiunque
    /// l'abbia fatta. ⚠️ Non nullable, al contrario delle Resource: quelle possono mancare
    /// perché le dichiara il gioco, questa la costruisce l'editor per sé.
    /// </summary>
    public UndoStack Undo { get; } = new();

    /// <summary>
    /// L'infrastruttura del gioco, o <c>null</c> se il gioco non l'ha passata.
    ///
    /// Sta qui e non in un parametro di <see cref="IEditorPanel.Draw"/> perché è esattamente
    /// ciò per cui questa classe esiste: roba che più pannelli guardano allo stesso modo e
    /// che non appartiene a nessuno di loro. L'alternativa — un parametro in più su
    /// <c>Setup</c> per ogni servizio — obbliga a toccare l'interfaccia di tutti i pannelli
    /// ogni volta che uno solo scopre di aver bisogno di qualcosa: il pannello Systems
    /// globale (elenca/aggiunge/rimuove) vorrà il <see cref="SystemRegistry"/>, e le
    /// <c>Resource</c> hanno già un <c>RegisteredTypes</c> scritto apposta per un pannello
    /// che ancora non esiste. Passando il contenitore, quei pannelli nascono senza wiring.
    ///
    /// ⚠️ Nullable, e i pannelli devono reggerlo: un gioco può non usare le Resource (o non
    /// registrarci dentro ciò che un pannello cerca). L'editor è uno strumento diagnostico —
    /// degrada e lo dice, non cade.
    /// </summary>
    public Resources? Resources { get; set; }

    /// <summary>
    /// Il registry dei system, se il gioco l'ha registrato fra le <see cref="Resources"/>.
    ///
    /// ⚠️ <c>null</c> è un caso normale, non un errore: <c>SystemRegistry</c> lo crea il
    /// gioco (l'engine non gliene impone uno) e nulla lo obbliga a dichiararlo. Chi legge
    /// deve distinguere "non lo so" da "nessun system" — vedi l'Inspector.
    /// </summary>
    public SystemRegistry? Systems =>
        Resources is { } resources && resources.TryGet<SystemRegistry>(out var systems)
            ? systems
            : null;

    /// <summary>
    /// Il registry dei componenti, se il gioco l'ha dichiarato fra le <see cref="Resources"/>.
    /// È da qui che l'editor sa <b>quali tipi di componente esistono</b> e come crearne uno:
    /// senza, "aggiungi componente" non ha un elenco da mostrare.
    ///
    /// ⚠️ Come <see cref="Systems"/>, <c>null</c> è un caso normale — il registry lo
    /// costruisce il gioco. Il <see cref="SceneDocument"/> ne riceve già uno, ma quello è un
    /// canale privato del salvataggio: passa per l'host e arriva solo alla barra dei menu.
    /// Dichiararlo come Resource è ciò che lo rende leggibile a un pannello qualunque senza
    /// wiring — vedi il commento su <see cref="Resources"/>.
    /// </summary>
    public SceneComponentRegistry? Components =>
        Resources is { } resources && resources.TryGet<SceneComponentRegistry>(out var components)
            ? components
            : null;

    /// <summary>
    /// L'AssetManager del gioco, se dichiarato (lo fa il <c>GameLoop</c>, che lo possiede).
    /// Serve a chi deve trasformare un <b>path</b> — il dato d'autore — nell'handle opaco che
    /// il componente tiene davvero: il drag&amp;drop di un modello dal pannello File system.
    /// </summary>
    public AssetManager? Assets =>
        Resources is { } resources && resources.TryGet<AssetManager>(out var assets)
            ? assets
            : null;

    /// <summary>
    /// L'esito dell'ultima compilazione degli script, se il gioco l'ha dichiarato.
    ///
    /// Passa dalle Resource come tutto il resto — ⚠️ ma qui il motivo è più stretto del solito:
    /// quando la compilazione <b>fallisce</b> non c'è nessun assembly, nessun system e nessun
    /// componente da cui l'editor possa dedurre che sia successo qualcosa. Senza questo canale
    /// l'unico sintomo sarebbe una scena che si comporta male, e la causa (una riga che non
    /// compila) non sarebbe raggiungibile da nessuna parte nell'UI.
    /// </summary>
    public ScriptCompilation? Scripts =>
        Resources is { } resources && resources.TryGet<ScriptCompilation>(out var scripts)
            ? scripts
            : null;

    public bool IsSelected(Entity entity)
    {
        return Selected is { } selected && selected.Id == entity.Id;
    }

    public void Select(Entity entity)
    {
        Selected = entity;
    }

    public void ClearSelection()
    {
        Selected = null;
    }
}
