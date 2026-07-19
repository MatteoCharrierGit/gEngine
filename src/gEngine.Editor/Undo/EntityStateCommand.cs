using gEngine.Ecs.Base;

namespace gEngine.Editor.Undo;

/// <summary>
/// "I componenti di questa entità sono passati da così a così". È il comando che copre quasi
/// tutto l'editing: un campo dell'Inspector, un trascinamento del gizmo, un asset messo in uno
/// slot, un componente aggiunto o tolto, un riparentamento.
///
/// Uno solo invece di cinque perché l'editor manipola già i componenti <b>senza conoscerne i
/// tipi</b> (<c>GetBoxed</c>/<c>SetBoxed</c>): "com'era l'entità / com'è adesso" è
/// l'astrazione che quel codice usa davvero, e un comando per ogni verbo (SetComponent,
/// AddComponent, RemoveComponent, Reparent) sarebbe stato quattro modi di scrivere la stessa
/// coppia di fotografie — e quattro posti in cui sbagliarla.
///
/// ⚠️ <b>Una entità sola.</b> Un'operazione che ne tocca due (riparentare non lo fa: il
/// <c>ParentComponent</c> sta tutto sul figlio) va composta, non forzata qui.
/// </summary>
public sealed class EntityStateCommand : IEditorCommand
{
    private readonly EntitySnapshot _before;
    private readonly EntitySnapshot _after;

    private EntityStateCommand(string label, EntitySnapshot before, EntitySnapshot after)
    {
        Label = label;
        _before = before;
        _after = after;
    }

    public string Label { get; }

    /// <summary>
    /// Esegue <paramref name="operation"/> fotografando l'entità prima e dopo.
    ///
    /// L'operazione resta scritta dove stava (<c>EntityOperations.Reparent</c>, la SetBoxed
    /// dell'Inspector): questo la avvolge, non la sostituisce. È il modo in cui l'undo entra in
    /// un editor già scritto senza riscriverlo — e senza che aggiungere un pannello domani
    /// significhi ricordarsi di un dispatcher.
    /// </summary>
    public static EntityStateCommand Around(World world, Entity entity, string label, Action operation)
    {
        var before = EntitySnapshot.Capture(world, entity);
        operation();
        return new EntityStateCommand(label, before, EntitySnapshot.Capture(world, entity));
    }

    /// <summary>
    /// Per le modifiche <b>continue</b>, dove l'operazione non è una chiamata ma un gesto: un
    /// <c>DragFloat</c> o una maniglia del gizmo scrivono a ogni frame, e il "prima" va preso
    /// quando il gesto <b>inizia</b>. Chi chiama tiene la fotografia iniziale e la consegna qui
    /// alla fine.
    ///
    /// ⚠️ Senza questa distinzione l'undo di un trascinamento tornerebbe indietro <b>di un
    /// frame</b>: 60 comandi al secondo, ognuno che disfa un millimetro. È il motivo per cui i
    /// confini del gesto sono un pezzo del disegno e non un dettaglio della UI.
    /// </summary>
    public static EntityStateCommand Between(World world, Entity entity, string label, EntitySnapshot before)
    {
        return new EntityStateCommand(label, before, EntitySnapshot.Capture(world, entity));
    }

    /// <summary>Se le due fotografie sono identiche non c'è niente da ricordare: vedi <see cref="SnapshotsEqual"/>.</summary>
    public bool ChangedSomething => !SnapshotsEqual(_before, _after);

    public void Undo(World world) => _before.Restore(world);

    public void Redo(World world) => _after.Restore(world);

    /// <summary>
    /// Confronto per <b>valore</b> dei componenti, per non riempire lo stack di comandi vuoti:
    /// un clic su un campo senza trascinare, o una maniglia afferrata e lasciata dov'era,
    /// passano comunque dai confini del gesto. Un "annulla" che non fa niente è peggio di un
    /// bottone spento — sembra rotto.
    ///
    /// ⚠️ Regge su <c>Equals</c>: gli struct l'hanno per valore da sé, e da quando il
    /// <c>MeshRenderer</c> è diventato struct <b>tutti</b> i componenti sono confrontati
    /// davvero. Prima era l'unica class del lotto e cadeva sul confronto per riferimento: due
    /// copie risultavano sempre diverse, quindi ogni gesto che lo toccava registrava un comando
    /// <i>anche quando nulla era cambiato</i>. Era il verso sicuro dello sbaglio (uno "annulla"
    /// in più, non uno in meno), ma era uno sbaglio, e adesso non c'è più.
    ///
    /// ⚠️ Una class senza <c>Equals</c> rifarebbe esattamente quello. È il secondo motivo per
    /// cui i componenti restano struct, dopo l'aliasing.
    /// </summary>
    private static bool SnapshotsEqual(EntitySnapshot a, EntitySnapshot b)
    {
        if (a.Components.Count != b.Components.Count)
            return false;

        // ⚠️ Per tipo e non per posizione: le due fotografie scorrono gli storage del World, e
        // un componente di un tipo mai visto prima ne crea uno nuovo — cioè l'ordine fra un
        // Capture e l'altro non è garantito. Confrontare a coppie di indice avrebbe funzionato
        // quasi sempre, che è il modo peggiore di non funzionare.
        var byType = b.Components.ToDictionary(component => component.GetType());

        return a.Components.All(component =>
            byType.TryGetValue(component.GetType(), out var other) && component.Equals(other));
    }
}
