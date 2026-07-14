using System.Numerics;
using gEngine.Ecs;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Ecs.Interfaces.System;
using gEngine.Input;
using gEngine.Rendering;

namespace Sandbox.Systems;

public class CameraFollowSystem(Camera3D camera, InputHandler inputHandler)
    : IInputSystem
{
    
    private readonly Vector3 _offsetPosition = new Vector3(0, 7, 6);
    
    public void OnCreate(World world)
    {
        
    }

    public void OnUpdate(World world, float dt)
    {
        foreach(var (e, player, transform) in world.Query<PlayerComponent, TransformComponent>())
        {
            if (inputHandler.IsActionDown(GameAction.CameraCenter))
            {
                camera.Position = transform.Position + _offsetPosition;
                camera.Target = transform.Position;
            }
        }
    }
}