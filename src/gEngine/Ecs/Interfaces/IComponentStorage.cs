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
    /// <b>Attenzione</b>: se il componente è uno struct, il boxing ne fa una <b>copia</b>.
    /// Mutarla non tocca lo storage: va riscritta con <see cref="SetBoxed"/>. È lo stesso
    /// gotcha struct/copia del write-back nei system.
    /// </summary>
    object? GetBoxed(int entityId);

    void SetBoxed(int entityId, object component);

    void Remove(int entityId);

    void Clear();
}
