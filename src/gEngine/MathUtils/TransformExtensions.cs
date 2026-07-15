using System.Numerics;
using gEngine.Ecs.Component;

namespace gEngine.MathUtils;

public static class TransformExtensions
{
    public static Matrix4x4 GetLocalMatrix(this TransformComponent component)
    {
        var m = Matrix4x4.CreateScale(component.Scale);
        m = m * Matrix4x4.CreateFromQuaternion(component.Rotation);
        m = m * Matrix4x4.CreateTranslation(component.Position);
        
        return m;
    }

    public static Vector3 GetForward(this TransformComponent component)
    {
        return Vector3.Transform(Vector3.UnitZ, component.Rotation);
    }

    public static Vector3 GetRight(this TransformComponent component)
    {
        return Vector3.Transform(Vector3.UnitX, component.Rotation);
    }

    public static Vector3 GetUp(this TransformComponent component)
    {
        return Vector3.Transform(Vector3.UnitY, component.Rotation);
    }
}