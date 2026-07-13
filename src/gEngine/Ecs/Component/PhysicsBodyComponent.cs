using nkast.Aether.Physics2D.Dynamics;

namespace gEngine.Ecs.Component;

public struct PhysicsBodyComponent
{
    public Body Body;
    public float HalfWidth;
    public float HalfHeight;
}