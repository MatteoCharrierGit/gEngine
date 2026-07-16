using gEngine.Ecs.Base;

namespace gEngine.Editor;

/// <summary>
/// Stato condiviso fra i pannelli dell'editor. Oggi contiene solo la selezione, ma è
/// il posto dove finirà tutto ciò che due pannelli devono vedere allo stesso modo
/// (la Hierarchy sceglie l'entità, l'Inspector la mostra, i gizmi la manipolano).
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
