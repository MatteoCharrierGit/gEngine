using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Ecs.Interfaces.System;

namespace gEngine.Ecs.System;

public class DetectCollision2DSystem : ISimulationSystem
{
    public void OnCreate(World world)
    {
        
    }

    public void OnUpdate(World world, float dt)
    {

        foreach (var (e, collisionEvent) in world.Query<Collision2DComponentEvent>().ToList())
        {
            world.RemoveComponent<Collision2DComponentEvent>(e);
        }
        
        
        foreach (var (e, collider2D, position) in world.Query<Collider2DComponent, PositionComponent>())
        {
            foreach (var (other, collider2DOther, positionOther) in world.Query<Collider2DComponent, PositionComponent>())
            {
                


                if (e == other) continue;
                    
                var xOver = Math.Abs(position.X - positionOther.X) < 
                            (collider2D.HalfWidth + collider2DOther.HalfWidth);
                
                var yOver = Math.Abs(position.Y - positionOther.Y) <
                            (collider2D.HalfHeight + collider2DOther.HalfHeight);

                if (xOver && yOver)
                {
                    var overlapX = (collider2D.HalfWidth + collider2DOther.HalfWidth) -
                                   Math.Abs(position.X - positionOther.X);

                    var overlapY = (collider2D.HalfHeight + collider2DOther.HalfHeight) -
                                   Math.Abs(position.Y - positionOther.Y);
                    
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