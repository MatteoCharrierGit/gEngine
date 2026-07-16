using System.Numerics;
using gEngine.Ecs.Base;
using gEngine.Rendering;
using ImGuiNET;

namespace gEngine.Editor.Panels;

/// <summary>
/// Una vista 3D dentro un pannello: la scena è disegnata su un render target e mostrata
/// come immagine ImGui, invece che a tutto schermo con la UI appiccicata sopra.
///
/// La classe è una sola e le istanze due, che è tutto il punto della separazione delle
/// camere: "Scena" guarda con la camera dell'editor (free-fly, ci si clicca per
/// selezionare), "Gioco" con quella del gioco (sola lettura, è l'inquadratura che vedrà
/// il giocatore). Prima la <c>Camera3D</c> era una sola e se la contendevano il free-fly
/// e il <c>CameraFollowSystem</c>: due viste insieme erano impossibili non perché
/// mancasse il render target, ma perché mancava la seconda camera.
/// </summary>
/// <param name="defaultPosition">
/// Posizione del primo avvio. Serve per lo stesso motivo degli altri pannelli — senza,
/// ImGui dà a ogni finestra la stessa posizione di default — ma qui morde il doppio: le
/// due viste sono la stessa classe, quindi nascerebbero <b>una esattamente sopra
/// l'altra</b> e sembrerebbe che la vista Gioco non esista.
/// </param>
/// <param name="interactive">
/// Se in questa vista si seleziona e si manipola. È un flag solo perché le due viste hanno
/// risposte opposte: nella Scena si lavora, nel Gioco si guarda e basta — lì un clic
/// appartiene al gioco, non all'editor.
/// </param>
/// <param name="camera">
/// Da dove si guarda, <b>richiesto per frame</b> e non tenuto una volta per tutte.
///
/// ⚠️ Prima era una <c>Camera3D</c> e basta: essendo una <c>class</c>, il riferimento preso
/// al setup restava agganciato a ciò che il gioco le faceva ogni frame. Quel trucco è morto
/// con la camera del gioco dentro il World — lì la <c>Camera3D</c> non esiste come oggetto
/// persistente, è <b>derivata</b> da Transform + CameraComponent, quindi un'istanza nuova a
/// ogni richiesta. Tenerne una sarebbe tenere l'inquadratura del primo frame.
///
/// Può restituire <c>null</c>: una scena senza camera è uno stato legittimo da quando
/// l'editor sa cancellare entità. La vista lo dice invece di far cadere l'editor.
/// </param>
public class ViewportPanel(
    string title,
    Func<Camera3D?> camera,
    WorldRenderer drawWorld,
    bool interactive,
    Vector2 defaultPosition,
    Vector2 defaultSize)
    : PanelBase(title, defaultPosition, defaultSize)
{
    private static readonly Color Background = new(30, 30, 34, 255);

    private readonly TransformGizmo? _gizmo = interactive ? new TransformGizmo() : null;

    private RenderTargetHandle _target = RenderTargetHandle.None;

    // Due misure e non una: la taglia del target che esiste ora, e quella che il pannello
    // aveva all'ultimo Draw. Il target si riempie PRIMA di aprire il frame ImGui, ma la
    // taglia del pannello si conosce solo mentre ImGui lo dispone, cioè dopo. Il target
    // insegue con un frame di ritardo — durante un resize la vista è vecchia di un frame,
    // che è molto meno peggio di non averla.
    private int _width;
    private int _height;
    private int _requestedWidth;
    private int _requestedHeight;

    /// <summary>
    /// Puntatore sopra l'immagine della vista. È il gate giusto per il free-fly, al posto
    /// di <c>EditorHost.WantsMouse</c>: da quando la scena vive dentro un pannello, il
    /// puntatore sopra di essa <b>è</b> sopra una finestra ImGui, quindi
    /// <c>WantCaptureMouse</c> è sempre vero e la camera non si muoverebbe mai più.
    /// </summary>
    public bool IsHovered { get; private set; }

    // ⚠️ NoScrollbar non è estetica. Un'immagine alta quanto lo spazio disponibile fa
    // sforare il contenuto di un pelo e ImGui tira su la scrollbar verticale; la scrollbar
    // si mangia ~14px di GetContentRegionAvail().X, quindi il frame dopo il target viene
    // ricreato più stretto — la vista si restringe da sola, e a ogni comparsa della barra si
    // butta via una render texture per ricrearla. Una vista 3D non scrolla: si ridimensiona.
    protected override ImGuiWindowFlags WindowFlags =>
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

    // Senza padding l'immagine arriva ai bordi del pannello: con quello di default resterebbe
    // una cornice grigia che fa sembrare la vista disallineata.
    protected override Vector2? WindowPadding => Vector2.Zero;

    /// <summary>
    /// Riempie il render target. Da chiamare <b>fuori</b> dal frame ImGui: dentro,
    /// <see cref="PanelBase.Draw"/> si limita a mostrare la texture già pronta.
    /// </summary>
    public void RenderToTarget(IRenderer renderer)
    {
        // Nessuna misura: primo frame, o pannello chiuso/collassato. In entrambi i casi
        // non c'è niente da riempire e nessuno che guarderebbe il risultato.
        if (_requestedWidth <= 0 || _requestedHeight <= 0)
            return;

        // Nessuna camera nel World: niente da cui guardare. Il target resta com'è e Draw
        // mostra il perché — meglio di un'immagine nera che sembra un bug del renderer.
        if (camera() is not { } resolved)
            return;

        if (!_target.IsValid || _width != _requestedWidth || _height != _requestedHeight)
        {
            if (_target.IsValid)
                renderer.DestroyRenderTarget(_target);

            _target = renderer.CreateRenderTarget(_requestedWidth, _requestedHeight);
            _width = _requestedWidth;
            _height = _requestedHeight;
        }

        renderer.BeginRenderTarget(_target);
        renderer.ClearBackground(Background);
        drawWorld(renderer, resolved);
        renderer.EndRenderTarget();
    }

    /// <summary>
    /// Chiuso dal menu o collassato: azzerare la misura ferma il rendering del target.
    /// Renderizzare una vista che nessuno vede è lavoro GPU buttato, ogni frame.
    /// </summary>
    protected override void OnNotDrawn()
    {
        _requestedWidth = 0;
        _requestedHeight = 0;
        IsHovered = false;
    }

    protected override void DrawContent(World world, EditorContext context, IRenderer renderer)
    {
        var available = ImGui.GetContentRegionAvail();
        _requestedWidth = Math.Max(1, (int)available.X);
        _requestedHeight = Math.Max(1, (int)available.Y);

        // Risolta una volta sola per Draw: gizmi e picking DEVONO usare la stessa camera con
        // cui è stata riempita la texture che si sta guardando, o si cliccherebbe un pixel
        // di un'inquadratura diversa da quella a schermo.
        var resolved = camera();

        if (_target.IsValid && resolved is not null)
        {
            // ⚠️ Le render texture di raylib (OpenGL) hanno l'origine in BASSO a sinistra,
            // ImGui in alto: la V va invertita passando uv0=(0,1) e uv1=(1,0), altrimenti
            // la vista appare capovolta.
            //
            // Disegnata alla taglia del target e non a quella disponibile: durante un
            // resize il pixel resta 1:1, il pannello semplicemente non si riempie per un
            // frame invece di mostrare l'immagine stirata.
            ImGui.Image(
                renderer.GetRenderTargetTextureId(_target),
                new Vector2(_width, _height),
                new Vector2(0f, 1f),
                new Vector2(1f, 0f));

            IsHovered = ImGui.IsItemHovered();

            var imageOrigin = ImGui.GetItemRectMin();

            // Il gizmo va disegnato DOPO l'immagine (ci sta sopra) ma consultato PRIMA di
            // decidere il picking: un clic che afferra una maniglia non è un clic per
            // selezionare l'entità che c'è dietro.
            var gizmoWantsMouse = _gizmo is not null &&
                                  _gizmo.Draw(world, context, resolved, imageOrigin,
                                      new Vector2(_width, _height), IsHovered);

            if (interactive && IsHovered && !gizmoWantsMouse && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                Pick(world, context, resolved, imageOrigin);
        }
        else
        {
            IsHovered = false;

            // Due motivi diversi per non avere un'immagine, e vanno distinti: al primo frame
            // il target non c'è ancora e basta aspettare; senza camera invece non c'è niente
            // da aspettare — la scena non ne ha una (cancellata? mai messa nel file?) e
            // l'unica cosa utile è dirlo.
            ImGui.TextDisabled(resolved is null
                ? "Nessuna camera nella scena."
                : "Vista in preparazione...");
        }
    }

    private void Pick(World world, EditorContext context, Camera3D resolved, Vector2 imageOrigin)
    {
        // Il mouse arriva in coordinate finestra, la camera ragiona in pixel della vista:
        // l'immagine sta in un punto qualunque dello schermo, e senza sottrarre il suo
        // angolo la semiretta partirebbe dal pixel sbagliato — tanto più sbagliato quanto
        // più il pannello è lontano dall'origine.
        var local = ImGui.GetIO().MousePos - imageOrigin;
        var ray = resolved.GetRay(local, new Vector2(_width, _height));

        if (EntityPicker.Pick(world, ray) is { } hit)
            context.Select(hit);
        else
            context.ClearSelection(); // clic nel vuoto = deseleziona, come nella Hierarchy
    }
}
