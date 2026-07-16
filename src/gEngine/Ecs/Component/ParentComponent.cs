using gEngine.Ecs.Base;

namespace gEngine.Ecs.Component;

/// <summary>
/// Lega un'entità a un genitore nella gerarchia dei transform. La sola presenza
/// del componente significa "sono figlia di <see cref="Parent"/>": il
/// <see cref="TransformComponent"/> dell'entità va allora interpretato come
/// <b>locale</b> (relativo al genitore), non più come transform di mondo.
///
/// Modello volutamente minimale (solo il riferimento verso l'alto): non teniamo
/// una lista di figli sul genitore — trovarli, se servisse, è una query. Un solo
/// punto di verità = meno bookkeeping e nessuna sincronizzazione da mantenere.
/// La world matrix si ricava risalendo i genitori con
/// <see cref="gEngine.Ecs.Base.WorldTransforms.GetWorldMatrix"/>.
/// </summary>
public struct ParentComponent
{
    /// <summary>
    /// Niente <c>[EditorConfiguration]</c>: è un <b>riferimento</b> a un'altra entità, e il
    /// posto dove si riparenta è l'albero della Hierarchy, non un campo dell'Inspector — un
    /// id battuto a mano è un piede nel fucile (nessun controllo che l'entità esista, né che
    /// non stia creando un ciclo). Il componente resta visibile come header: "questa entità
    /// ha un genitore" è già un'informazione.
    /// </summary>
    public Entity Parent;
}
