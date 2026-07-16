using System.Text.Json;
using gEngine.Assets;
using gEngine.Core;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Ecs.Interfaces.System;
using gEngine.Ecs.System;
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


    private List<IInputSystem>? _inputSystems;
    private List<ISimulationSystem>? _simulationSystems;
    private List<ILateSystem>? _lateSystems;
    private List<IRenderSystem>? _renderSystems;
    
    
    // AssetManager posseduto dal GameLoop e ricevuto in Init (vedi IGame.Init).
    private AssetManager _assetManager = null!;

    // Mondo fisico (adapter Bepu), posseduto dal gioco.
    // il suo ciclo di vita non è legato alla finestra: lo creo qui e lo dispongo in Shutdown.
    private readonly IPhysicsWorld _physics = new BepuPhysicsWorld(new Vector3(0, -9.81f, 0));

    private TextureHandle _playerSprite;
    private MusicHandle _introSound;
    

    public void Init(InputHandler inputHandler, AssetManager assets)
    {
        _assetManager = assets;

        var gameActionContext = new GameActionContext();
        gameActionContext.AddToContext([new InputBinding(){KKey = KeyboardKey.W}], GameAction.MoveUp);
        gameActionContext.AddToContext([new InputBinding(){KKey = KeyboardKey.S}], GameAction.MoveDown);
        gameActionContext.AddToContext([new InputBinding(){KKey = KeyboardKey.A}], GameAction.MoveLeft);
        gameActionContext.AddToContext([new InputBinding(){KKey = KeyboardKey.D}], GameAction.MoveRight);
        gameActionContext.AddToContext([new InputBinding(){KKey = KeyboardKey.Space}], GameAction.CameraCenter);
        gameActionContext.AddToContext([new InputBinding(){MButton = MouseButton.Right}], GameAction.CameraFreeFly);
        
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
        
    }

    public void Update(float fixedDeltaTime, InputHandler  inputHandler)
    {
        if (_inputSystems == null) return;
        
        foreach (var system in _inputSystems)
            system.OnUpdate(_world, fixedDeltaTime);
        
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
        renderer.BeginFrame();
        renderer.ClearBackground(Color.White);
        
        renderer.Begin3D(_camera);

        if (_renderSystems != null)
            foreach (var system in _renderSystems)
                system.OnRender(_world, renderer, _camera, renderer.GetFrameTime());

        renderer.End3D();
        
        
        var (_, transform, _) = _world.Query<TransformComponent, VelocityComponent>().First();
        
        const int hudPadding = 20;
        const int hudBoxWidth = 220;
        const int hudBoxHeight = 40;
        var hudBoxX = hudPadding;
        var hudBoxY = renderer.GetScreenHeight() - hudBoxHeight - hudPadding;

        renderer.DrawRectangle(hudBoxX, hudBoxY, hudBoxWidth, hudBoxHeight, new Color(0, 0, 0, 150));
        renderer.DrawText($"Pos: ({transform.Position.X:F0}, {transform.Position.Y:F0})", hudBoxX + 10, hudBoxY + 10, 20, Color.White);
        renderer.DrawText("GameTime: " +  Raylib.GetTime(), 20, 20, 30, Color.Black);


        renderer.EndFrame();
        
        // audio management
        _assetManager.UpdateMusic(_introSound);
    }

    public void Shutdown()
    {
        // Le risorse asset sono scaricate dal GameLoop (che possiede l'AssetManager).
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