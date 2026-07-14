using System.Numerics;

namespace gEngine.Ecs.Component;

public struct TransformComponent
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;
}