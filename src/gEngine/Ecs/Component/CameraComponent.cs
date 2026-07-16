using Raylib_cs;

namespace gEngine.Ecs.Component;

/// <summary>
/// Il punto di vista del giocatore. Dati puri e <b>solo ottici</b>: la POSA non sta qui —
/// posizione e direzione si ricavano dal <see cref="TransformComponent"/> dell'entità
/// (posizione, e <c>Forward</c>/<c>Up</c> dalla rotazione), esattamente come fa già
/// <see cref="LightComponent"/>. È il modello Unity, ed è il motivo per cui questo
/// componente esiste: senza, la camera sarebbe un oggetto a parte con una sua posizione
/// duplicata, che nessun gizmo può muovere e nessuna gerarchia può agganciare.
///
/// ⚠️ Asimmetria voluta con la camera di scena dell'editor (<c>EditorHost.SceneCamera</c>),
/// che <b>non</b> è un'entità del World. Non è un'incoerenza ma la conseguenza diretta
/// della regola "i dati di scena vivono nel World": la camera del gioco è dato d'autore
/// (va nel file scena, si seleziona nella Hierarchy, si muove col gizmo), quella con cui si
/// naviga nell'editor è stato dell'<b>editor</b> — se stesse nel World finirebbe
/// serializzata dentro la scena e comparirebbe nella gerarchia del gioco. Anche Unity
/// tiene la scene camera fuori dalla scena.
///
/// La <see cref="Rendering.Camera3D"/> "risolta" si ottiene da qui + Transform con
/// <c>World.GetCamera</c> / <c>World.GetPrimaryCamera</c>.
/// </summary>
public struct CameraComponent
{
    /// <summary>Campo visivo <b>verticale</b> in gradi (convenzione raylib).</summary>
    [EditorConfiguration] public float FovY;

    [EditorConfiguration] public float Near;
    [EditorConfiguration] public float Far;

    [EditorConfiguration] public CameraProjection Projection;

    /// <summary>
    /// Quale camera guarda il giocatore, quando ce n'è più d'una (una principale e una di
    /// una security cam, per dire). Esiste apposta invece di "la prima che trovi": l'ordine
    /// delle query segue l'ordine di creazione delle entità, quindi aggiungere una camera
    /// in cima al file scena cambierebbe l'inquadratura del gioco senza che nessuno lo
    /// abbia chiesto.
    ///
    /// ⚠️ Non è un invariante che ce ne sia esattamente una: con più camere primarie vince
    /// la prima incontrata, con nessuna si ricade sulla prima camera qualunque. Vedi
    /// <c>World.GetPrimaryCamera</c>.
    /// </summary>
    [EditorConfiguration] public bool Primary;
}
