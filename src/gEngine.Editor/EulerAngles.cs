using System.Numerics;

namespace gEngine.Editor;

/// <summary>
/// Conversione quaternione ↔ angoli di Eulero in gradi, <b>solo per l'interfaccia</b>.
/// Nessun dato dell'engine è mai in Eulero: il <c>TransformComponent</c> resta un
/// quaternione: qui si converte avanti e indietro attorno al widget, perché nessuno sa
/// leggere `(x:0.38, y:0, z:0, w:0.92)` ma tutti sanno leggere `45°`.
///
/// Convenzione: <b>yaw(Y) → pitch(X) → roll(Z)</b>, la stessa di
/// <see cref="Quaternion.CreateFromYawPitchRoll"/>, così l'andata e il ritorno usano lo
/// stesso ordine. Round-trip verificato numericamente su 200k rotazioni casuali con
/// |pitch| ≤ 89°: errore max 1-|dot| ≈ 3e-7.
///
/// ⚠️ Limite noto: a pitch ±90° (gimbal lock) la scomposizione in Eulero è ambigua —
/// yaw e roll degenerano sullo stesso asse. Lì i numeri mostrati possono saltare mentre
/// trascini, anche se la rotazione risultante resta corretta. È il motivo per cui gli
/// editor seri tengono un Eulero "di lavoro" per-entità invece di ri-derivarlo ogni
/// frame dal quaternione; qui non serve finché non dà fastidio.
/// </summary>
internal static class EulerAngles
{
    private const float RadToDeg = 180f / MathF.PI;
    private const float DegToRad = MathF.PI / 180f;

    /// <summary>Scompone il quaternione in <c>(pitch X, yaw Y, roll Z)</c> in gradi.</summary>
    public static Vector3 ToDegrees(Quaternion q)
    {
        // pitch (X): asin del termine che isola la rotazione attorno a X. Vicino a ±1 il
        // valore uscirebbe dal dominio di Asin per errore numerico → lo blocchiamo a ±90°.
        var sinPitch = 2f * (q.W * q.X - q.Y * q.Z);
        var pitch = MathF.Abs(sinPitch) >= 1f
            ? MathF.CopySign(MathF.PI / 2f, sinPitch)
            : MathF.Asin(sinPitch);

        var yaw = MathF.Atan2(2f * (q.W * q.Y + q.X * q.Z), 1f - 2f * (q.X * q.X + q.Y * q.Y));
        var roll = MathF.Atan2(2f * (q.W * q.Z + q.X * q.Y), 1f - 2f * (q.X * q.X + q.Z * q.Z));

        return new Vector3(pitch, yaw, roll) * RadToDeg;
    }

    /// <summary>Ricompone il quaternione da <c>(pitch X, yaw Y, roll Z)</c> in gradi.</summary>
    public static Quaternion ToQuaternion(Vector3 degrees)
    {
        return Quaternion.CreateFromYawPitchRoll(
            degrees.Y * DegToRad,
            degrees.X * DegToRad,
            degrees.Z * DegToRad);
    }
}
