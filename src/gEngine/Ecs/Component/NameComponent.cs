namespace gEngine.Ecs.Component;

/// <summary>
/// Nome leggibile dell'entità. Puramente descrittivo: nessun system di runtime lo
/// legge per prendere decisioni — identificare un'entità per nome resterebbe una
/// scansione lineare, e il punto di verità per i riferimenti è sempre l'<see cref="Base.Entity"/>.
///
/// Esiste per gli <b>umani</b>: il pannello Hierarchy dell'editor ha bisogno di
/// un'etichetta migliore di "Entity 7", e il salvataggio della scena deve poter
/// riscrivere <c>EntityDefinition.Name</c> — che è anche il bersaglio dei
/// riferimenti fra entità nel file (es. <c>Parent</c>).
///
/// Il nome è quindi opzionale: un'entità creata a runtime può benissimo non averlo.
/// </summary>
public struct NameComponent
{
    [EditorConfiguration("Nome")] public string Value;
}
