using System.Numerics;
using gEngine.Ecs;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Ecs.Interfaces.System;
using gEngine.Scripting;
using Sandbox.Components;
using gEngine.Input;

namespace  Sandbox.Systems;

[GameSystem(Order = 20)]
public class PlayerInputSystem(InputHandler inputHandler)
    : IInputSystem
{

    private const float Velocity = 2;

    public IReadOnlyList<Type> MatchedComponents { get; } =
        [typeof(PlayerComponent), typeof(VelocityComponent)];


    public void OnCreate(World world)
    {
        
    }

    public void OnUpdate(World world, float dt)
    {
        foreach (var (e, player, velocity) in world.Query<PlayerComponent, VelocityComponent>())
        {
            var velX = 0f;
            var velZ = 0f;
            
            if (inputHandler.IsActionDown(GameAction.MoveUp))
            {
                velZ -= Velocity;
            }

            if (inputHandler.IsActionDown(GameAction.MoveDown))
            {
                velZ += Velocity;
            }

            if (inputHandler.IsActionDown(GameAction.MoveLeft))
            {
                velX -= Velocity;
            }
            
            if (inputHandler.IsActionDown(GameAction.MoveRight))
            {
                velX += Velocity;
            }
            world.AddComponent(e, new VelocityComponent()
            {
                Velocity = new Vector3(velX, 0f, velZ),
            });
        }
    }
}
