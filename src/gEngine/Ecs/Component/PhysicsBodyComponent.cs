using gEngine.Physics;

namespace gEngine.Ecs.Component;

/// <summary>
/// Link runtime entità → corpo nel mondo fisico, aggiunto dal
/// <see cref="gEngine.Ecs.System.PhysicsSystem"/>. Presenza = "il corpo è già stato
/// creato". NON va messo nei file scena (è stato di runtime, non dato d'autore).
/// </summary>
public struct PhysicsBodyComponent
{
    public BodyId Body;
}
