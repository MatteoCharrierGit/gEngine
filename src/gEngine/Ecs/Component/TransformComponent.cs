using System.Numerics;

namespace gEngine.Ecs.Component;

public struct TransformComponent
{
    [EditorConfiguration] public Vector3 Position;
    [EditorConfiguration] public Quaternion Rotation;
    [EditorConfiguration] public Vector3 Scale;
}