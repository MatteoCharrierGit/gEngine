namespace gEngine.Ecs.Interfaces;

/// <summary>
/// Faccia <b>non generica</b> di uno <c>ComponentStorage&lt;T&gt;</c>.
///
/// I system normali non passano di qui: conoscono i tipi che vogliono e usano
/// <c>Query&lt;T&gt;</c>/<c>GetComponent&lt;T&gt;</c>, che sono tipizzati e senza boxing.
/// Questa interfaccia serve a chi deve ragionare sui componenti <b>senza conoscerne i
/// tipi a compile time</b> — in pratica l'editor, il cui Inspector deve poter mostrare
/// anche un componente custom definito fuori dall'engine.
/// </summary>
public interface IComponentStorage
{
    int Count { get; }

    /// <summary>Il <c>T</c> dello storage, per la reflection dell'editor.</summary>
    Type ComponentType { get; }

    bool Has(int entityId);

    /// <summary>
    /// Il componente come <c>object</c>, o <c>null</c> se l'entità non ce l'ha.
    ///
    /// ⚠️ <b>Il gotcha ha due metà, e per anni ne è stata scritta una sola.</b>
    /// <list type="bullet">
    ///   <item>Se il componente è uno <b>struct</b>, il boxing ne fa una <b>copia</b>: mutarla
    ///   non tocca lo storage, va riscritta con <see cref="SetBoxed"/>. È il write-back dei
    ///   system.</item>
    ///   <item>Se è una <b>class</b> (es. <c>MeshRendererComponent</c>) si ottiene <b>il
    ///   riferimento</b>: mutarlo tocca lo storage <i>subito</i>, e darlo a una seconda entità
    ///   la lega alla prima. Non è teorico — <c>EntityOperations.Duplicate</c> copiava così,
    ///   e dipingere la copia dipingeva l'originale.</item>
    /// </list>
    /// Chi ha bisogno di un valore <b>indipendente</b> (duplicare, o tenere un "prima" per
    /// l'undo) non può usare questo da solo: serve una copia esplicita.
    /// </summary>
    object? GetBoxed(int entityId);

    void SetBoxed(int entityId, object component);

    void Remove(int entityId);

    void Clear();
}
