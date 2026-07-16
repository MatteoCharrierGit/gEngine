using System.Numerics;
using ImGuiNET;

namespace gEngine.Editor;

/// <summary>
/// Il tema dell'editor: colori e metriche, applicati una volta al setup.
///
/// Prima c'era il dark di default di rlImGui, che è il tema di ImGui — cioè quello che ha
/// qualunque cosa costruita con ImGui, incluse le finestre di debug buttate lì in un
/// pomeriggio. Non è brutto: è <b>anonimo</b>, e un editor che assomiglia a un pannello di
/// debug viene usato come un pannello di debug.
///
/// Le regole seguite, che sono più importanti dei singoli numeri:
/// <list type="bullet">
///   <item><b>Una sola scala di grigi</b>, neutra e fredda, con i piani separati dalla
///   luminosità e non dai bordi: lo sfondo della finestra è più scuro dei suoi campi, che sono
///   più scuri di quelli sotto il puntatore. Così "cosa è cliccabile" si legge senza cercare un
///   contorno.</item>
///   <item><b>Un solo accento</b>, e speso solo dove significa qualcosa: selezione, spunte,
///   maniglie. Il blu di default di ImGui è saturo e finisce ovunque — con dodici pannelli
///   aperti diventa rumore. Questo è desaturato apposta: deve dire "qui" senza gridare.</item>
///   <item><b>Raggi piccoli e uguali</b> (4px, 2px per le maniglie). Il default mescola angoli
///   vivi e arrotondati; l'incoerenza si nota anche quando non si sa cosa si sta notando.</item>
///   <item><b>Spaziature strette ma respirabili</b>: questo è un editor, la densità è una
///   feature — ma le righe devono restare distinguibili a colpo d'occhio.</item>
/// </list>
///
/// ⚠️ Il limite più grosso resta il <b>font</b>: è ProggyClean, il bitmap font di default di
/// ImGui, ed è ciò che fa sembrare "prototipo" più di ogni colore. Cambiarlo vuol dire
/// spedire un .ttf (peso, licenza, e un path che non sia quello di Windows) — è una decisione
/// a sé, non un ritocco. Vedi <c>HANDOFF.md</c>.
/// </summary>
public static class EditorTheme
{
    // La scala. Ogni colore qui sotto viene da questa e da nient'altro: è ciò che tiene
    // insieme il tema quando fra sei mesi qualcuno aggiungerà un pannello.
    private static readonly Vector4 Background = Grey(0.106f);   // sfondo finestra
    private static readonly Vector4 Surface = Grey(0.141f);      // campi, header
    private static readonly Vector4 SurfaceHover = Grey(0.192f);
    private static readonly Vector4 SurfaceActive = Grey(0.243f);
    private static readonly Vector4 Sunken = Grey(0.078f);       // barre dei titoli, sfondo scrollbar
    private static readonly Vector4 Line = Grey(0.220f);         // bordi e separatori

    private static readonly Vector4 Text = new(0.878f, 0.890f, 0.910f, 1f);
    private static readonly Vector4 TextMuted = new(0.420f, 0.447f, 0.490f, 1f);

    /// <summary>Desaturato apposta: vedi il commento della classe.</summary>
    private static readonly Vector4 Accent = new(0.325f, 0.529f, 0.808f, 1f);

    public static void Apply()
    {
        var style = ImGui.GetStyle();

        style.WindowRounding = 4f;
        style.ChildRounding = 4f;
        style.FrameRounding = 4f;
        style.PopupRounding = 4f;
        style.ScrollbarRounding = 4f;
        style.TabRounding = 4f;
        style.GrabRounding = 2f;

        // I piani si separano con la luminosità, non coi contorni: un bordo per riquadro, in
        // un editor pieno di riquadri, è una griglia che non aiuta a leggere niente.
        style.WindowBorderSize = 1f;
        style.FrameBorderSize = 0f;
        style.PopupBorderSize = 1f;
        style.ChildBorderSize = 1f;

        style.WindowPadding = new Vector2(8f, 8f);
        style.FramePadding = new Vector2(6f, 3f);
        style.ItemSpacing = new Vector2(6f, 5f);
        style.ItemInnerSpacing = new Vector2(5f, 4f);
        style.IndentSpacing = 16f;
        style.ScrollbarSize = 11f;
        style.GrabMinSize = 9f;

        // A sinistra: i titoli si leggono in colonna quando i pannelli sono impilati, e
        // centrati costringono l'occhio a rincorrerli.
        style.WindowTitleAlign = new Vector2(0f, 0.5f);
        style.SeparatorTextBorderSize = 1f;
        style.SeparatorTextPadding = new Vector2(16f, 3f);

        var colors = style.Colors;

        colors[(int)ImGuiCol.Text] = Text;
        colors[(int)ImGuiCol.TextDisabled] = TextMuted;

        colors[(int)ImGuiCol.WindowBg] = Background;
        colors[(int)ImGuiCol.ChildBg] = new Vector4(0f, 0f, 0f, 0f);
        colors[(int)ImGuiCol.PopupBg] = Grey(0.129f);
        colors[(int)ImGuiCol.Border] = Line;
        colors[(int)ImGuiCol.BorderShadow] = new Vector4(0f, 0f, 0f, 0f);

        colors[(int)ImGuiCol.FrameBg] = Surface;
        colors[(int)ImGuiCol.FrameBgHovered] = SurfaceHover;
        colors[(int)ImGuiCol.FrameBgActive] = SurfaceActive;

        // La barra del titolo è più SCURA della finestra, non più chiara: così il pannello
        // attivo si distingue per il testo e per il bordo, non per una fascia luminosa che
        // tira l'occhio su ogni finestra aperta.
        colors[(int)ImGuiCol.TitleBg] = Sunken;
        colors[(int)ImGuiCol.TitleBgActive] = Grey(0.129f);
        colors[(int)ImGuiCol.TitleBgCollapsed] = Sunken;
        colors[(int)ImGuiCol.MenuBarBg] = Grey(0.129f);

        colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0f, 0f, 0f, 0f);
        colors[(int)ImGuiCol.ScrollbarGrab] = Grey(0.224f);
        colors[(int)ImGuiCol.ScrollbarGrabHovered] = Grey(0.290f);
        colors[(int)ImGuiCol.ScrollbarGrabActive] = Grey(0.350f);

        colors[(int)ImGuiCol.CheckMark] = Accent;
        colors[(int)ImGuiCol.SliderGrab] = Grey(0.400f);
        colors[(int)ImGuiCol.SliderGrabActive] = Accent;

        // I bottoni partono dallo stesso grigio dei campi: in un Inspector, un bottone blu
        // ogni tre righe trasforma la colonna in un semaforo. L'accento arriva alla pressione.
        colors[(int)ImGuiCol.Button] = Surface;
        colors[(int)ImGuiCol.ButtonHovered] = SurfaceHover;
        colors[(int)ImGuiCol.ButtonActive] = Accent;

        colors[(int)ImGuiCol.Header] = Surface;
        colors[(int)ImGuiCol.HeaderHovered] = SurfaceHover;
        colors[(int)ImGuiCol.HeaderActive] = SurfaceActive;

        colors[(int)ImGuiCol.Separator] = Line;
        colors[(int)ImGuiCol.SeparatorHovered] = Accent;
        colors[(int)ImGuiCol.SeparatorActive] = Accent;

        colors[(int)ImGuiCol.ResizeGrip] = new Vector4(0f, 0f, 0f, 0f);
        colors[(int)ImGuiCol.ResizeGripHovered] = Grey(0.290f);
        colors[(int)ImGuiCol.ResizeGripActive] = Accent;

        colors[(int)ImGuiCol.Tab] = Sunken;
        colors[(int)ImGuiCol.TabHovered] = SurfaceHover;
        colors[(int)ImGuiCol.TabSelected] = Background;
        colors[(int)ImGuiCol.TabSelectedOverline] = Accent;
        colors[(int)ImGuiCol.TabDimmed] = Sunken;
        colors[(int)ImGuiCol.TabDimmedSelected] = Grey(0.129f);

        colors[(int)ImGuiCol.DockingPreview] = Fade(Accent, 0.45f);
        colors[(int)ImGuiCol.DockingEmptyBg] = Sunken;

        // ⚠️ Il bersaglio del drag&drop e il rettangolo di navigazione restano ACCESI e a
        // piena saturazione: sono gli unici due colori che devono interrompere. Uno slot che
        // accetta un modello si deve vedere mentre lo si sta trascinando sopra, non dopo.
        colors[(int)ImGuiCol.DragDropTarget] = new Vector4(1f, 0.78f, 0.24f, 0.95f);
        colors[(int)ImGuiCol.NavCursor] = Accent;

        colors[(int)ImGuiCol.TextSelectedBg] = Fade(Accent, 0.35f);
        colors[(int)ImGuiCol.ModalWindowDimBg] = new Vector4(0.05f, 0.05f, 0.06f, 0.6f);
    }

    /// <summary>Un grigio neutro e freddo: la componente blu leggermente più alta toglie il
    /// sapore "cenere" del grigio puro, che a schermo legge come sporco.</summary>
    private static Vector4 Grey(float level) => new(level, level + 0.006f, level + 0.016f, 1f);

    private static Vector4 Fade(Vector4 color, float alpha) => color with { W = alpha };
}
