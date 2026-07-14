using System.Numerics;
using gEngine.Core;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Input;
using Raylib_cs;

namespace gEngine.Sample;

public class SampleGame : IGame
{
    private const float Speed = 300f;
    private const float RotationSpeed = MathF.PI; // 180°/s

    private const float Rad2Deg = 180f / MathF.PI;

    private readonly World _world = new();
    private Entity _player;

    public void Init(InputHandler inputHandler)
    {
        var context = new GameActionContext();

        context.AddToContext([new InputBinding(){KKey = KeyboardKey.W}], GameAction.MoveUp);
        context.AddToContext([new InputBinding(){KKey = KeyboardKey.S}], GameAction.MoveDown);
        context.AddToContext([new InputBinding(){KKey = KeyboardKey.A}], GameAction.MoveLeft);
        context.AddToContext([new InputBinding(){KKey = KeyboardKey.D}], GameAction.MoveRight);

        inputHandler.SetActiveContext(context);

        _player = _world.CreateEntity();

        _world.AddComponent(_player, new TransformComponent
        {
            Position = new Vector3(640, 360, 0),
            Rotation = Quaternion.Identity,
            Scale = Vector3.One
        });
    }

    public void Update(float fixedDeltaTime, InputHandler inputHandler)
    {
        var transform = _world.GetComponent<TransformComponent>(_player);

        if (Held(inputHandler, GameAction.MoveUp))
            transform.Position.Y -= Speed * fixedDeltaTime;

        if (Held(inputHandler, GameAction.MoveDown))
            transform.Position.Y += Speed * fixedDeltaTime;

        if (Held(inputHandler, GameAction.MoveLeft))
            transform.Position.X -= Speed * fixedDeltaTime;

        if (Held(inputHandler, GameAction.MoveRight))
            transform.Position.X += Speed * fixedDeltaTime;

        // Rotazione continua attorno a Z
        var delta = Quaternion.CreateFromAxisAngle(
            Vector3.UnitZ,
            RotationSpeed * fixedDeltaTime);

        transform.Rotation = Quaternion.Normalize(transform.Rotation * delta);

        _world.AddComponent(_player, transform);
    }

    public void Draw()
    {
        var transform = _world.GetComponent<TransformComponent>(_player);

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.RayWhite);

        Raylib.DrawText(
            "gEngine sample - muoviti con WASD",
            20,
            20,
            20,
            Color.DarkGray);

        var rect = new Rectangle(
            transform.Position.X,
            transform.Position.Y,
            50,
            50);

        // Estrae l'angolo Z dal quaternion.
        // Valido perché stiamo ruotando solo attorno all'asse Z.
        float rotationDeg =
            2f * MathF.Atan2(transform.Rotation.Z, transform.Rotation.W) * Rad2Deg;

        Raylib.DrawRectanglePro(
            rect,
            new Vector2(rect.Width / 2f, rect.Height / 2f),
            rotationDeg,
            Color.Red);

        Raylib.EndDrawing();
    }

    public void Shutdown()
    {
    }

    private static bool Held(InputHandler input, GameAction action)
        => input.IsActionDown(action) || input.IsActionPressed(action);
}