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

    // Piani di clip near/far. Non esistono in Raylib_cs.Camera3D (raylib usa costanti
    // globali interne): li teniamo qui per costruire la matrice di proiezione lato engine
    // (es. frustum culling). I default combaciano con quelli di raylib, così il frustum
    // matematico coincide con ciò che raylib clippa davvero.
    public float Near = 0.01f;
    public float Far = 1000f;

    // View matrix (right-handed, come raylib). Row-vector: un punto va trasformato con
    // v * View.
    public Matrix4x4 GetViewMatrix()
    {
        return Matrix4x4.CreateLookAt(Position, Target, Up);
    }

    // Projection matrix prospettica. FovY è il campo visivo VERTICALE in gradi (come
    // raylib); CreatePerspectiveFieldOfView lo vuole in radianti.
    public Matrix4x4 GetProjectionMatrix(float aspectRatio)
    {
        return Matrix4x4.CreatePerspectiveFieldOfView(
            float.DegreesToRadians(FovY), aspectRatio, Near, Far);
    }

    // View-projection combinata. Ordine row-vector: clip = v * View * Projection,
    // quindi ViewProjection = View * Projection (view applicata per prima).
    public Matrix4x4 GetViewProjection(float aspectRatio)
    {
        return GetViewMatrix() * GetProjectionMatrix(aspectRatio);
    }

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