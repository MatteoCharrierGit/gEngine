using System.Numerics;
using gEngine.Assets;
using gEngine.Rendering;

namespace gEngine.Ecs.Component;

public class MeshRendererComponent
{
    [EditorConfiguration] public MeshKind Kind;
    [EditorConfiguration] public Vector3 Size;
    [EditorConfiguration] public Color Tint;
    [EditorConfiguration] public bool Wireframe;
    [EditorConfiguration] public bool Visible;

    /// <summary>
    /// Modello da disegnare quando <see cref="Kind"/> è <see cref="MeshKind.Model"/>.
    /// Handle opaco caricato via <c>AssetManager.LoadModel</c>. Ignorato per le primitive
    /// (Cube/Plane/Grid). Default: <see cref="ModelHandle.None"/>.
    ///
    /// ⚠️ <c>[EditorAsset]</c> e <b>non</b> <c>[EditorConfiguration]</c>: è un link a una
    /// risorsa esterna, non un numero. Il valore d'autore è il <i>path</i> del modello
    /// (infatti è quello che finisce nel file scena, vedi <c>SceneWriteContext</c>), quindi
    /// l'editor fa scegliere il file e converte lui — esporre l'handle darebbe da editare un
    /// indice di cache che al reload punta a un modello a caso.
    /// </summary>
    [EditorAsset(AssetKind.Model)] public ModelHandle Model;

    /// <summary>Fascia di disegno: gli opachi prima dei trasparenti. Default: Opaque.</summary>
    [EditorConfiguration] public RenderLayer Layer;

    /// <summary>
    /// Ordine di disegno esplicito <b>all'interno della stessa</b> <see cref="Layer"/>:
    /// valori più bassi vengono disegnati prima. Default: 0. È un controllo manuale
    /// (utile per painter's order 2D-like); non è ancora un ordinamento per distanza
    /// dalla camera — vedi nota in <see cref="gEngine.Ecs.System.MeshRenderSystem"/>.
    /// </summary>
    [EditorConfiguration] public int SortingOrder;
}