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

    /// <summary>
    /// L'inverso esatto di <see cref="GetForward"/>: la rotazione che fa guardare un
    /// Transform in una direzione. Serve a chi ragiona ancora per "guarda quel punto" e
    /// deve scriverlo in un <see cref="TransformComponent"/> — tipicamente una camera, la
    /// cui posa d'autore è un quaternione ma la cui intenzione è "inquadra il player".
    ///
    /// ⚠️ Sta accanto a <see cref="GetForward"/> apposta: sono la stessa convenzione letta
    /// nei due versi (<c>Forward=+Z</c>, <c>Right=+X</c>, <c>Up=+Y</c>), e se una cambiasse
    /// senza l'altra il giro d'andata e ritorno smetterebbe di chiudere. Row-vector: le
    /// RIGHE della matrice di rotazione sono gli assi, perché
    /// <c>Vector3.Transform(UnitX, R)</c> è la prima riga.
    /// </summary>
    /// <param name="worldUp">
    /// Il riferimento per il roll: dice quale delle infinite rotazioni che guardano in
    /// <paramref name="direction"/> scegliere. Ignorato se è parallelo a
    /// <paramref name="direction"/> (camera a picco): lì il roll è indeterminato — è il
    /// gimbal lock del look-at — e si ripiega su un asse qualunque ortogonale.
    /// </param>
    public static Quaternion LookRotation(Vector3 direction, Vector3 worldUp)
    {
        if (direction.LengthSquared() < 1e-12f)
            return Quaternion.Identity;

        var forward = Vector3.Normalize(direction);
        var right = Vector3.Cross(worldUp, forward);

        // Parallelo a worldUp: il cross degenera a zero e normalizzarlo darebbe NaN — che
        // non esploderebbe qui ma comparirebbe come camera nera molto più a valle.
        if (right.LengthSquared() < 1e-12f)
            right = Vector3.Cross(Vector3.UnitZ, forward);
        if (right.LengthSquared() < 1e-12f)
            right = Vector3.Cross(Vector3.UnitX, forward);

        right = Vector3.Normalize(right);
        var up = Vector3.Cross(forward, right);

        var m = new Matrix4x4(
            right.X, right.Y, right.Z, 0f,
            up.X, up.Y, up.Z, 0f,
            forward.X, forward.Y, forward.Z, 0f,
            0f, 0f, 0f, 1f);

        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(m));
    }
}