using System.Numerics;
using Raylib_cs;

namespace gEngine.Rendering;

public class Camera3D
{
    public Vector3 Position = new();
    public Vector3 Target = new();
    public Vector3 Up = Vector3.UnitY;
    public float FovY = 60f;
    public CameraProjection Projection = CameraProjection.Perspective;

    public CameraAxes GetCameraAxes()
    {
        var fw = Vector3.Normalize(Vector3.Subtract(Target, Position));
        var r = Vector3.Normalize(Vector3.Cross(Up, fw));
        var ur = Vector3.Cross(fw, r);

        return new CameraAxes()
        {
            Forward = fw,
            Right = r,
            UpReal = ur
        };
    }

    public Raylib_cs.Camera3D ToRaylibCamera3D()
    {
        var cam = new Raylib_cs.Camera3D()
        {
            Position = Position,
            Target = Target,
            Up = Up,
            FovY = FovY,
            Projection = Projection
        };
        
        return cam;
    }
}