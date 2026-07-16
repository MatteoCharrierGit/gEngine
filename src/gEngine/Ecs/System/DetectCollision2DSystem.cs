using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Ecs.Component.Event;
using gEngine.Ecs.Interfaces.System;

namespace gEngine.Ecs.System;

public class DetectCollision2DSystem : ISimulationSystem
{
    // Solo l'insieme che decide su chi agisce. Il system tocca anche le entità con un
    // Collision2DComponentEvent (per ripulirlo), ma quell'evento se lo scrive da sé: sono
    // sempre entità con Collider2D + Transform.
    public IReadOnlyList<Type> MatchedComponents { get; } =
        [typeof(Collider2DComponent), typeof(TransformComponent)];

    public void OnCreate(World world)
    {
        
    }

    public void OnUpdate(World world, float dt)
    {

        foreach (var (e, collisionEvent) in world.Query<Collision2DComponentEvent>().ToList())
        {
            world.RemoveComponent<Collision2DComponentEvent>(e);
        }
        
        
        foreach (var (e, collider2D, transform) in world.Query<Collider2DComponent, TransformComponent>())
        {
            foreach (var (other, collider2DOther, transformOther) in world.Query<Collider2DComponent, TransformComponent>())
            {
                


                if (e == other) continue;
                    
                var xOver = Math.Abs(transform.Position.X - transformOther.Position.X) < 
                            (collider2D.HalfWidth + collider2DOther.HalfWidth);
                
                var yOver = Math.Abs(transform.Position.Y - transformOther.Position.Y) <
                            (collider2D.HalfHeight + collider2DOther.HalfHeight);

                if (xOver && yOver)
                {
                    var overlapX = (collider2D.HalfWidth + collider2DOther.HalfWidth) -
                                   Math.Abs(transform.Position.X - transformOther.Position.X);

                    var overlapY = (collider2D.HalfHeight + collider2DOther.HalfHeight) -
                                   Math.Abs(transform.Position.Y - transformOther.Position.Y);
                    
                    var axis = overlapX < overlapY ? Collision2DAxis.X : Collision2DAxis.Y;

                    var info = new Collision2DInfo
                    {
                        Other = other,
                        Axis = axis,
                        OverlapX = overlapX,
                        OverlapY = overlapY
                    };

                    if (world.TryGetComponent<Collision2DComponentEvent>(e, out var existingEvent))
                    {
                        existingEvent.Collisions.Add(info);
                    }
                    else
                    {
                        world.AddComponent(e, new Collision2DComponentEvent
                        {
                            Collisions = [info]
                        });
                    }
                }
            }
        }
    }
}

public enum Collision2DAxis
{
    X,
    Y,
}