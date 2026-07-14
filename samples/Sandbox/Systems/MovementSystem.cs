using System.Numerics;
using gEngine.Ecs;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Ecs.Interfaces.System;

namespace Sandbox.Systems;

public class MovementSystem : ISimulationSystem
{
    public void OnCreate(World world)
    {

    }

    public void OnUpdate(World world, float dt)
    {
        foreach (var (entity, transform, velocity) in world.Query<TransformComponent, VelocityComponent>())
        {
            world.AddComponent(entity, new TransformComponent()
            {
                Position = new Vector3(transform.Position.X + velocity.Velocity.X * dt, transform.Position.Y + velocity.Velocity.Y * dt, transform.Position.Z  + velocity.Velocity.Z * dt),
            });
        }
    }
}