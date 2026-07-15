using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Ecs.Interfaces.System;
using gEngine.Physics;

namespace gEngine.Ecs.System;

/// <summary>
/// Ponte ECS ⇄ mondo fisico. Ogni update: crea i corpi mancanti, avanza la simulazione
/// di un passo fisso, poi riscrive nel <see cref="TransformComponent"/> la posa dei corpi
/// dinamici. Gira come <see cref="ISimulationSystem"/> nel fixed-step del GameLoop.
/// </summary>
public class PhysicsSystem : ISimulationSystem
{
    private readonly IPhysicsWorld _physics;

    // Materializza le entità da registrare PRIMA di modificare il world (non si aggiungono
    // componenti mentre si itera la stessa query).
    private readonly List<(Entity Entity, TransformComponent Transform, RigidBodyComponent Body)> _pending = new();

    public PhysicsSystem(IPhysicsWorld physics)
    {
        _physics = physics;
    }

    public void OnCreate(World world)
    {
    }

    public void OnUpdate(World world, float dt)
    {
        // 1) Registra i corpi per le entità con RigidBody ma ancora senza corpo fisico.
        _pending.Clear();
        foreach (var (entity, transform, body) in world.Query<TransformComponent, RigidBodyComponent>())
        {
            if (!world.HasComponent<PhysicsBodyComponent>(entity))
                _pending.Add((entity, transform, body));
        }

        foreach (var (entity, transform, body) in _pending)
        {
            var id = body.Shape == ColliderShape.Sphere
                ? _physics.AddSphere(transform.Position, transform.Rotation, body.Size.X, body.Mass, body.IsStatic)
                : _physics.AddBox(transform.Position, transform.Rotation, body.Size, body.Mass, body.IsStatic);

            world.AddComponent(entity, new PhysicsBodyComponent { Body = id });
        }

        // 2) Avanza la simulazione di un passo.
        _physics.Step(dt);

        // 3) Sync fisica → Transform (solo i dinamici restituiscono una posa).
        foreach (var (entity, _, physicsBody) in world.Query<TransformComponent, PhysicsBodyComponent>())
        {
            if (!_physics.TryGetPose(physicsBody.Body, out var position, out var orientation))
                continue;

            var transform = world.GetComponent<TransformComponent>(entity);
            transform.Position = position;
            transform.Rotation = orientation;
            world.AddComponent(entity, transform); // write-back (TransformComponent è struct)
        }
    }
}
