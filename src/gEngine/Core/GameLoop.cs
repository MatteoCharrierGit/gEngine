using gEngine.Input;
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
    
    
    private InputHandler _inputHandler = new InputHandler();


    public void Run()
    {
        Raylib.InitWindow(WindowWidth, WindowHeight, Title);
        Raylib.InitAudioDevice();
        Raylib.SetTargetFPS(60);
        
        Game.Init(_inputHandler);

        while (!Raylib.WindowShouldClose())
        {
            _gameAccumulator += Raylib.GetFrameTime();
            _inputHandler.Update();

            while (_gameAccumulator >= FixedDeltaTime)
            {
                Game.Update(FixedDeltaTime, _inputHandler);
                _gameAccumulator -= FixedDeltaTime;
            }
            
            Game.Draw();
        }
        
        Game.Shutdown();
        Raylib.CloseWindow();
    }
}