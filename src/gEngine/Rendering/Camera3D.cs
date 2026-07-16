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

    /// <summary>
    /// La semiretta che parte dall'occhio e passa per un pixel della vista. È il ponte fra
    /// un clic e il mondo 3D (picking).
    ///
    /// Le coordinate sono <b>del viewport</b>, non della finestra: origine in alto a
    /// sinistra dell'immagine, non dello schermo. Chi chiama deve aver già tolto la
    /// posizione del pannello — qui non si sa nemmeno che esista una finestra.
    /// </summary>
    public MathUtils.Ray GetRay(Vector2 viewportPosition, Vector2 viewportSize)
    {
        // NDC: [0..w] → [-1..+1] in X, e [0..h] → [+1..-1] in Y perché lo schermo conta le
        // righe dall'alto e il clip space dal basso.
        var ndc = new Vector2(
            2f * viewportPosition.X / viewportSize.X - 1f,
            1f - 2f * viewportPosition.Y / viewportSize.Y);

        var viewProjection = GetViewProjection(viewportSize.X / viewportSize.Y);

        if (!Matrix4x4.Invert(viewProjection, out var inverse))
            return new MathUtils.Ray(Position, GetCameraAxes().Forward);

        // z=0 è il near plane e z=1 il far: convenzione Direct3D, quella che usa
        // CreatePerspectiveFieldOfView — la stessa già assunta da Frustum per il piano near.
        var near = Unproject(new Vector3(ndc, 0f), inverse);
        var far = Unproject(new Vector3(ndc, 1f), inverse);

        return new MathUtils.Ray(near, Vector3.Normalize(far - near));
    }

    /// <summary>
    /// L'inverso esatto di <see cref="GetRay"/>: da un punto del mondo al pixel della
    /// vista in cui si vede. Serve a disegnare in 2D sopra la scena (le maniglie dei
    /// gizmi) senza passare da una pipeline 3D.
    /// </summary>
    /// <returns>
    /// False se il punto è <b>dietro</b> l'occhio. Non è un dettaglio: con w &lt; 0 la
    /// divisione prospettica restituisce comunque un pixel dall'aria innocente, ma
    /// ribaltato dalla parte opposta dello schermo. Chi disegna deve saltare il punto,
    /// non fidarsi del numero.
    /// </returns>
    public bool WorldToViewport(Vector3 worldPosition, Vector2 viewportSize, out Vector2 viewportPosition)
    {
        return WorldToViewport(
            worldPosition,
            GetViewProjection(viewportSize.X / viewportSize.Y),
            viewportSize,
            out viewportPosition);
    }

    /// <summary>
    /// Come sopra, con la view-projection già pronta: chi proietta molti punti dello
    /// stesso frame (un cerchio del gizmo di rotazione) la costruisce una volta sola.
    /// </summary>
    public static bool WorldToViewport(
        Vector3 worldPosition, Matrix4x4 viewProjection, Vector2 viewportSize, out Vector2 viewportPosition)
    {
        var clip = Vector4.Transform(new Vector4(worldPosition, 1f), viewProjection);

        if (clip.W <= 1e-5f)
        {
            viewportPosition = default;
            return false;
        }

        var ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;

        viewportPosition = new Vector2(
            (ndc.X + 1f) * 0.5f * viewportSize.X,
            (1f - ndc.Y) * 0.5f * viewportSize.Y);

        return true;
    }

    private static Vector3 Unproject(Vector3 clipPosition, Matrix4x4 inverseViewProjection)
    {
        // Row-vector: clip = world * VP, quindi world = clip * VP⁻¹ — ed è esattamente il
        // prodotto riga × matrice che fa Vector4.Transform in System.Numerics.
        var world = Vector4.Transform(new Vector4(clipPosition, 1f), inverseViewProjection);

        // La proiezione prospettica lascia w ≠ 1: la divisione è ciò che rimette il punto
        // nello spazio mondo (è l'inverso della divisione prospettica della pipeline).
        return new Vector3(world.X, world.Y, world.Z) / world.W;
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