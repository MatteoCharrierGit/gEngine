using gEngine.Ecs.Base;

namespace gEngine.Editor.Undo;

/// <summary>
/// Più comandi che l'utente ha visto come <b>un'azione sola</b>, e che quindi si annullano
/// insieme: "Rimuovi da tutte" tocca dodici entità ma è un clic, e dodici Ctrl+Z per disfare un
/// clic sarebbero una punizione.
///
/// ⚠️ L'annulla scorre le parti <b>al contrario</b>. Oggi non si vede — le parti sono
/// indipendenti fra loro (entità diverse) e in qualunque ordine darebbero lo stesso risultato —
/// ma è la regola generale dei comandi composti, e scriverla al dritto significherebbe che il
/// primo composto con parti dipendenti si romperebbe in un modo difficile da vedere.
/// </summary>
public sealed class CompositeCommand(string label, IReadOnlyList<IEditorCommand> parts) : IEditorCommand
{
    public string Label { get; } = label;

    public bool IsEmpty => parts.Count == 0;

    public void Undo(World world)
    {
        for (var i = parts.Count - 1; i >= 0; i--)
            parts[i].Undo(world);
    }

    public void Redo(World world)
    {
        foreach (var part in parts)
            part.Redo(world);
    }

    /// <summary>
    /// Avvolge un'operazione che tocca <b>più entità note in anticipo</b>: fotografa tutte,
    /// esegue, e tiene solo le parti che sono davvero cambiate.
    ///
    /// ⚠️ "note in anticipo" è il vincolo: le entità vanno raccolte <b>prima</b>, perché dopo
    /// l'operazione non c'è modo di sapere quali toccava (un componente rimosso da tutte non
    /// lascia traccia di dov'era).
    /// </summary>
    public static CompositeCommand Around(
        World world, IReadOnlyList<Entity> entities, string label, Action operation)
    {
        var before = entities
            .Where(world.Exists)
            .Select(entity => EntitySnapshot.Capture(world, entity))
            .ToList();

        operation();

        var parts = before
            .Select(snapshot => EntityStateCommand.Between(world, snapshot.Entity, label, snapshot))
            .Where(command => command.ChangedSomething)
            .Cast<IEditorCommand>()
            .ToList();

        return new CompositeCommand(label, parts);
    }
}
