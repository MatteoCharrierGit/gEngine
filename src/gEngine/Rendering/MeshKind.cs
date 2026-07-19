namespace gEngine.Rendering;

public enum MeshKind
{
    Cube,

    /// <summary>
    /// Sfera unitaria (raggio 0.5, come il cubo è di lato 1): la <c>Scale</c> del Transform la
    /// dimensiona, e una scala non uniforme la schiaccia in un ellissoide — che è il
    /// comportamento giusto, lo stesso del cubo.
    ///
    /// ⚠️ Il picking la tratta come un <b>cubo</b>: <c>MeshRenderSystem</c> e <c>Ray</c> usano
    /// un AABB a cubo unitario, esatto per <see cref="Cube"/> e approssimato per tutto il
    /// resto. Su una sfera si clicca quindi anche un po' fuori, agli angoli del suo cubo
    /// circoscritto. È lo stesso grado di approssimazione già accettato per <see cref="Model"/>,
    /// non un caso nuovo.
    /// </summary>
    Sphere,

    Plane,
    Grid,
    Model
}