using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Ecs.Interfaces.System;
using nkast.Aether.Physics2D.Common;
using AetherWorld = nkast.Aether.Physics2D.Dynamics.World;

namespace gEngine.Ecs.System;

public class PhysicsSystem() : ISimulationSystem
{

    public AetherWorld? AetherWorld { get; private set; }
    public float PixelPerMeter { get; private set; } = 100f;
    
    private const float Gravity = 9.81f;
    
    public void OnCreate(World world)
    {
        AetherWorld = new AetherWorld
        {
            Gravity = new Vector2(0, Gravity)
        };
    }

    public void OnUpdate(World world, float dt)
    {
        AetherWorld!.Step(dt);

        foreach (var (e, pb, pos) in world.Query<PhysicsBodyComponent, PositionComponent>())
        {
            world.AddComponent(e, new PositionComponent()
            {
                X = pb.Body.Position.X * PixelPerMeter - pb.HalfWidth,
                Y = pb.Body.Position.Y * PixelPerMeter - pb.HalfHeight
            });

        }
    }
}