using gEngine.Ecs.Base;

namespace gEngine.Editor.Undo;

/// <summary>
/// Un'azione dell'editor che si sa disfare.
///
/// <b>La regola che tiene insieme il disegno</b>: un comando si costruisce <i>attorno a
/// un'operazione già avvenuta</i>. Chi modifica la scena continua a farlo come prima —
/// l'Inspector scrive nello storage, la Hierarchy chiama <c>EntityOperations</c> — e il comando
/// fotografa il prima e il dopo. Non c'è un mondo parallelo in cui le modifiche passano da un
/// dispatcher, che è la strada in cui un solo punto dimenticato produce una scena che si muove
/// e uno stack che non lo sa.
///
/// Ne segue che <see cref="Redo"/> è <b>idempotente</b> (rimette uno stato in cui, la prima
/// volta, si è già) e che <see cref="UndoStack.Push"/> non deve eseguire niente. Il rischio
/// noto di questo disegno — un <c>Redo</c> mai esercitato finché qualcuno non lo preme — è
/// chiuso per costruzione dove si può: <c>Undo</c> e <c>Redo</c> sono lo <b>stesso</b> codice
/// con due fotografie diverse, quindi se una direzione funziona funziona anche l'altra.
/// </summary>
public interface IEditorCommand
{
    /// <summary>
    /// Come si chiama l'azione nel menu ("Annulla <c>sposta lamp-blue</c>"). Al presente e in
    /// minuscolo: è un complemento, non una frase.
    /// </summary>
    string Label { get; }

    /// <summary>Torna allo stato precedente all'azione.</summary>
    void Undo(World world);

    /// <summary>Rimette lo stato successivo all'azione.</summary>
    void Redo(World world);
}
