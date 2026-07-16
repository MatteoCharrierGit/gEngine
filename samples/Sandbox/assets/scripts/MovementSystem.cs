using System.Numerics;
using gEngine.Ecs;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Ecs.Interfaces.System;
using gEngine.Scripting;
using Sandbox.Components;

namespace Sandbox.Systems;

[GameSystem(Order = 10)]
public class MovementSystem : ISimulationSystem
{
    public IReadOnlyList<Type> MatchedComponents { get; } =
        [typeof(TransformComponent), typeof(VelocityComponent)];

    public void OnCreate(World world)
    {

    }

    public void OnUpdate(World world, float dt)
    {
        foreach (var (entity, transform, velocity) in world.Query<TransformComponent, VelocityComponent>())
        {
            // Muta solo la copia locale e riscrivi l'intero struct: così Rotation
            // e Scale si conservano. Costruire un nuovo TransformComponent con la
            // sola Position le azzererebbe (Scale=0 → mesh invisibile).
            var updated = transform;
            updated.Position += velocity.Velocity * dt;
            world.AddComponent(entity, updated);
        }
    }
}