using gEngine.Core;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Input;
using Raylib_cs;

namespace gEngine.Sample;

/// <summary>
/// Esempio minimale d'uso di gEngine: una finestra, un'entità mossa con WASD
/// disegnata come un rettangolo. Nessun asset esterno, così il progetto
/// compila e gira appena clonato il repository.
/// </summary>
public class SampleGame : IGame
{
    private readonly World _world = new();
    private Entity _player;

    private const float Speed = 300f; // pixel al secondo

    public void Init(InputHandler inputHandler)
    {
        var context = new GameActionContext();
        context.AddToContext([KeyboardKey.W], GameAction.MoveUp);
        context.AddToContext([KeyboardKey.S], GameAction.MoveDown);
        context.AddToContext([KeyboardKey.A], GameAction.MoveLeft);
        context.AddToContext([KeyboardKey.D], GameAction.MoveRight);
        inputHandler.SetActiveContext(context);

        _player = _world.CreateEntity();
        _world.AddComponent(_player, new PositionComponent { X = 640, Y = 360 });
    }

    public void Update(float fixedDeltaTime, InputHandler inputHandler)
    {
        var position = _world.GetComponent<PositionComponent>(_player);

        if (Held(inputHandler, GameAction.MoveUp))    position.Y -= Speed * fixedDeltaTime;
        if (Held(inputHandler, GameAction.MoveDown))  position.Y += Speed * fixedDeltaTime;
        if (Held(inputHandler, GameAction.MoveLeft))  position.X -= Speed * fixedDeltaTime;
        if (Held(inputHandler, GameAction.MoveRight)) position.X += Speed * fixedDeltaTime;

        // PositionComponent è una struct: GetComponent restituisce una copia,
        // quindi riscrivo il valore aggiornato nello storage.
        _world.AddComponent(_player, position);
    }

    public void Draw()
    {
        var position = _world.GetComponent<PositionComponent>(_player);

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.RayWhite);
        Raylib.DrawText("gEngine sample - muoviti con WASD", 20, 20, 20, Color.DarkGray);
        Raylib.DrawRectangle((int)position.X - 25, (int)position.Y - 25, 50, 50, Color.Maroon);
        Raylib.EndDrawing();
    }

    public void Shutdown()
    {
    }

    // Il primo frame in cui premi un tasto lo stato è "Pressed", poi "Down":
    // per un movimento continuo consideriamo tenuto entrambi i casi.
    private static bool Held(InputHandler input, GameAction action)
        => input.IsActionDown(action) || input.IsActionPressed(action);
}
