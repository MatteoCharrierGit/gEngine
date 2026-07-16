using System.Text.Json;
using gEngine.Assets;
using gEngine.Core;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Ecs.Interfaces.System;
using gEngine.Ecs.System;
using gEngine.Editor;
using gEngine.Input;
using gEngine.Physics;
using gEngine.Rendering;
using gEngine.Rendering.Editor;
using gEngine.Scenes;
using gEngine.Scenes.Json;
using Raylib_cs;
using Sandbox.Systems;
using Camera3D = gEngine.Rendering.Camera3D;
using Color = gEngine.Rendering.Color;
using Vector3 = System.Numerics.Vector3;
using World = gEngine.Ecs.Base.World;


namespace Sandbox;

public class SandboxGame() : IGame
{
    private readonly World _world = new World();
    
    private Camera3D _camera =  new Camera3D()
    {
        Position = new Vector3(0, 18, 28),
        Target = new Vector3(0, 6, 0),
        Up = Vector3.UnitY,
        FovY = 60f,
        Projection = CameraProjection.Perspective,  
    };
    
    private FreeFlyCamera3DController _freeFlyCamera3DController;

    // L'editor è posseduto dal gioco, non dal GameLoop: vedi il commento su EditorHost.
    private readonly EditorHost _editor = new EditorHost();


    private List<IInputSystem>? _inputSystems;
    private List<ISimulationSystem>? _simulationSystems;
    private List<ILateSystem>? _lateSystems;
    private List<IRenderSystem>? _renderSystems;
    
    
    // AssetManager posseduto dal GameLoop e ricevuto in Init (vedi IGame.Init).
    private AssetManager _assetManager = null!;

    // Ricevuto in Init e tenuto qui perché serve anche in Draw (toggle dell'editor),
    // che a differenza di Update non lo riceve come parametro.
    private InputHandler _inputHandler = null!;

    // Mondo fisico (adapter Bepu), posseduto dal gioco.
    // il suo ciclo di vita non è legato alla finestra: lo creo qui e lo dispongo in Shutdown.
    private readonly IPhysicsWorld _physics = new BepuPhysicsWorld(new Vector3(0, -9.81f, 0));

    private TextureHandle _playerSprite;
    private MusicHandle _introSound;

    private SceneDocument _sceneDocument = null!;
    

    public void Init(InputHandler inputHandler, AssetManager assets)
    {
        _assetManager = assets;
        _inputHandler = inputHandler;

        var gameActionContext = new GameActionContext();
        gameActionContext.AddToContext([new InputBinding(){KKey = KeyboardKey.W}], GameAction.MoveUp);
        gameActionContext.AddToContext([new InputBinding(){KKey = KeyboardKey.S}], GameAction.MoveDown);
        gameActionContext.AddToContext([new InputBinding(){KKey = KeyboardKey.A}], GameAction.MoveLeft);
        gameActionContext.AddToContext([new InputBinding(){KKey = KeyboardKey.D}], GameAction.MoveRight);
        gameActionContext.AddToContext([new InputBinding(){KKey = KeyboardKey.Space}], GameAction.CameraCenter);
        gameActionContext.AddToContext([new InputBinding(){MButton = MouseButton.Right}], GameAction.CameraFreeFly);
        gameActionContext.AddToContext([new InputBinding(){KKey = KeyboardKey.F1}], GameAction.ToggleEditor);

        inputHandler.SetActiveContext(gameActionContext);
        _freeFlyCamera3DController = new FreeFlyCamera3DController(inputHandler, _camera);
        
        _inputSystems = [];
        _simulationSystems = [];
        _lateSystems = [];
        _renderSystems = [];

        // Registry dei componenti: built-in dell'engine (Transform, MeshRenderer)
        // + componenti custom del gioco (Player, Velocity), stesso formato JSON.
        var registry = new SceneComponentRegistry();
        registry.RegisterEngineDefaults();
        registry.Register("Player", data => data.Deserialize<PlayerComponent>(SceneJson.Options));
        registry.Register("Velocity", data => data.Deserialize<VelocityComponent>(SceneJson.Options));

        // Scena caricata da file: niente entità hardcoded qui. Luci, modelli (ModelPath) e
        // gerarchia (Parent) sono gestiti dai binder built-in dell'engine.
        var scenePath = Path.Combine(AppContext.BaseDirectory, "assets", "scenes", "demo.json");
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

        AddSystem(new MovementSystem());
        AddSystem(new PlayerInputSystem(inputHandler));
        AddSystem(new CameraFollowSystem(_camera, inputHandler));
        AddSystem(new PhysicsSystem(_physics));

        // LightingSystem PRIMA di MeshRenderSystem: carica le uniform delle luci prima di disegnare.
        AddSystem(new LightingSystem());
        AddSystem(new MeshRenderSystem());


        _introSound = _assetManager.LoadMusicStream("audio/Before_the_Light_Fades.mp3");
        _assetManager.PlayMusic(_introSound);
        
        _freeFlyCamera3DController.Init();

        // Dopo InitWindow (Init è chiamato dal GameLoop a finestra già aperta): rlImGui
        // carica la texture dell'atlas dei font, quindi vuole un contesto grafico vivo.
        _editor.Setup(_sceneDocument);
    }

    public void Update(float fixedDeltaTime, InputHandler  inputHandler)
    {
        if (_inputSystems == null) return;

        // ImGui e il gioco leggono lo stesso mouse/tastiera. Quando l'editor li sta usando
        // (puntatore sopra un pannello, campo col focus) il gioco deve stare fermo: senza
        // questi due guard, un clic su un pannello farebbe anche ruotare la camera.
        if (!_editor.WantsKeyboard)
            foreach (var system in _inputSystems)
                system.OnUpdate(_world, fixedDeltaTime);

        if (!_editor.WantsMouse)
            _freeFlyCamera3DController.OnUpdate(fixedDeltaTime);

        if (_simulationSystems == null) return;
        
        foreach (var system in _simulationSystems)
            system.OnUpdate(_world, fixedDeltaTime);
        
        if (_lateSystems == null) return;
        
        foreach (var system in _lateSystems)
            system.OnUpdate(_world, fixedDeltaTime);
    }

    public void Draw(IRenderer renderer)
    {
        // Il toggle sta qui e non in Update perché Update è a passo fisso: in un frame
        // lento gira più volte, e un input edge-triggered come "F1 premuto" verrebbe
        // consumato a ogni iterazione, ribaltando l'editor due volte (= nessun effetto).
        // Draw gira esattamente una volta per frame, come il polling dell'input.
        if (_inputHandler.IsActionPressed(GameAction.ToggleEditor))
            _editor.Visible = !_editor.Visible;

        renderer.BeginFrame();
        renderer.ClearBackground(Color.White);
        
        renderer.Begin3D(_camera);

        if (_renderSystems != null)
            foreach (var system in _renderSystems)
                system.OnRender(_world, renderer, _camera, renderer.GetFrameTime());

        renderer.End3D();
        
        
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
        _editor.Draw(_world, renderer.GetFrameTime());

        renderer.EndFrame();
        
        // audio management
        _assetManager.UpdateMusic(_introSound);
    }

    public void Shutdown()
    {
        // Le risorse asset sono scaricate dal GameLoop (che possiede l'AssetManager).
        // Shutdown gira prima di CloseWindow: rlImGui fa in tempo a liberare le sue texture.
        _editor.Shutdown();
        _physics.Dispose();
    }

    private void AddSystem(IInputSystem system)
    {
        _inputSystems?.Add(system);
        system.OnCreate(_world);
    }
    
    private void AddSystem(ISimulationSystem system)
    {
        _simulationSystems?.Add(system);
        system.OnCreate(_world);
    }
    
    private void AddSystem(ILateSystem system)
    {
        _lateSystems?.Add(system);
        system.OnCreate(_world);
    }

    private void AddSystem(IRenderSystem system)
    {
        _renderSystems?.Add(system);
        system.OnCreate(_world);
    }
}