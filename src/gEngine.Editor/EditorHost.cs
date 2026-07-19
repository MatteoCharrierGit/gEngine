using System.Numerics;
using gEngine.Core;
using gEngine.Ecs.Base;
using gEngine.Editor.Files;
using gEngine.Editor.Panels;
using gEngine.Input;
using gEngine.Rendering;
using gEngine.Rendering.Editor;
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
///   <item><see cref="Draw"/> da <c>IGame.Draw</c>, dentro <c>BeginFrame</c>/<c>EndFrame</c>;</item>
///   <item><see cref="Shutdown"/> da <c>IGame.Shutdown</c>, che gira prima di <c>CloseWindow</c>.</item>
/// </list>
/// Così il core engine non ha nessuna dipendenza da ImGui e un gioco che spedisce senza
/// editor semplicemente non referenzia questo progetto.
/// </summary>
public class EditorHost
{
    private readonly List<IEditorPanel> _panels = [];

    // Tenuti anche come campi concreti, oltre che nella lista: i viewport hanno un passo
    // in più rispetto al "disegnati e basta" dell'interfaccia (riempire il target prima
    // del frame ImGui), e il free-fly deve sapere se la vista Scena è sotto il puntatore.
    private ViewportPanel? _sceneView;
    private ViewportPanel? _gameView;

    private FreeFlyCamera3DController? _freeFly;

    private MainMenuBar? _menuBar;

    private bool _initialized;

    /// <summary>Selezione e stato condiviso fra i pannelli.</summary>
    public EditorContext Context { get; } = new();

    /// <summary>
    /// La camera della vista Scena. <b>Appartiene all'editor</b>, e questo è il punto:
    /// prima ce n'era una sola, contesa fra il <c>FreeFlyCamera3DController</c> (editor) e
    /// il <c>CameraFollowSystem</c> (gioco). Convivevano solo perché non si guardavano mai
    /// insieme — appena si vogliono due viste a schermo, la contesa diventa visibile:
    /// navigare la scena sposterebbe anche l'inquadratura del giocatore.
    ///
    /// ⚠️ Resta una <c>Camera3D</c> viva e fuori dal World, mentre quella del gioco è
    /// diventata un'entità (<c>CameraComponent</c> + <c>TransformComponent</c>). L'asimmetria
    /// è voluta e discende dalla regola "i dati di scena vivono nel World": questa non è
    /// dato di scena ma <b>stato dell'editor</b>. Se fosse un'entità, il <c>SceneSerializer</c>
    /// la scriverebbe dentro il file scena e comparirebbe nella Hierarchy del gioco — cioè
    /// spedire una scena significherebbe spedirci dentro la posizione da cui la si stava
    /// guardando. Unity tiene la scene camera fuori dalla scena per lo stesso motivo.
    /// </summary>
    public Camera3D SceneCamera { get; } = new()
    {
        Position = new Vector3(0, 18, 28),
        Target = new Vector3(0, 6, 0),
        Up = Vector3.UnitY,
        FovY = 60f
    };

    /// <summary>
    /// Se false l'editor non disegna e non consuma input: il gioco gira "nudo".
    ///
    /// ⚠️ Sola lettura: si cambia con <see cref="SetVisible"/>, e non è cerimonia. Nascondere
    /// l'editor <b>significa giocare</b>, e giocare significa prendere lo snapshot: se questo
    /// fosse un semplice assegnamento, il chiamante dovrebbe ricordarsene ogni volta.
    /// </summary>
    public bool Visible { get; private set; } = true;

    /// <summary>
    /// Mostra o nasconde l'editor (in pratica: F1).
    ///
    /// <b>Nasconderlo entra in Play</b>, snapshot compreso. La ragione è che "editor chiuso"
    /// e "gioco vero" sono la stessa cosa in questa classe da sempre — quindi chi preme F1 sta
    /// chiedendo di giocare, e chi gioca vuole poter tornare indietro. Senza, si usciva
    /// dall'editor in Editing, la scena si muoveva, e rientrando non c'era nessuno Stop da
    /// premere: il lavoro non salvato era perso.
    ///
    /// ⚠️ Il prezzo, dichiarato: F1 fa <b>due cose</b> — nasconde l'UI e fa partire il gioco —
    /// e si rientra in uno stato che non si era chiesto. Lo Stop acceso nella barra è ciò che
    /// lo ricorda. L'alternativa (F1 mostra la scena congelata) è più "pura" e sembra un gioco
    /// rotto.
    ///
    /// ⚠️ Non fa il verso opposto: rientrare <b>non</b> ferma il gioco. Fosse simmetrico, F1
    /// sarebbe un'anteprima usa-e-getta — si gioca, succede qualcosa di interessante, si
    /// rientra per guardarlo e non c'è più. Si ferma con Stop, quando lo si decide.
    /// </summary>
    public void SetVisible(World world, bool visible)
    {
        if (visible == Visible)
            return;

        Visible = visible;

        if (visible)
            return;

        // ⚠️ Se non parte (scena non serializzabile, registry non dichiarato) non si insiste:
        // ci pensa il fallback in ShouldSimulate, e il motivo resta in PlayMode.LastError per
        // la barra dei menu — che si vedrà appena si rientra.
        PlayMode.Start(world, Context);
    }

    /// <summary>Play / Pausa / Stop. Il gioco lo legge tramite <see cref="ShouldSimulate"/>.</summary>
    public PlayMode PlayMode { get; } = new();

    /// <summary>
    /// Se il gioco deve far girare i suoi system in questo tick. Da interrogare <b>prima</b>
    /// delle fasi Input/Simulation/Late — <b>non</b> di Render: in Editing la scena si deve
    /// vedere, ferma.
    ///
    /// Il gating sta nel gioco e non nel <c>SystemRegistry</c> perché è il gioco a chiamare
    /// le fasi: l'engine non sa (e non deve sapere) che esiste un editor. Qui c'è solo la
    /// verità su cui decide.
    ///
    /// <c>!Visible</c> resta nella condizione, ma ora è una <b>rete</b> e non più la regola:
    /// <see cref="SetVisible"/> entra in Play quando si chiude l'editor, quindi nel caso normale
    /// è già <c>PlayMode.ShouldSimulate</c> a dire di sì.
    ///
    /// ⚠️ Serve per il caso in cui il Play <b>non parte</b> (registry non dichiarato, scena non
    /// serializzabile): lì si gioca lo stesso, senza snapshot. Sembra il buco di prima e non lo
    /// è — un gioco che non sa serializzare la propria scena non aveva comunque niente da
    /// ripristinare, né qui né con un Salva. L'alternativa sarebbe mostrargli un gioco
    /// congelato a schermo intero in cambio di una garanzia che per lui non esiste.
    /// </summary>
    public bool ShouldSimulate => !Visible || PlayMode.ShouldSimulate;

    /// <summary>
    /// True quando il puntatore è sopra un pannello (o ImGui sta trascinando qualcosa):
    /// il gioco deve ignorare il mouse, altrimenti un clic su un pannello muove anche la
    /// camera. Da interrogare <b>prima</b> di leggere l'input, non dopo.
    ///
    /// ⚠️ Non usarlo per la camera di scena: da quando il 3D vive dentro un pannello, il
    /// puntatore sopra la vista è sopra una finestra ImGui e questo è sempre true. Ci
    /// pensa <see cref="Update"/>, col gate giusto.
    /// </summary>
    public bool WantsMouse => _initialized && Visible && ImGui.GetIO().WantCaptureMouse;

    /// <summary>Come <see cref="WantsMouse"/>, ma per la tastiera (es. focus su un campo di testo).</summary>
    public bool WantsKeyboard => _initialized && Visible && ImGui.GetIO().WantCaptureKeyboard;

    /// <param name="input">Serve al free-fly della camera di scena, che l'editor ora guida da sé.</param>
    /// <param name="drawWorld">Come si disegna il mondo: lo sa il gioco, non l'editor. Vedi <see cref="WorldRenderer"/>.</param>
    /// <param name="gameCamera">
    /// Come si ottiene l'inquadratura del giocatore, <b>richiesta a ogni frame</b>.
    ///
    /// ⚠️ Era una <c>Camera3D</c>, e funzionava per un motivo che non esiste più: essendo
    /// una <c>class</c>, il riferimento preso qui restava agganciato all'oggetto che il
    /// gioco muoveva. Ora la camera del gioco è un'<b>entità del World</b> e la
    /// <c>Camera3D</c> è derivata da Transform + CameraComponent: è un oggetto nuovo a ogni
    /// richiesta, quindi tenerne uno vorrebbe dire mostrare per sempre il primo frame.
    /// Il gioco resta comunque l'unico a sapere quale sia la sua camera — l'editor chiede.
    ///
    /// Può restituire <c>null</c>: una scena senza camera è uno stato legittimo.
    /// </param>
    /// <param name="document">
    /// La scena su file su cui lavorare, o <c>null</c> se il gioco non ne ha una (entità
    /// costruite in codice): in quel caso l'editor funziona lo stesso, ma senza Salva.
    /// </param>
    /// <param name="resources">
    /// L'infrastruttura del gioco, da cui i pannelli pescano ciò che gli serve (oggi il
    /// <c>SystemRegistry</c>, per la traceability nell'Inspector).
    ///
    /// Arriva qui e finisce nel <see cref="Context"/> invece di essere un parametro per
    /// servizio: vedi <see cref="EditorContext.Resources"/>. Opzionale — un gioco che non
    /// la passa perde le sezioni che ne dipendono, non l'editor.
    /// </param>
    public void Setup(InputHandler input, WorldRenderer drawWorld, Func<Camera3D?> gameCamera,
        SceneDocument? document = null, Resources? resources = null)
    {
        rlImGui.Setup(darkTheme: true, enableDocking: true);

        // Dopo rlImGui.Setup, che crea il contesto ImGui: prima non ci sarebbe uno stile su
        // cui scrivere. Il darkTheme qui sopra resta come base ragionevole per ciò che il
        // tema non tocca.
        EditorTheme.Apply();

        Context.Resources = resources;

        _freeFly = new FreeFlyCamera3DController(input, SceneCamera);
        _freeFly.Init();

        // Selezione e gizmi sono solo della vista Scena: nella vista Gioco si guarda cosa
        // vedrà il giocatore, e un clic lì è un clic del gioco, non dell'editor.
        //
        // Le posizioni di default lasciano libere le colonne degli altri pannelli
        // (Hierarchy a sinistra, Inspector a destra) e impilano le due viste in mezzo.
        // La camera di scena è dell'editor e vive per tutta la sessione: qui il provider è
        // una formalità per usare la stessa classe. Che le due viste chiedano la camera allo
        // stesso modo è il punto — la differenza fra loro sta in DOVE quella camera vive,
        // non in come il viewport la ottiene.
        _sceneView = new ViewportPanel("Scena", () => SceneCamera, drawWorld,
            interactive: true, new Vector2(320, 40), new Vector2(1000, 560));

        _gameView = new ViewportPanel("Gioco", gameCamera, drawWorld,
            interactive: false, new Vector2(320, 620), new Vector2(1000, 400));

        _panels.Add(_sceneView);
        _panels.Add(_gameView);
        _panels.Add(new HierarchyPanel());
        _panels.Add(new InspectorPanel());

        // La cartella degli asset è dedotta e non chiesta al gioco: è la stessa convenzione
        // che il gioco stesso usa per trovare le sue scene (vedi SandboxGame.Init), cioè
        // "assets" accanto all'eseguibile, e un parametro in più su Setup per ridire la
        // convenzione avrebbe solo dato modo di sbagliarla. ⚠️ Se un gioco terrà i suoi asset
        // altrove, questo è il punto da aprire — il pannello dice già "cartella non trovata"
        // invece di far finta di niente.
        _panels.Add(new FileSystemPanel(
            Path.Combine(ContentRoot.Path, "assets"),
            FileTrash.ForCurrentPlatform()));

        // Gli ultimi due nascono spenti (vedi i loro costruttori): sono nella lista perché è
        // da lì che il menu Panels li elenca — un pannello che non è registrato qui non ha
        // nessun modo di essere riacceso.
        _panels.Add(new SystemsPanel());
        _panels.Add(new ComponentsPanel());

        // Anche questo nasce spento, ma a differenza degli altri due si accende DA SE' quando
        // uno script non compila: e' l'unico pannello a cui e' concesso, perche' ha da dire una
        // cosa che l'utente non sa ancora di dover chiedere. Vedi ScriptsPanel.
        _panels.Add(new ScriptsPanel());

        // La Console nasce ACCESA, al contrario degli altri: non e' un inventario che si
        // consulta quando serve, e' il flusso di cio' che il gioco sta facendo. Un log che
        // bisogna ricordarsi di aprire e' un log che si guarda solo dopo aver gia' perso tempo
        // a cercare altrove. Come lo ScriptsPanel si tira su da se' su un errore nuovo.
        _panels.Add(new ConsolePanel());

        _menuBar = new MainMenuBar(document, PlayMode);

        _initialized = true;
    }

    /// <summary>
    /// Muove la camera della vista Scena. Da chiamare dall'update del gioco.
    ///
    /// Il free-fly è passato di qui perché la camera che muove è dell'editor. Prima lo
    /// faceva girare il gioco, con un guard su <see cref="WantsMouse"/> — che però smette
    /// di funzionare esattamente quando serve: col 3D dentro un pannello, "ImGui vuole il
    /// mouse" è vero proprio mentre punti la scena. Il gate giusto è "il puntatore è sopra
    /// la vista Scena", e lo sa solo il viewport.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (!_initialized || _freeFly is null || _sceneView is null)
            return;

        // Editor chiuso (F1) a metà volo: il rilascio del destro cadrebbe in un frame in
        // cui non stiamo guardando, e il volo non finirebbe mai più. Vedi Cancel.
        if (!Visible)
        {
            _freeFly.Cancel();
            return;
        }

        _freeFly.OnUpdate(deltaTime, canStartCapture: _sceneView.IsHovered);
    }

    public void Draw(World world, IRenderer renderer, float deltaTime)
    {
        if (!_initialized || !Visible)
            return;

        // Le due viste si riempiono PRIMA di aprire il frame ImGui: dentro, il pannello
        // mostra una texture già pronta. Il ritardo di un frame sulla taglia del pannello
        // è il prezzo — vedi ViewportPanel.
        _sceneView?.RenderToTarget(renderer);
        _gameView?.RenderToTarget(renderer);

        rlImGui.Begin(deltaTime);

        // ⚠️ La barra PRIMA del dockspace, e non è indifferente: BeginMainMenuBar toglie la
        // propria altezza dalla work area del viewport, e DockSpaceOverViewport si dispone
        // esattamente su quella. Invertendo l'ordine il dockspace leggerebbe la work area di
        // un viewport che ancora non sa della barra, e i pannelli agganciati in alto
        // finirebbero sotto di essa.
        _menuBar?.Draw(world, Context, _panels);

        // Host di docking a tutto schermo: dà ai pannelli i bordi a cui agganciarsi.
        // PassthruCentralNode lascia trasparente il nodo centrale finché nessuno ci
        // aggancia niente: senza, la finestra host coprirebbe lo schermo col proprio
        // sfondo opaco.
        ImGui.DockSpaceOverViewport(0, ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);

        foreach (var panel in _panels)
            panel.Draw(world, Context, renderer);

        // Dopo i pannelli: le scorciatoie non devono rubare il tasto a chi lo stava usando nel
        // frame corrente. Vedi DrawUndoShortcuts.
        DrawUndoShortcuts(world);

        rlImGui.End();
    }

    /// <summary>
    /// Ctrl+Z / Ctrl+Y, e Ctrl+Shift+Z perché mezzo mondo rifà così.
    ///
    /// ⚠️ <b>Non mentre si scrive in un campo di testo</b>: dentro un <c>InputText</c> ImGui ha
    /// il <b>suo</b> annulla, che è quello che ci si aspetta lì (disfa le lettere, non l'ultima
    /// entità creata). Rubargli il tasto vorrebbe dire che rinominare un'entità e sbagliare una
    /// lettera annulla qualcos'altro nella scena. <c>WantTextInput</c> è il segnale giusto, ed è
    /// più stretto di <c>WantCaptureKeyboard</c>: quest'ultimo è vero ogni volta che una
    /// finestra ImGui ha il fuoco, cioè quasi sempre — e le scorciatoie non funzionerebbero mai.
    ///
    /// Le maiuscole non c'entrano: <c>ImGuiKey.Z</c> è il tasto fisico.
    /// </summary>
    private void DrawUndoShortcuts(World world)
    {
        var io = ImGui.GetIO();

        if (io.WantTextInput || !io.KeyCtrl)
            return;

        var shift = io.KeyShift;

        if (ImGui.IsKeyPressed(ImGuiKey.Z, repeat: true))
        {
            if (shift)
                Context.Undo.Redo(world);
            else
                Context.Undo.Undo(world);
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Y, repeat: true))
            Context.Undo.Redo(world);
    }

    public void Shutdown()
    {
        if (!_initialized)
            return;

        // I render target dei viewport non si liberano qui: qui non c'è un IRenderer in
        // mano (IGame.Shutdown non lo riceve), e la tabella è comunque del renderer, che
        // spazza via ciò che resta nel proprio Shutdown.
        rlImGui.Shutdown();
        _initialized = false;
    }
}
