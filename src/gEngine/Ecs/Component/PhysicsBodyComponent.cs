using gEngine.Physics;

namespace gEngine.Ecs.Component;

/// <summary>
/// Link runtime entità → corpo nel mondo fisico, aggiunto dal
/// <see cref="gEngine.Ecs.System.PhysicsSystem"/>. Presenza = "il corpo è già stato
/// creato". NON va messo nei file scena (è stato di runtime, non dato d'autore) e non va
/// duplicato: due entità sullo stesso <see cref="BodyId"/> significherebbero due entità
/// che sincronizzano — e poi liberano — lo stesso corpo Bepu. Da qui
/// <see cref="RuntimeStateAttribute"/>, che dice all'editor di saltarlo.
/// </summary>
[RuntimeState]
public struct PhysicsBodyComponent
{
    public BodyId Body;
}
