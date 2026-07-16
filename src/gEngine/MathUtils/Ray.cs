using System.Numerics;

namespace gEngine.MathUtils;

/// <summary>
/// Semiretta: parte da <c>Origin</c> e va verso <c>Direction</c>. Nasce dal viewport
/// (<see cref="gEngine.Rendering.Camera3D.GetRay"/>) e serve a chiedere al mondo "cosa
/// c'è sotto questo pixel".
///
/// Come <see cref="Frustum"/>, è matematica pura: non sa niente di raylib né dell'ECS.
/// </summary>
public readonly record struct Ray(Vector3 Origin, Vector3 Direction)
{
    public Vector3 PointAt(float distance) => Origin + Direction * distance;

    /// <summary>
    /// Porta la semiretta in un altro spazio. Serve con l'inversa della world matrix di
    /// un'entità: nel suo spazio locale un ingombro ruotato e scalato torna a essere un
    /// cubo allineato agli assi, e il test diventa banale.
    ///
    /// ⚠️ La direzione esce <b>non normalizzata</b>, ed è voluto: origine e direzione
    /// sono scalate insieme, quindi la distanza <c>t</c> che si ottiene nello spazio
    /// locale è la stessa <c>t</c> della semiretta di partenza. È ciò che permette di
    /// confrontare le distanze fra entità con scale diverse e prendere la più vicina.
    /// Normalizzare qui rimescolerebbe quelle distanze fra loro.
    /// </summary>
    public Ray Transform(Matrix4x4 matrix)
    {
        return new Ray(
            Vector3.Transform(Origin, matrix),
            // La direzione è un vettore, non un punto: TransformNormal applica solo la
            // parte lineare. Con Transform ci si porterebbe dietro anche la traslazione,
            // e la semiretta punterebbe altrove.
            Vector3.TransformNormal(Direction, matrix));
    }

    /// <summary>
    /// Interseca il cubo unitario centrato nell'origine (da -0.5 a +0.5 su ogni asse) —
    /// lo stesso ingombro che <c>MeshRenderSystem</c> assume per il frustum culling, e per
    /// lo stesso motivo: finché i bounds reali delle mesh non ci sono (rimandati in Fase
    /// 5), è l'approssimazione già in uso, ed è esatta per <c>MeshKind.Cube</c>.
    ///
    /// Va chiamata sulla semiretta già portata in spazio locale con <see cref="Transform"/>:
    /// un cubo qualsiasi nel mondo <b>è</b> il cubo unitario, visto da lì.
    ///
    /// Metodo delle slab: per ogni asse la semiretta attraversa la lastra fra i due piani
    /// entrando a <c>t1</c> e uscendo a <c>t2</c>; se l'intervallo comune ai tre assi non
    /// è vuoto, la semiretta passa dentro il cubo.
    /// </summary>
    /// <param name="distance">Distanza del primo contatto, valida solo se il test passa.</param>
    public bool IntersectsUnitCube(out float distance)
    {
        distance = 0f;

        var tMin = float.NegativeInfinity;
        var tMax = float.PositiveInfinity;

        for (var axis = 0; axis < 3; axis++)
        {
            var origin = axis switch { 0 => Origin.X, 1 => Origin.Y, _ => Origin.Z };
            var direction = axis switch { 0 => Direction.X, 1 => Direction.Y, _ => Direction.Z };

            if (MathF.Abs(direction) < 1e-8f)
            {
                // Parallela alla lastra: non la attraverserà mai, quindi o è già dentro
                // (e questo asse non vincola nulla) o non c'è intersezione. La divisione
                // per zero darebbe ±infinito e sopravviverebbe al confronto: il caso va
                // tolto di mezzo prima, non dopo.
                if (origin < -0.5f || origin > 0.5f)
                    return false;

                continue;
            }

            var inverse = 1f / direction;
            var t1 = (-0.5f - origin) * inverse;
            var t2 = (0.5f - origin) * inverse;

            // Direzione negativa sull'asse: si entra dal piano "alto". Senza lo scambio,
            // entrata e uscita risulterebbero invertite.
            if (t1 > t2)
                (t1, t2) = (t2, t1);

            tMin = MathF.Max(tMin, t1);
            tMax = MathF.Min(tMax, t2);

            if (tMin > tMax)
                return false;
        }

        // Il cubo è tutto dietro l'origine: lo attraversa la retta, non la semiretta.
        if (tMax < 0f)
            return false;

        // tMin < 0 significa che l'origine è già dentro al cubo (camera dentro un muro):
        // il contatto utile è l'origine stessa.
        distance = MathF.Max(tMin, 0f);
        return true;
    }
}
