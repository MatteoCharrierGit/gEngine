using System.Numerics;
using gEngine.Assets;
using gEngine.Rendering;

namespace gEngine.Ecs.Component;

public class MeshRendererComponent
{
    public MeshKind Kind;
    public Vector3 Size;
    public Color Tint;
    public bool Wireframe;
    public bool Visible;

    /// <summary>
    /// Modello da disegnare quando <see cref="Kind"/> è <see cref="MeshKind.Model"/>.
    /// Handle opaco caricato via <c>AssetManager.LoadModel</c>. Ignorato per le primitive
    /// (Cube/Plane/Grid). Default: <see cref="ModelHandle.None"/>.
    /// </summary>
    public ModelHandle Model;

    /// <summary>Fascia di disegno: gli opachi prima dei trasparenti. Default: Opaque.</summary>
    public RenderLayer Layer;

    /// <summary>
    /// Ordine di disegno esplicito <b>all'interno della stessa</b> <see cref="Layer"/>:
    /// valori più bassi vengono disegnati prima. Default: 0. È un controllo manuale
    /// (utile per painter's order 2D-like); non è ancora un ordinamento per distanza
    /// dalla camera — vedi nota in <see cref="gEngine.Ecs.System.MeshRenderSystem"/>.
    /// </summary>
    public int SortingOrder;
}