using gEngine.Ecs.Base;
using gEngine.Editor.Panels;
using ImGuiNET;
using rlImGui_cs;

namespace gEngine.Editor;

/// <summary>
/// Punto d'ingresso dell'editor: possiede il ciclo di vita di ImGui e disegna i pannelli.
///
/// È il gioco a possedere l'<c>EditorHost</c>, non il <c>GameLoop</c>. Il motivo è che i
/// tre agganci di cui ImGui ha bisogno esistono già in <c>IGame</c>, con le garanzie giuste:
/// <list type="bullet">
///   <item><see cref="Setup"/> da <c>IGame.Init</c>, che gira dopo <c>InitWindow</c>
///   (rlImGui carica texture GPU: serve un contesto grafico);</item>
///   <item><see cref="Draw"/> da <c>IGame.Draw</c>, dentro <c>BeginFrame</c>/<c>EndFrame</c>
///   e dopo la scena, così i pannelli finiscono sopra il 3D;</item>
///   <item><see cref="Shutdown"/> da <c>IGame.Shutdown</c>, che gira prima di <c>CloseWindow</c>.</item>
/// </list>
/// Così il core engine non ha nessuna dipendenza da ImGui e un gioco che spedisce senza
/// editor semplicemente non referenzia questo progetto.
/// </summary>
public class EditorHost
{
    private readonly List<IEditorPanel> _panels = [];

    private bool _initialized;

    /// <summary>Selezione e stato condiviso fra i pannelli.</summary>
    public EditorContext Context { get; } = new();

    /// <summary>Se false l'editor non disegna e non consuma input: il gioco gira "nudo".</summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// True quando il puntatore è sopra un pannello (o ImGui sta trascinando qualcosa):
    /// il gioco deve ignorare il mouse, altrimenti un clic su un pannello muove anche la
    /// camera. Da interrogare <b>prima</b> di leggere l'input, non dopo.
    /// </summary>
    public bool WantsMouse => _initialized && Visible && ImGui.GetIO().WantCaptureMouse;

    /// <summary>Come <see cref="WantsMouse"/>, ma per la tastiera (es. focus su un campo di testo).</summary>
    public bool WantsKeyboard => _initialized && Visible && ImGui.GetIO().WantCaptureKeyboard;

    /// <param name="document">
    /// La scena su file su cui lavorare, o <c>null</c> se il gioco non ne ha una (entità
    /// costruite in codice): in quel caso l'editor funziona lo stesso, ma senza Salva.
    /// </param>
    public void Setup(SceneDocument? document = null)
    {
        rlImGui.Setup(darkTheme: true, enableDocking: true);

        _panels.Add(new HierarchyPanel());
        _panels.Add(new InspectorPanel());
        _panels.Add(new ScenePanel(document));

        _initialized = true;
    }

    public void Draw(World world, float deltaTime)
    {
        if (!_initialized || !Visible)
            return;

        rlImGui.Begin(deltaTime);

        // Host di docking a tutto schermo: dà ai pannelli i bordi a cui agganciarsi.
        // PassthruCentralNode è ciò che rende visibile la scena 3D sotto: senza, la
        // finestra host coprirebbe lo schermo col proprio sfondo opaco.
        ImGui.DockSpaceOverViewport(0, ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);

        foreach (var panel in _panels)
            panel.Draw(world, Context);

        rlImGui.End();
    }

    public void Shutdown()
    {
        if (!_initialized)
            return;

        rlImGui.Shutdown();
        _initialized = false;
    }
}
