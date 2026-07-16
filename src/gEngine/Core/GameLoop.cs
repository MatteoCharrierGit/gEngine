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

    // Le Resources le possiede il GameLoop, non il gioco: chi crea l'infrastruttura è chi
    // la dichiara. Il gioco le riceve in Init e ci aggiunge le sue (vedi IGame.Init).
    private readonly Resources _resources = new Resources();


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

        // ⚠️ ORDINE: la registrazione sta QUI e non prima di InitWindow perché renderer e
        // AssetManager tengono risorse GPU e non possono esistere a finestra chiusa. Ed è
        // proprio questo il motivo per cui l'IRenderer non poteva stare nella vecchia firma
        // di Init: ora ci arriva dentro il contenitore, come tutto il resto.
        // Tipi espliciti: i campi sono nullable (esistono solo dopo InitWindow) e
        // l'inferenza registrerebbe sotto `AssetManager?`/`IRenderer?`. L'IRenderer va sotto
        // la porta, non sotto RayLibRenderer — vedi Resources.Add.
        _resources.Add<InputHandler>(_inputHandler);
        _resources.Add<AssetManager>(_assetManager);
        _resources.Add<IRenderer>(_renderer);

        Game.Init(_resources);

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