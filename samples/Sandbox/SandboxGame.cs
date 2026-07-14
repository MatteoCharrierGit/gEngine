using gEngine.Assets;
using gEngine.Core;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Ecs.Interfaces.System;
using gEngine.Input;
using gEngine.Rendering.Editor;
using Raylib_cs;
using Sandbox.Systems;
using Camera3D = gEngine.Rendering.Camera3D;
using Path = System.IO.Path;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using World = gEngine.Ecs.Base.World;


namespace Sandbox;

public class SandboxGame() : IGame
{
    private readonly World _world = new World();
    
    private Camera3D _camera =  new Camera3D()
    {
        Position = new Vector3(0, 7, 6),
        Target = new Vector3(0, 0, 0),
        Up = Vector3.UnitY,
        FovY = 60f,
        Projection = CameraProjection.Perspective,  
    };
    
    private FreeFlyCamera3DController _freeFlyCamera3DController;
    
    
    private Entity PlayerEntity { get; set; }
    
    private List<IInputSystem>? _inputSystems;
    private List<ISimulationSystem>? _simulationSystems;
    private List<ILateSystem>? _lateSystems;
    
    
    private readonly AssetManager _assetManager = new AssetManager(AppContext.BaseDirectory,  "assets");
    
    private Texture2D _playerSprite;
    private Music _introSound;
    

    private const int FloorX = 0;
    private const int FloorY = 900;
    private const int FloorWidth = 1920;
    private const int FloorHeight = 40;

    private static readonly (Vector3 Position, float Width, float Depth, float Height, Color Color)[] Buildings =
    [
        (new Vector3(6, 0, 4), 2f, 2f, 10f, Color.DarkGray),
        (new Vector3(-5, 0, 6), 3f, 3f, 16f, Color.Gray),
        (new Vector3(-8, 0, -3), 2f, 2f, 6f, Color.DarkGray),
        (new Vector3(9, 0, -6), 2.5f, 2.5f, 20f, Color.Gray),
        (new Vector3(3, 0, -9), 2f, 2f, 12f, Color.DarkGray),
        (new Vector3(-3, 0, -12), 3f, 3f, 8f, Color.Gray),
    ];

    
    public void Init(InputHandler inputHandler)
    {

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
        
        PlayerEntity = _world.CreateEntity();
        
        _world.AddComponent(PlayerEntity, new TransformComponent()
        {
            Position = Vector3.Zero,
        });
        _world.AddComponent(PlayerEntity, new VelocityComponent()
        {
            Velocity = Vector3.Zero,
        });
        _world.AddComponent(PlayerEntity, new PlayerComponent());

        AddSystem(new MovementSystem());
        // AddSystem(new PlayerInputSystem(inputHandler));
        AddSystem(new CameraFollowSystem(_camera, inputHandler));
        
        
        _playerSprite = _assetManager.LoadTexture2D("sprites/shan_shan_idle0.png");
        _introSound = _assetManager.LoadMusicStream(Path.Combine("audio", "Before_the_Light_Fades.mp3"));
        
        Raylib.PlayMusicStream(_introSound);
        
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

    public void Draw()
    {
        
        var (_, transform, _) = _world.Query<TransformComponent, VelocityComponent>().First();
        
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.White);
        Raylib.DrawText("GameTime: " +  Raylib.GetTime(), 20, 20, 30, Color.Black);

        const int hudPadding = 20;
        const int hudBoxWidth = 220;
        const int hudBoxHeight = 40;
        var hudBoxX = hudPadding;
        var hudBoxY = Raylib.GetScreenHeight() - hudBoxHeight - hudPadding;

        Raylib.DrawRectangle(hudBoxX, hudBoxY, hudBoxWidth, hudBoxHeight, new Color(0, 0, 0, 150));
        Raylib.DrawText($"Pos: ({transform.Position.X:F0}, {transform.Position.Y:F0})", hudBoxX + 10, hudBoxY + 10, 20, Color.White);
        

        Raylib.BeginMode3D(_camera.ToRaylibCamera3D());

        Raylib.DrawPlane(new Vector3(0, 0, 0), new Vector2(40, 40), Color.LightGray);
        Raylib.DrawGrid(20, 1.0f);

        foreach (var building in Buildings)
        {
            var center = new Vector3(building.Position.X, building.Height / 2f, building.Position.Z);
            Raylib.DrawCube(center, building.Width, building.Height, building.Depth, building.Color);
            Raylib.DrawCubeWires(center, building.Width, building.Height, building.Depth, Color.Black);
        }

        Raylib.DrawCube(transform.Position, 1, 1, 1, Color.Red);

        Raylib.EndMode3D();
        
        
        Raylib.EndDrawing();
        
        // audio management
        Raylib.UpdateMusicStream(_introSound);
    }

    public void Shutdown()
    {
        _assetManager.UnloadAll();
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
}