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

    // Il logger nasce con un sink sullo stdout. Gliene si aggiungono altri dall'esterno
    // (la console dell'editor) senza che nessun chiamante di Info/Warn/Error cambi.
    private readonly Logger _logger = new Logger();
    private IRenderer? _renderer = null;
    private AssetManager? _assetManager = null;
    private InputHandler _inputHandler = new InputHandler();

    // Le Resources le possiede il GameLoop, non il gioco: chi crea l'infrastruttura è chi
    // la dichiara. Il gioco le riceve in Init e ci aggiunge le sue (vedi IGame.Init).
    private readonly Resources _resources = new Resources();


    public void Run()
    {
        // ⚠️ ORDINE: il logger si registra PRIMA di InitWindow, al contrario di tutto il resto.
        // Non è una svista simmetrica all'ordine imposto più sotto: è il motivo opposto. Gli
        // altri servizi non POSSONO esistere a finestra chiusa (tengono risorse GPU); il logger
        // non ha quel vincolo, e registrarlo insieme a loro renderebbe muto proprio l'avvio —
        // cioè il pezzo di vita del programma in cui è più probabile che qualcosa vada storto
        // e in cui, non essendoci ancora una finestra, il log è l'unica cosa che parla.
        _logger.AddSink(new ConsoleLogSink());
        _resources.Add<ILogger>(_logger);

        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint);
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);


        Raylib.InitWindow(WindowWidth, WindowHeight, Title);
        Raylib.InitAudioDevice();
        Raylib.SetTargetFPS(60);

        _logger.Info(LogCategories.Window, $"Finestra {WindowWidth}x{WindowHeight} - {Title}");

        // GameLoop possiede i due adapter raylib e li collega: il renderer risolve i
        // ModelHandle attraverso lo stesso backend che carica gli asset.
        var assetBackend = new RayLibAssetBackend();
        _assetManager = new AssetManager(ContentRoot.Path, "assets", assetBackend, _logger);
        _renderer = new RayLibRenderer(assetBackend, _logger);

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
        _logger.Info(LogCategories.Engine, "Gioco inizializzato");

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

        _logger.Info(LogCategories.Engine, "Chiusura");

        Game.Shutdown();
        _assetManager?.UnloadAll();   // scarica le risorse GPU prima di chiudere la finestra
        _renderer?.Shutdown();
        Raylib.CloseWindow();
    }
}