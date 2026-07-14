using System.Numerics;
using gEngine.Input;
using Raylib_cs;

namespace gEngine.Rendering.Editor;

public class FreeFlyCamera3DController(InputHandler input, Camera3D camera)
{
    
    private readonly float _sensitivity = 0.003f;
    private float _speed = 5;
    
    
    public float Yaw { get; set; } = 0f;
    public float Pitch { get; set; } = 0f;
    public Vector3 Right { get; set; } = Vector3.Zero;

    public void Init()
    {
        Pitch = (float)Math.Asin(camera.GetCameraAxes().Forward.Y);
        Yaw = (float)Math.Atan2(camera.GetCameraAxes().Forward.Z, camera.GetCameraAxes().Forward.X);
    }
    
    
    public void OnUpdate(float dt)
    {
        
        if (input.IsActionPressed(GameAction.CameraFreeFly))
            Raylib.DisableCursor();
            
        if (input.IsActionReleased(GameAction.CameraFreeFly))
            Raylib.EnableCursor();

        if (input.IsActionDown(GameAction.CameraFreeFly))
        {
            Yaw += input.GetMouseDelta().X * _sensitivity;
            Pitch -= input.GetMouseDelta().Y * _sensitivity;
            Pitch = Math.Clamp(Pitch, -89f, 89f);
            
            var newFw = Vector3.Normalize(new Vector3(
                    MathF.Cos(Yaw) *  MathF.Cos(Pitch),
                    MathF.Sin(Pitch),
                    MathF.Sin(Yaw) *  MathF.Cos(Pitch)
                ));
            
            Right = Vector3.Normalize(Vector3.Cross(camera.Up, newFw));
            
            if (input.IsActionDown(GameAction.MoveUp))
                camera.Position += newFw * _speed * dt;

            if (input.IsActionDown(GameAction.MoveDown))
                camera.Position -= newFw * _speed * dt;

            if (input.IsActionDown(GameAction.MoveLeft))
                camera.Position += Right * _speed * dt;

            if (input.IsActionDown(GameAction.MoveRight))
                camera.Position -= Right * _speed * dt;
            
            
            camera.Target = camera.Position + newFw;
            
            
        }
        
    }
}