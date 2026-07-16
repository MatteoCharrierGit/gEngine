using System.Numerics;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.MathUtils;

namespace gEngine.Editor;

/// <summary>
/// Da una semiretta all'entità che colpisce: il pezzo dietro "clicco nel viewport e si
/// seleziona".
///
/// ⚠️ <b>Non</b> passa da <c>IPhysicsWorld.Raycast</c>, che il piano dava come
/// precondizione. Guardandolo da vicino era la dipendenza sbagliata, per due motivi che
/// contano più della comodità di riusarlo:
/// <list type="bullet">
///   <item>si seleziona ciò che si <b>vede</b>, e nella scena ci sono entità senza
///   <c>RigidBody</c> (le luci, la lampada figlia del player): col mondo fisico sarebbero
///   invisibili al clic pur essendo lì sullo schermo;</item>
///   <item>i corpi Bepu esistono solo mentre la fisica gira, mentre l'editor deve saper
///   selezionare a simulazione ferma — cioè quasi sempre, e per definizione sempre prima
///   del Play.</item>
/// </list>
/// L'ingombro giusto è lo stesso che usa <c>MeshRenderSystem</c> per il culling, e infatti
/// qui si ricalcola la stessa identica world matrix: quel che si disegna è quel che si clicca.
/// Resta comune il limite già noto — cubo unitario finché non ci sono i bounds per-mesh.
/// </summary>
public static class EntityPicker
{
    /// <returns>L'entità colpita più vicina all'origine della semiretta, o <c>null</c>.</returns>
    public static Entity? Pick(World world, Ray ray)
    {
        Entity? closest = null;
        var closestDistance = float.PositiveInfinity;

        foreach (var (entity, _, meshRenderer) in world.Query<TransformComponent, MeshRendererComponent>())
        {
            // Un'entità nascosta non si clicca: è la stessa regola del disegno, e senza
            // si finirebbe per selezionare qualcosa che non è sullo schermo.
            if (!meshRenderer.Visible)
                continue;

            // La stessa matrice con cui MeshRenderSystem disegna, non una sua copia: è
            // condivisa apposta, perché due formule che devono coincidere e stanno in due
            // file diversi prima o poi non coincidono più.
            var worldMatrix = world.GetRenderMatrix(entity, meshRenderer);

            // Scala nulla su un asse: la matrice non è invertibile e l'entità non ha un
            // volume da colpire. Invert lo dice al posto nostro, senza test sui campi.
            if (!Matrix4x4.Invert(worldMatrix, out var toLocal))
                continue;

            if (!ray.Transform(toLocal).IntersectsUnitCube(out var distance))
                continue;

            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            closest = entity;
        }

        return closest;
    }
}
