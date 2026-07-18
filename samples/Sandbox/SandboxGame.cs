using System.Text.Json;
using gEngine.Assets;
using gEngine.Core;
using gEngine.Ecs;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using Sandbox.Components;
using gEngine.Ecs.System;
using gEngine.Editor;
using gEngine.Input;
using gEngine.Physics;
using gEngine.Rendering;
using gEngine.Scenes;
using gEngine.Editor.Scripting;
using gEngine.Scripting;
using gEngine.Scenes.Json;
using Raylib_cs;
using Camera3D = gEngine.Rendering.Camera3D;
using Color = gEngine.Rendering.Color;
using Vector3 = System.Numerics.Vector3;
using World = gEngine.Ecs.Base.World;


namespace Sandbox;

public class SandboxGame() : IGame
{
    private readonly World _world = new World();

    // Niente campo _gameCamera: la camera DEL GIOCO è un'entità di demo.json
    // (CameraComponent + TransformComponent), quindi si cerca nel World come ogni altro
    // dato di scena. La camera con cui si naviga nell'editor è un'altra cosa e resta
    // dell'editor (EditorHost.SceneCamera): è stato dell'editor, non dato di scena.

    // L'editor è posseduto dal gioco, non dal GameLoop: vedi il commento su EditorHost.
    private readonly EditorHost _editor = new EditorHost();


    // I system li possiede l'engine, non il gioco: prima erano quattro liste e quattro
    // overload di AddSystem qui dentro. Assegnato in Init perché il registry vuole il World
    // (un field initializer non può leggere un altro field dell'istanza).
    private SystemRegistry _systems = null!;

    // L'infrastruttura del gioco, dichiarata invece che sparsa: renderer, asset, fisica,
    // input. Fuori dal World apposta — vedi il commento su Resources. Il contenitore è
    // del GameLoop e arriva già popolato in Init: qui ci aggiungiamo solo la fisica.
    private Resources _resources = null!;

    // AssetManager posseduto dal GameLoop, letto dalle Resources in Init (vedi IGame.Init).
    private AssetManager _assetManager = null!;

    // Tenuto qui perché serve anche in Draw (toggle dell'editor), che a differenza di
    // Update non lo riceve come parametro.
    private InputHandler _inputHandler = null!;

    // Mondo fisico (adapter Bepu), posseduto dal gioco.
    // il suo ciclo di vita non è legato alla finestra: lo creo qui e lo dispongo in Shutdown.
    private readonly IPhysicsWorld _physics = new BepuPhysicsWorld(new Vector3(0, -9.81f, 0));

    private MusicHandle _introSound;

    private SceneDocument _sceneDocument = null!;

    // Gli script compilati a runtime da assets/scripts/. Tenuto perché serve due volte in
    // Init (componenti e system) e perché l'editor lo legge dalle Resource.
    private ScriptCompilation _scripts = null!;


    public void Init(Resources resources)
    {
        _resources = resources;
        _assetManager = resources.Get<AssetManager>();
        _inputHandler = resources.Get<InputHandler>();

        var gameActionContext = new GameActionContext();
        gameActionContext.AddToContext([new InputBinding(){KKey = KeyboardKey.W}], GameAction.MoveUp);
        gameActionContext.AddToContext([new InputBinding(){KKey = KeyboardKey.S}], GameAction.MoveDown);
        gameActionContext.AddToContext([new InputBinding(){KKey = KeyboardKey.A}], GameAction.MoveLeft);
        gameActionContext.AddToContext([new InputBinding(){KKey = KeyboardKey.D}], GameAction.MoveRight);
        gameActionContext.AddToContext([new InputBinding(){KKey = KeyboardKey.Space}], GameAction.CameraCenter);
        gameActionContext.AddToContext([new InputBinding(){MButton = MouseButton.Right}], GameAction.CameraFreeFly);
        gameActionContext.AddToContext([new InputBinding(){KKey = KeyboardKey.F1}], GameAction.ToggleEditor);

        _inputHandler.SetActiveContext(gameActionContext);

        _systems = new SystemRegistry(_world);

        // Input, asset e renderer li ha già registrati il GameLoop, che li possiede. Fisica
        // e registry dei system no: li crea il gioco, quindi li dichiara il gioco. Il
        // contenitore è condiviso — non impone chi crea cosa, solo che chi crea dichiari.
        _resources.Add(_physics);

        // Dichiarare il registry non serve a noi (ce l'abbiamo in un campo): serve a chi non
        // può conoscerlo, cioè l'editor — è così che l'Inspector arriva a dire quali system
        // stanno lavorando sull'entità selezionata.
        _resources.Add(_systems);

        // Registry dei componenti: i built-in dell'engine (Transform, MeshRenderer, Light,
        // RigidBody, Camera, Parent) restano espliciti perché hanno binder ASIMMETRICI che un
        // attributo non saprebbe esprimere — MeshRenderer converte un path in handle, Parent
        // risolve un nome in Entity.
        //
        // I componenti del GIOCO no: si dichiarano da soli con [GameComponent] e li trova la
        // riga qui sotto. Prima erano due Register a mano, e il prezzo non era la verbosità —
        // era che scrivere un componente nuovo significava ricordarsi di venire a citarlo QUI,
        // in un file che non è quello che stai scrivendo. Dimenticarsene non dava un errore di
        // compilazione: dava una scena che non si salvava.
        var registry = new SceneComponentRegistry();
        registry.RegisterEngineDefaults();
        ScriptDiscovery.RegisterComponents(typeof(SandboxGame).Assembly, registry);

        // GLI SCRIPT: i .cs sotto assets/scripts/, compilati adesso. Non sono file del
        // .csproj (vedi il Compile Remove lì dentro): per il progetto sono dati, come un
        // modello. Chi li scrive non tocca né questo file né il progetto.
        //
        // ⚠️ Non si lancia se non compilano: un errore di battitura è il caso normale di chi
        // scrive codice, e buttare giù il gioco a ogni punto e virgola sarebbe inutilizzabile.
        // L'esito va dichiarato fra le Resource perché è l'UNICO modo che l'editor ha di
        // saperlo: quando la compilazione fallisce non esiste nessun assembly, nessun system e
        // nessun componente da cui dedurre che sia successo qualcosa. Lo mostra il pannello
        // Scripts, che si apre da sé.
        _scripts = ScriptCompiler.Compile(Path.Combine(ContentRoot.Path, "assets", "scripts"));
        _resources.Add(_scripts);

        if (_scripts.Assembly is { } scriptAssembly)
            ScriptDiscovery.RegisterComponents(scriptAssembly, registry);

        // Dichiarato come Resource per lo stesso motivo del SystemRegistry: serve a chi non
        // può conoscerlo. Il SceneDocument qui sotto ne riceve già uno, ma quel canale arriva
        // solo al salvataggio — è da qui che l'Inspector prende l'elenco dei componenti
        // aggiungibili.
        _resources.Add(registry);

        // Scena caricata da file: niente entità hardcoded qui. Luci, modelli (ModelPath) e
        // gerarchia (Parent) sono gestiti dai binder built-in dell'engine.
        var scenePath = Path.Combine(ContentRoot.Path, "assets", "scenes", "demo.json");
        var scene = JsonSceneLoader.Load(scenePath);
        SceneInstantiator.Instantiate(scene, _world, registry, _assetManager);

        // Ciò che l'editor non può indovinare: il registry (che contiene i componenti
        // custom di Sandbox) e dove sta la scena.
        _sceneDocument = new SceneDocument
        {
            World = _world,
            Registry = registry,
            Assets = _assetManager,
            Path = scenePath,
            Source = scene
        };

        // I system del GIOCO si dichiarano da soli con [GameSystem] e li trova questa riga.
        // Movement, PlayerInput e CameraFollow non sono più citati qui — e da adesso non sono
        // nemmeno più file di questo progetto: stanno in assets/scripts/. Le loro dipendenze
        // (l'InputHandler) le risolve la scoperta dalle Resource — che è il motivo per cui le
        // Resource dovevano essere un elenco dichiarato e non campi sparsi.
        //
        // ⚠️ DOVE sta questa riga conta: i system finiscono nel registry nell'ordine in cui li
        // si aggiunge, e dentro una fase l'ordine è comportamento. Gli script vanno PRIMA dei
        // render system dell'engine qui sotto; l'attributo li ordina solo fra loro. È l'unico
        // punto in cui questa decisione si prende, ed è voluto che sia una riga visibile
        // invece di una regola nascosta dentro la scoperta.
        if (_scripts.Assembly is { } systemsAssembly)
            ScriptDiscovery.RegisterSystems(systemsAssembly, _systems, _resources);

        // I system dell'ENGINE restano espliciti: non sono script del gioco, e il gioco deve
        // poter scegliere quali far girare.
        _systems.Add(new PhysicsSystem(_physics));

        // LightingSystem PRIMA di MeshRenderSystem: carica le uniform delle luci prima di disegnare.
        _systems.Add(new LightingSystem());
        _systems.Add(new MeshRenderSystem());


        _introSound = _assetManager.LoadMusicStream("audio/Before_the_Light_Fades.mp3");
        _assetManager.PlayMusic(_introSound);

        // Dopo InitWindow (Init è chiamato dal GameLoop a finestra già aperta): rlImGui
        // carica la texture dell'atlas dei font, quindi vuole un contesto grafico vivo.
        //
        // DrawWorld è ciò che l'editor non può sapere da sé: i render system sono del
        // gioco. Idem quale sia la camera del giocatore, che la vista "Gioco" deve mostrare
        // (la vista "Scena" usa quella dell'editor, che l'editor si fa da sé).
        //
        // Passata come funzione e non come oggetto: ora la Camera3D del gioco è DERIVATA dal
        // World a ogni richiesta, quindi non c'è un'istanza a cui restare agganciati.
        _editor.Setup(_inputHandler, DrawWorld, GetGameCamera, _sceneDocument, _resources);
    }

    public void Update(float fixedDeltaTime, InputHandler  inputHandler)
    {
        // Il gating del Play/Stop, e sta qui perché è il gioco a chiamare le fasi: l'engine
        // non sa che esiste un editor, e l'editor non sa quali fasi il gioco faccia girare.
        // In Editing i system stanno fermi — è tutto il senso di "autorare" invece di
        // rincorrere una scena che simula. Vedi EditorHost.ShouldSimulate.
        //
        // ⚠️ Render NON è qui: la scena si deve vedere anche da ferma. La fase Render gira in
        // Draw e non è gated da niente.
        var simulate = _editor.ShouldSimulate;

        // ImGui e il gioco leggono lo stesso mouse/tastiera. Quando l'editor li sta usando
        // (puntatore sopra un pannello, campo col focus) il gioco deve stare fermo: senza
        // questi due guard, un clic su un pannello farebbe anche ruotare la camera.
        if (simulate && !_editor.WantsKeyboard)
            _systems.RunInput(fixedDeltaTime);

        // La camera di scena la muove l'editor, che è quello che sa se il puntatore è
        // sopra la vista Scena. Prima il free-fly girava qui, gated su WantsMouse: quel
        // guard è diventato inutilizzabile per questo scopo da quando il 3D sta dentro
        // un pannello — vedi EditorHost.Update.
        //
        // ⚠️ Fuori dal gate: navigare la scena è dell'editor, non del gioco. In Editing la
        // scena sta ferma ma ci si deve poter girare intorno — è proprio lì che serve.
        _editor.Update(fixedDeltaTime);

        if (!simulate)
            return;

        _systems.RunSimulation(fixedDeltaTime);
        _systems.RunLate(fixedDeltaTime);
    }

    public void Draw(IRenderer renderer)
    {
        // Il toggle sta qui e non in Update perché Update è a passo fisso: in un frame
        // lento gira più volte, e un input edge-triggered come "F1 premuto" verrebbe
        // consumato a ogni iterazione, ribaltando l'editor due volte (= nessun effetto).
        // Draw gira esattamente una volta per frame, come il polling dell'input.
        // SetVisible e non un assegnamento a Visible: chiudere l'editor ENTRA IN PLAY,
        // snapshot compreso — chi preme F1 sta chiedendo di giocare, e chi gioca deve poter
        // tornare indietro. Vedi EditorHost.SetVisible.
        if (_inputHandler.IsActionPressed(GameAction.ToggleEditor))
            _editor.SetVisible(_world, !_editor.Visible);

        renderer.BeginFrame();
        renderer.ClearBackground(Color.White);

        // Con l'editor aperto il mondo NON si disegna qui: lo disegnano i due viewport,
        // ognuno nel proprio render target e con la propria camera. A tutto schermo si
        // torna solo quando l'editor è chiuso (F1), ed è il gioco vero e proprio.
        //
        // La camera si rilegge dal World ogni frame: è lì che il CameraFollowSystem l'ha
        // mossa. Senza camera non si disegna il mondo e basta — l'HUD e l'editor restano,
        // così si vede cos'è successo invece di cadere.
        if (!_editor.Visible && GetGameCamera() is { } gameCamera)
            DrawWorld(renderer, gameCamera);

        // Nullable + FirstOrDefault, non First: da quando l'editor sa eliminare entità,
        // "esiste sempre un player" non è più un invariante — e un HUD non deve far cadere
        // il gioco perché non ha niente da mostrare.
        var playerTransform = _world.Query<TransformComponent, VelocityComponent>()
            .Select(query => (TransformComponent?)query.C1)
            .FirstOrDefault();

        const int hudPadding = 20;
        const int hudBoxWidth = 220;
        const int hudBoxHeight = 40;
        var hudBoxX = hudPadding;
        var hudBoxY = renderer.GetScreenHeight() - hudBoxHeight - hudPadding;

        var hudText = playerTransform is { } transform
            ? $"Pos: ({transform.Position.X:F0}, {transform.Position.Y:F0})"
            : "Nessun player";

        renderer.DrawRectangle(hudBoxX, hudBoxY, hudBoxWidth, hudBoxHeight, new Color(0, 0, 0, 150));
        renderer.DrawText(hudText, hudBoxX + 10, hudBoxY + 10, 20, Color.White);
        renderer.DrawText("GameTime: " +  Raylib.GetTime(), 20, 20, 30, Color.Black);

        // Per ultimo, ancora dentro il frame: i pannelli vanno sopra scena e HUD.
        _editor.Draw(_world, renderer, renderer.GetFrameTime());

        renderer.EndFrame();

        // audio management
        _assetManager.UpdateMusic(_introSound);
    }

    /// <summary>
    /// L'inquadratura del giocatore, risolta dal World: l'entità con <c>CameraComponent</c>
    /// marcata <c>Primary</c> (in demo.json è "game-camera").
    ///
    /// ⚠️ Nullable, e i due chiamanti devono reggerlo: "esiste sempre una camera" NON è un
    /// invariante da quando l'editor sa cancellare entità — è la stessa trappola del player
    /// cancellato che faceva cadere l'HUD, e la camera è persino più facile da cancellare
    /// per sbaglio, perché ora si vede nella Hierarchy.
    /// </summary>
    private Camera3D? GetGameCamera() => _world.GetPrimaryCamera();

    /// <summary>
    /// Disegna il mondo da una camera qualunque. Sta qui e non nell'editor perché i render
    /// system sono del gioco; l'editor lo riceve come <see cref="WorldRenderer"/> e lo
    /// chiama due volte per frame, una per vista.
    ///
    /// Il blocco Begin3D/End3D è dentro: chi disegna possiede il proprio blocco 3D, il
    /// viewport si occupa solo di dove finiscono i pixel.
    /// </summary>
    private void DrawWorld(IRenderer renderer, Camera3D camera)
    {
        renderer.Begin3D(camera);

        _systems.RunRender(renderer, camera, renderer.GetFrameTime());

        renderer.End3D();
    }

    public void Shutdown()
    {
        // Le risorse asset sono scaricate dal GameLoop (che possiede l'AssetManager).
        // Shutdown gira prima di CloseWindow: rlImGui fa in tempo a liberare le sue texture.
        _editor.Shutdown();
        _physics.Dispose();
    }
}