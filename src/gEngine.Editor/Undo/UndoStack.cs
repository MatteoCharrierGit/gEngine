using gEngine.Ecs.Base;

namespace gEngine.Editor.Undo;

/// <summary>
/// La pila dei comandi annullabili: il "torna indietro" dell'editor.
///
/// Fino a qui ogni azione distruttiva era definitiva — Elimina, "Rimuovi da tutte", ogni
/// trascinamento del gizmo — e si sopravviveva solo perché il disco non veniva toccato:
/// riaprire la scena era l'annulla del poveraccio. Un editor che sa scrivere sul disco senza
/// saper tornare indietro è uno strumento che punisce chi lo esplora.
///
/// ⚠️ <b>Non registra "l'ultima azione", registra i comandi</b>: la differenza si vede quando
/// un'operazione ne contiene un'altra. Vedi <see cref="Push"/>.
/// </summary>
public sealed class UndoStack
{
    /// <summary>
    /// Quanti passi indietro. Il limite c'è perché ogni comando tiene copie di componenti, e
    /// una sessione lunga non deve crescere senza fine; è alto perché il costo per comando è
    /// una manciata di struct — il numero che conta è "più di quanti errori si fanno di
    /// seguito", non un budget di memoria.
    /// </summary>
    private const int Capacity = 200;

    private readonly List<IEditorCommand> _done = [];
    private readonly List<IEditorCommand> _undone = [];

    public bool CanUndo => _done.Count > 0;
    public bool CanRedo => _undone.Count > 0;

    /// <summary>Come si chiama ciò che si annullerebbe, per il menu. Null se non c'è niente.</summary>
    public string? UndoLabel => CanUndo ? _done[^1].Label : null;

    public string? RedoLabel => CanRedo ? _undone[^1].Label : null;

    /// <summary>
    /// Registra un comando <b>già eseguito</b>. Non lo esegue: vedi <see cref="IEditorCommand"/>
    /// per il perché i comandi si costruiscono attorno a un'operazione avvenuta.
    ///
    /// ⚠️ Registrare azzera la pila del <b>redo</b>: da un passato modificato non si può più
    /// rifare il futuro che c'era prima. È il comportamento di qualunque editor, e ometterlo
    /// darebbe un "rifai" che rimette pezzi di una storia che non è più successa.
    /// </summary>
    public void Push(IEditorCommand command)
    {
        _done.Add(command);
        _undone.Clear();

        if (_done.Count > Capacity)
            _done.RemoveAt(0);
    }

    /// <summary>
    /// Comodità per il caso più frequente: <see cref="EntityStateCommand.Around"/> più il
    /// <see cref="Push"/>, saltando i comandi che non hanno cambiato niente.
    ///
    /// ⚠️ Lo scarto dei comandi vuoti sta <b>qui</b> e non nella UI: chi chiama non deve
    /// chiedersi se l'azione ha prodotto una differenza — se non l'ha prodotta, non è
    /// un'azione. Senza, un clic su una maniglia senza trascinare lascerebbe un "annulla" che
    /// non fa niente, che sembra un editor rotto.
    /// </summary>
    public void Run(World world, Entity entity, string label, Action operation)
    {
        var command = EntityStateCommand.Around(world, entity, label, operation);

        if (command.ChangedSomething)
            Push(command);
    }

    public void Undo(World world)
    {
        if (!CanUndo)
            return;

        var command = _done[^1];
        _done.RemoveAt(_done.Count - 1);

        command.Undo(world);
        _undone.Add(command);
    }

    public void Redo(World world)
    {
        if (!CanRedo)
            return;

        var command = _undone[^1];
        _undone.RemoveAt(_undone.Count - 1);

        command.Redo(world);
        _done.Add(command);
    }

    /// <summary>
    /// Butta via la storia. Serve <b>ogni volta che il World viene sostituito in blocco</b>:
    /// Play, Stop, Apri, Nuova scena.
    ///
    /// ⚠️ Non è prudenza, è correttezza: quelle operazioni passano da <c>World.Clear</c> +
    /// <c>Instantiate</c>, quindi le entità di prima non esistono più <b>e i loro id nemmeno</b>
    /// (il contatore non torna indietro). Un comando rimasto nello stack parlerebbe di entità
    /// che non ci sono, e il suo Undo le farebbe <b>rinascere</b> dentro una scena a cui non
    /// appartengono — un annulla che aggiunge roba. Meglio niente storia che una storia di
    /// un'altra scena.
    /// </summary>
    public void Clear()
    {
        _done.Clear();
        _undone.Clear();
    }
}
