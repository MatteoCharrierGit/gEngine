using gEngine.Assets;
using gEngine.Input;
using gEngine.Log;
using gEngine.Rendering;
using Raylib_cs;

namespace gEngine.Core;

public class GameLoop(int windowWidth, int windowHeight, string title, IGame game)
{
    private int WindowWidth { get; } = windowWidth;
    private int WindowHeight { get; } = windowHeight;
    private string Title { get; } = title;
    private IGame Game { get; } = game;

    private float _gameAccumulator = 0;
    private const float FixedDeltaTime = 1f / 60;

    private readonly ILogger _logger = new ConsoleLogger();
    private IRenderer? _renderer = null;
    private AssetManager? _assetManager = null;
    private InputHandler _inputHandler = new InputHandler();


    public void Run()
    {
        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint);
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);


        Raylib.InitWindow(WindowWidth, WindowHeight, Title);
        Raylib.InitAudioDevice();
        Raylib.SetTargetFPS(60);

        // GameLoop possiede i due adapter raylib e li collega: il renderer risolve i
        // ModelHandle attraverso lo stesso backend che carica gli asset.
        var assetBackend = new RayLibAssetBackend();
        _assetManager = new AssetManager(AppContext.BaseDirectory, "assets", assetBackend);
        _renderer = new RayLibRenderer(assetBackend);

        Game.Init(_inputHandler, _assetManager);

        while (!Raylib.WindowShouldClose())
        {
            _gameAccumulator += Raylib.GetFrameTime();
            _inputHandler.Update();

            while (_gameAccumulator >= FixedDeltaTime)
            {
                Game.Update(FixedDeltaTime, _inputHandler);
                _gameAccumulator -= FixedDeltaTime;
            }

            Game.Draw(_renderer);
        }

        Game.Shutdown();
        _assetManager?.UnloadAll();   // scarica le risorse GPU prima di chiudere la finestra
        _renderer?.Shutdown();
        Raylib.CloseWindow();
    }
}