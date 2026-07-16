using System.Numerics;
using gEngine.Input;
using Raylib_cs;

namespace gEngine.Rendering.Editor;

public class FreeFlyCamera3DController(InputHandler input, Camera3D camera)
{

    private readonly float _sensitivity = 0.003f;
    private float _speed = 5;

    // Yaw e Pitch sono in RADIANTI (Init li ricava da Asin/Atan2): il limite va scritto
    // nella stessa unità. Prima il clamp era a ±89 "gradi" applicati ai radianti, cioè
    // ±5000°: non limitava niente, e passando lo zenit la camera si ribaltava — Up resta
    // fisso a +Y, quindi oltre il polo l'immagine si capovolge.
    private static readonly float MaxPitch = float.DegreesToRadians(89f);

    // Il volo, una volta iniziato, va avanti finché non si rilascia il tasto — anche se
    // il puntatore esce dalla vista. Durante il volo il cursore è bloccato al centro
    // della finestra: chiedere ogni frame "sei ancora sopra il viewport?" smetterebbe di
    // essere vero proprio mentre lo si sta usando.
    private bool _capturing;

    public float Yaw { get; set; } = 0f;
    public float Pitch { get; set; } = 0f;
    public Vector3 Right { get; set; } = Vector3.Zero;

    public void Init()
    {
        Pitch = (float)Math.Asin(camera.GetCameraAxes().Forward.Y);
        Yaw = (float)Math.Atan2(camera.GetCameraAxes().Forward.Z, camera.GetCameraAxes().Forward.X);
    }


    /// <summary>
    /// Interrompe il volo e ridà il cursore.
    ///
    /// Serve perché la fine del volo è un <b>evento</b> (il rilascio del tasto) e chi non
    /// gira in quel frame se lo perde per sempre: chiudendo l'editor con F1 mentre si tiene
    /// premuto il destro, il rilascio cade quando questo controller non viene più
    /// aggiornato. Senza, il cursore resta sparito e alla riapertura la camera insegue il
    /// mouse da sola, senza che nessuno stia premendo niente.
    /// </summary>
    public void Cancel()
    {
        if (!_capturing)
            return;

        _capturing = false;
        Raylib.EnableCursor();
    }

    /// <param name="canStartCapture">
    /// Se il volo può <b>iniziare</b> adesso — tipicamente "il puntatore è sopra la vista
    /// Scena". Gate solo sull'inizio, non sulla continuazione: vedi <see cref="_capturing"/>.
    /// </param>
    public void OnUpdate(float dt, bool canStartCapture = true)
    {
        if (!_capturing && canStartCapture && input.IsActionPressed(GameAction.CameraFreeFly))
        {
            _capturing = true;
            Raylib.DisableCursor();
        }

        if (_capturing && input.IsActionReleased(GameAction.CameraFreeFly))
        {
            _capturing = false;
            Raylib.EnableCursor();
        }

        if (!_capturing)
            return;

        Yaw += input.GetMouseDelta().X * _sensitivity;
        Pitch -= input.GetMouseDelta().Y * _sensitivity;
        Pitch = Math.Clamp(Pitch, -MaxPitch, MaxPitch);

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
