using gEngine.Ecs.Base;

namespace gEngine.Editor.Undo;

/// <summary>
/// Entità che nascono e muoiono: creare, creare una figlia, duplicare, eliminare un sottoalbero.
///
/// Un comando solo per i due versi perché <b>sono lo stesso comando</b>: creare è "queste
/// entità esistono", eliminare è la stessa frase letta al contrario. Tenerli separati avrebbe
/// voluto dire due classi che si scambiano <c>Undo</c> e <c>Redo</c>, cioè lo stesso codice
/// scritto due volte con la possibilità di sbagliarne uno.
///
/// ⚠️ L'eliminazione porta con sé <b>i discendenti</b> (è la politica di
/// <c>EntityOperations.DestroyRecursive</c>), quindi qui le entità sono una lista e non una: un
/// undo che rimettesse solo il genitore lascerebbe i figli morti, che è metà del lavoro fatta
/// nel modo peggiore — sembra riuscito.
///
/// ⚠️ Le entità tornano <b>con lo stesso id</b> (<c>World.RestoreEntity</c>), non come copie
/// nuove: è ciò che rende esatti i riferimenti che le altre parti si tengono — il
/// <c>ParentComponent</c> di un figlio, la selezione, i comandi più vecchi nello stack.
/// </summary>
public sealed class EntityLifetimeCommand : IEditorCommand
{
    private readonly IReadOnlyList<EntitySnapshot> _snapshots;

    /// <summary>
    /// Da che parte sta la vita: <c>true</c> se dopo l'azione le entità <b>esistono</b>
    /// (creazione), <c>false</c> se non esistono più (eliminazione).
    /// </summary>
    private readonly bool _existsAfter;

    private EntityLifetimeCommand(string label, IReadOnlyList<EntitySnapshot> snapshots, bool existsAfter)
    {
        Label = label;
        _snapshots = snapshots;
        _existsAfter = existsAfter;
    }

    public string Label { get; }

    /// <summary>
    /// La prima entità coinvolta — per una creazione è <b>quella creata</b>, ed è il modo in
    /// cui chi ha chiesto di creare la ritrova per selezionarla: la factory è girata qui
    /// dentro, quindi il suo risultato non tornerebbe indietro da nessun'altra parte.
    /// </summary>
    public Entity Entity => _snapshots[0].Entity;

    /// <summary>
    /// Avvolge una creazione: la <paramref name="factory"/> gira <b>una volta sola</b>, e ciò
    /// che ha prodotto viene fotografato.
    ///
    /// ⚠️ Il "una volta sola" è il punto, non un'ottimizzazione: rieseguire la factory a ogni
    /// Redo darebbe un'entità <b>diversa</b> — <c>EntityOperations.Create</c> e
    /// <c>Duplicate</c> calcolano un nome libero al momento, quindi rifare "crea" dopo un
    /// annulla produrrebbe "Nuova entità (2)" dove prima c'era "Nuova entità". Rifare non è
    /// ripetere: è rimettere quella di prima.
    /// </summary>
    public static EntityLifetimeCommand ForCreation(World world, string label, Func<Entity> factory)
    {
        var entity = factory();
        return new EntityLifetimeCommand(label, [EntitySnapshot.Capture(world, entity)], existsAfter: true);
    }

    /// <summary>
    /// Avvolge un'eliminazione: fotografa <b>prima</b> di distruggere (dopo non c'è più niente
    /// da fotografare) e poi lascia distruggere a chi sa come.
    /// </summary>
    public static EntityLifetimeCommand ForDestruction(
        World world, string label, IReadOnlyList<Entity> entities, Action destroy)
    {
        var snapshots = entities
            .Where(world.Exists)
            .Select(entity => EntitySnapshot.Capture(world, entity))
            .ToList();

        destroy();

        return new EntityLifetimeCommand(label, snapshots, existsAfter: false);
    }

    public void Undo(World world)
    {
        if (_existsAfter)
            Destroy(world);
        else
            Restore(world);
    }

    public void Redo(World world)
    {
        if (_existsAfter)
            Restore(world);
        else
            Destroy(world);
    }

    /// <summary>
    /// ⚠️ Due passate: prima tutte le entità tornano a esistere, poi i componenti. Un
    /// <c>ParentComponent</c> ripristinato può puntare a un'altra entità dello stesso
    /// sottoalbero, e in una passata sola il figlio ricostruito prima del genitore
    /// riferirebbe un'entità che ancora non c'è. È la stessa istanziazione a due passate di
    /// <c>SceneInstantiator</c>, per lo stesso identico motivo.
    /// </summary>
    private void Restore(World world)
    {
        foreach (var snapshot in _snapshots)
        {
            if (!world.Exists(snapshot.Entity))
                world.RestoreEntity(snapshot.Entity.Id);
        }

        foreach (var snapshot in _snapshots)
            snapshot.Restore(world);
    }

    private void Destroy(World world)
    {
        foreach (var snapshot in _snapshots)
            world.DestroyEntity(snapshot.Entity);
    }
}
