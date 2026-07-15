using System.Numerics;

namespace gEngine.MathUtils;

/// <summary>
/// I 6 piani del frustum della camera, in spazio mondo, per il culling. Ogni piano ha
/// la normale rivolta <b>verso l'interno</b> del frustum: un punto è dentro rispetto a
/// un piano quando la sua distanza con segno è &gt;= 0.
///
/// Estrazione dei piani dalla matrice view-projection (metodo Gribb–Hartmann).
/// ⚠️ Convenzione: <c>System.Numerics</c> è row-vector (<c>clip = v * viewProj</c>),
/// quindi le componenti di <c>clip</c> sono <b>colonne</b> della matrice
/// (<c>clip.x = v·(M11,M21,M31) + M41</c>, ecc.). I piani sono combinazioni di quelle
/// colonne — è l'opposto della versione column-vector/OpenGL, stessa famiglia del
/// gotcha del transpose in <c>DrawMesh</c>.
/// </summary>
public readonly struct Frustum
{
    private readonly Plane _left;
    private readonly Plane _right;
    private readonly Plane _bottom;
    private readonly Plane _top;
    private readonly Plane _near;
    private readonly Plane _far;

    private Frustum(Plane left, Plane right, Plane bottom, Plane top, Plane near, Plane far)
    {
        _left = left;
        _right = right;
        _bottom = bottom;
        _top = top;
        _near = near;
        _far = far;
    }

    public static Frustum FromViewProjection(Matrix4x4 m)
    {
        // clip.x = v·(M11,M21,M31)+M41 ; clip.y = ...M*2... ; clip.z = ...M*3... ; clip.w = ...M*4...
        // Un punto è dentro quando, per ogni piano, la combinazione clip.* +/- clip.w >= 0.
        var left   = Plane.Normalize(new Plane(m.M11 + m.M14, m.M21 + m.M24, m.M31 + m.M34, m.M41 + m.M44));
        var right  = Plane.Normalize(new Plane(m.M14 - m.M11, m.M24 - m.M21, m.M34 - m.M31, m.M44 - m.M41));
        var bottom = Plane.Normalize(new Plane(m.M12 + m.M14, m.M22 + m.M24, m.M32 + m.M34, m.M42 + m.M44));
        var top    = Plane.Normalize(new Plane(m.M14 - m.M12, m.M24 - m.M22, m.M34 - m.M32, m.M44 - m.M42));
        // Near in convenzione Direct3D (solo clip.z >= 0), perché
        // Matrix4x4.CreatePerspectiveFieldOfView mappa z in [0, 1], non [-1, 1].
        var near   = Plane.Normalize(new Plane(m.M13, m.M23, m.M33, m.M43));
        var far    = Plane.Normalize(new Plane(m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33, m.M44 - m.M43));

        return new Frustum(left, right, bottom, top, near, far);
    }

    /// <summary>
    /// True se la sfera è dentro o interseca il frustum. Test conservativo: la sfera è
    /// scartata solo se sta <b>interamente oltre</b> almeno un piano
    /// (<c>DotCoordinate(piano, centro) &lt; -radius</c>). Può lasciar passare qualche
    /// falso positivo agli angoli — va benissimo, il culling dev'essere prudente
    /// (meglio disegnare un oggetto in più che scartarne uno visibile).
    /// </summary>
    public bool IntersectsSphere(Vector3 center, float radius)
    {
        return Plane.DotCoordinate(_left,   center) >= -radius
            && Plane.DotCoordinate(_right,  center) >= -radius
            && Plane.DotCoordinate(_bottom, center) >= -radius
            && Plane.DotCoordinate(_top,    center) >= -radius
            && Plane.DotCoordinate(_near,   center) >= -radius
            && Plane.DotCoordinate(_far,    center) >= -radius;
    }
}
