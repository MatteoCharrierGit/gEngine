using System.Numerics;

namespace gEngine.Rendering;

public class Camera2D
{
    public Vector2 TargetPosition =  new();
    public Vector2 Offset = new();
    
    public float Zoom = 0f;
    public float Rotation = 0f;

    public Raylib_cs.Camera2D ToRaylibCamera2D()
    {
        var cam = new Raylib_cs.Camera2D()
        {
            Target = TargetPosition,
            Offset = Offset,
            Zoom = Zoom,
            Rotation = Rotation
        };
        
        return cam;
    }
}